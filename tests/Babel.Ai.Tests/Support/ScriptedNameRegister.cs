using Babel.Contracts.Lookup;

namespace Babel.Ai.Tests.Support;

/// <summary>
/// <b>سجلُّ أسماءٍ مبذورٌ بمقاطعَ مُعلَنة — <u>مِفصلٌ لا مُطابِق</u>.</b>
/// <para>
/// <b>ولماذا لا يُطابِق:</b> مُطابِقُ الإنتاج ثلاثيّاتٌ في PostgreSQL بعد طيٍّ عربيّ،
/// وكتابةُ نظيرٍ له بلغةٍ أخرى تُنشئ <b>تنفيذاً ثانياً ينحرف</b> — فيبقى الإثبات أخضر
/// بينما يتغيّر جواب القاعدة. فهذا الكائن يقرأ ما أعلنه ملفّ المتجهات: مقطعٌ في القائمة
/// ⇒ صفٌّ واحد، وما عداه ⇒ لا شيء. <b>وما يُثبته الملفّ سباكةٌ لا مطابقة</b>: مقطعٌ
/// يُحدَّد، فيُسأل عنه السجلّ، فيصير مِقبضاً، فيمرّ من البوّابة.
/// </para>
/// <para>
/// والمطابقة الحقيقية تُثبَت على PostgreSQL في <c>tests/Babel.Ai.Tests/Lookup</c> وفي
/// <c>TheThirdPartyIsNeverChosen</c> — على مخطّط المبيعات كما ينشئه EF فعلاً.
/// </para>
/// </summary>
internal sealed class ScriptedNameRegister : INameCandidateSource
{
    private readonly Dictionary<string, Guid> _rows = new(StringComparer.Ordinal);
    private readonly HashSet<string> _many = new(StringComparer.Ordinal);

    /// <summary>ينشئ سجلّاً بمقاطعه.</summary>
    /// <param name="registerKey">مفتاح السجلّ.</param>
    /// <param name="resolving">المقاطع التي تُحلّ إلى صفٍّ واحد.</param>
    /// <param name="ambiguous">المقاطع التي يُسأل عنها.</param>
    public ScriptedNameRegister(
        string registerKey,
        IEnumerable<string> resolving,
        IEnumerable<string>? ambiguous = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerKey);
        ArgumentNullException.ThrowIfNull(resolving);

        RegisterKey = registerKey;

        foreach (string span in resolving)
        {
            // معرّفٌ حتميّ مشتقٌّ من المفتاح والمقطع: إثباتان لا ينحرفان، ولا Guid عشوائي.
            _rows[Key(span)] = Deterministic(registerKey + "|" + Key(span));
        }

        foreach (string span in ambiguous ?? [])
        {
            _many.Add(Key(span));
        }
    }

    /// <inheritdoc />
    public string RegisterKey { get; }

    /// <summary>معرّف الصفّ الذي يعيده هذا السجلّ لمقطعٍ بعينه — يقرؤه الإثبات ليسمّي الطرف.</summary>
    /// <param name="span">المقطع.</param>
    public Guid? RowOf(string span) => _rows.TryGetValue(Key(span), out Guid id) ? id : null;

    /// <inheritdoc />
    public Task<NameCandidateProbe> ProbeAsync(
        NameCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string key = Key(request.Text);

        if (_many.Contains(key))
        {
            return Task.FromResult(NameCandidateProbe.Many);
        }

        return Task.FromResult(_rows.TryGetValue(key, out Guid id)
            ? NameCandidateProbe.One(id)
            : NameCandidateProbe.None);
    }

    private static string Key(string span) => Babel.Ai.Voice.VoiceText.Fold(span ?? string.Empty).Trim();

    private static Guid Deterministic(string seed)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return new Guid(hash.AsSpan(0, 16));
    }
}
