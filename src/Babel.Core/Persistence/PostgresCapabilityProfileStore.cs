using Babel.Core.CapabilityProfile;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Core.Persistence;

/// <summary>
/// مخزن ملفّات القدرات فوق PostgreSQL.
/// <para>
/// كان <c>InMemoryCapabilityProfileStore</c>، وسُجِّل ذلك ديناً مؤجَّلاً في ADR-0023.
/// وقد <b>صار حاملاً</b> حين بدأت بوّابة القبول تشترط ملفّاً محفوظاً: خادمٌ أُعيد
/// إقلاعه بلا ملفّ يرفض كل مستند بـ<c>capability_profile.not_found</c> — العطل نفسه
/// الذي يعالجه تثبيت التأسيس، وبالباب نفسه.
/// </para>
/// <para>
/// <b>والمخزَّن هو المسوّدة لا الشكل المشتقّ</b> (انظر
/// <see cref="ValidatedCapabilityProfile.ToDraft"/>): الشكل دالّةٌ في العقد المنشور
/// ويتقادم بتقادمه، والقرار «هذه القدرة مُشغَّلة» هو ما يبقى صحيحاً. ولذلك تمرّ كل
/// قراءة من <see cref="ValidatedCapabilityProfile.Create"/> فتُطابَق بمصفوفة الترحيل من
/// جديد — <b>وقدرةٌ كفّت المصفوفة عن خدمتها تُرفض عند التحميل ولا يُرحَّل بها</b>.
/// </para>
/// </summary>
internal sealed class PostgresCapabilityProfileStore : ICapabilityProfileStore
{
    private readonly DbContextOptions<CoreDbContext> _options;
    private readonly IPostingEventDirectory _directory;

    /// <summary>ينشئ المخزن.</summary>
    /// <param name="options">إعدادات النواة — اتصال <b>دور التطبيق</b> وحده.</param>
    /// <param name="directory">فهرس أحداث المصفوفة الذي يُطابَق به عند كل قراءة.</param>
    public PostgresCapabilityProfileStore(CoreOptions options, IPostingEventDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(directory);

        DbContextOptionsBuilder<CoreDbContext> builder = new();
        builder.UseNpgsql(options.AppConnectionString);
        _options = builder.Options;
        _directory = directory;
    }

    /// <inheritdoc />
    public async ValueTask<ValidatedCapabilityProfile?> FindAsync(
        TenantId tenant,
        CancellationToken cancellationToken = default)
    {
        Guid company = tenant.Value;

        await using CoreDbContext context = new(_options);

        List<CapabilityProfileDocumentRow> documents = await context.ProfileDocuments
            .AsNoTracking()
            .Where(row => row.CompanyId == company)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (documents.Count == 0)
        {
            return null;
        }

        List<CapabilityProfileCapabilityRow> capabilities = await context.ProfileCapabilities
            .AsNoTracking()
            .Where(row => row.CompanyId == company)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CapabilityProfileDefaultRow> defaults = await context.ProfileDefaults
            .AsNoTracking()
            .Where(row => row.CompanyId == company)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, DocumentProfileDraft> drafts = new(StringComparer.Ordinal);

        foreach (CapabilityProfileDocumentRow document in documents)
        {
            Dictionary<string, bool> enabled = new(StringComparer.Ordinal);

            foreach (CapabilityProfileCapabilityRow row in capabilities)
            {
                if (string.Equals(row.DocumentType, document.DocumentType, StringComparison.Ordinal))
                {
                    enabled[row.Capability] = row.Enabled;
                }
            }

            Dictionary<string, string> values = new(StringComparer.Ordinal);

            foreach (CapabilityProfileDefaultRow row in defaults)
            {
                if (string.Equals(row.DocumentType, document.DocumentType, StringComparison.Ordinal))
                {
                    values[row.Field] = row.Value;
                }
            }

            drafts[document.DocumentType] = new DocumentProfileDraft(enabled, values);
        }

        Result<ValidatedCapabilityProfile> profile =
            ValidatedCapabilityProfile.Create(new CapabilityProfileDraft(drafts), _directory);

        // صفوفٌ لا تُنتج ملفّاً صالحاً خللٌ في المخزن لا «غياب ملفّ». وإرجاعُها فارغةً
        // يجعل الخادم يقول «لا ملفّ لهذا المستأجر» عن مستأجر له ملفّ تالف — فيصمت
        // العطل بدل أن يُبلَّغ عنه.
        return profile.IsFailure
            ? throw new InvalidOperationException(
                "ملفّ قدرات مخزَّن لا يجتاز المطابقة بمصفوفة الترحيل: "
                + string.Join(" | ", profile.Errors.Select(static error => error.ToString())))
            : profile.Value;
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        TenantId tenant,
        ValidatedCapabilityProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Guid company = tenant.Value;
        CapabilityProfileDraft draft = profile.ToDraft();

        await using CoreDbContext context = new(_options);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // الملفّ مورد واحد يُقرأ ويُستبدل كلّاً (ADR-0023): فالاستبدال محوٌ ثم كتابة
        // داخل معاملة واحدة، لا دمجٌ صفّاً صفّاً يترك قدرةً مُطفأةً حيّةً في الجدول.
        context.ProfileDocuments.RemoveRange(context.ProfileDocuments.Where(row => row.CompanyId == company));
        context.ProfileCapabilities.RemoveRange(context.ProfileCapabilities.Where(row => row.CompanyId == company));
        context.ProfileDefaults.RemoveRange(context.ProfileDefaults.Where(row => row.CompanyId == company));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (KeyValuePair<string, DocumentProfileDraft> document in draft.Documents)
        {
            context.ProfileDocuments.Add(new CapabilityProfileDocumentRow
            {
                CompanyId = company,
                DocumentType = document.Key,
            });

            foreach (KeyValuePair<string, bool> capability in document.Value.Capabilities)
            {
                context.ProfileCapabilities.Add(new CapabilityProfileCapabilityRow
                {
                    CompanyId = company,
                    DocumentType = document.Key,
                    Capability = capability.Key,
                    Enabled = capability.Value,
                });
            }

            foreach (KeyValuePair<string, string> value in document.Value.Defaults)
            {
                context.ProfileDefaults.Add(new CapabilityProfileDefaultRow
                {
                    CompanyId = company,
                    DocumentType = document.Key,
                    Field = value.Key,
                    Value = value.Value,
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
