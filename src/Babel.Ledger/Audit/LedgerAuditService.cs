using System.Globalization;
using System.Text.Json;
using Babel.Canonicalization;
using Babel.Canonicalization.Schemas;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Ledger.Audit;

/// <summary>
/// حكم إعادة التحقق من سلسلة نطاق واحد.
/// <para>
/// <b>ولماذا «أول تسلسل منحرف» لا «هل السلسلة سليمة»:</b> المدقّق لا يسأل «هل عُبث؟»
/// بل «أين؟ ومتى؟ وما الذي بعده يجب أن يُراجَع؟». إجابة منطقية واحدة لا تصلح تقريراً.
/// </para>
/// </summary>
/// <param name="Ok">هل النطاق سليم كاملاً؟</param>
/// <param name="Checked">عدد السجلات التي فُحصت، بما فيها السجل المنحرف.</param>
/// <param name="FirstDivergentSequence">أول رقم تسلسل منحرف، أو <c>null</c>.</param>
/// <param name="Verdict">رمز الحكم الثابت.</param>
/// <param name="ReasonAr">شرح عربي صالح للعرض في تقرير تدقيق.</param>
/// <param name="Detail">تفاصيل فنّية: البصمات المتوقّعة والمخزَّنة.</param>
public sealed record LedgerChainReport(
    bool Ok,
    int Checked,
    long? FirstDivergentSequence,
    string Verdict,
    string ReasonAr,
    string? Detail);

/// <summary>
/// صفّ في ميزان المراجعة.
/// <para>
/// <b>الاسم <see cref="TranslatedName"/> لا زوجاً ثابتاً</b> (ADR-0021): كان الصفّ يحمل
/// <c>NameAr</c> و<c>NameEn</c>، وهما <b>عاجزان بنيوياً</b> عن لغة ثالثة — فالمحاسب
/// الأردي في ميزان مراجعة كان يرى السجلّ العربي ومعه ترجمة إنجليزية لا ترجمةً بلغته.
/// والآن يحمل الصفّ السجلّ العربي وكل ترجمة موجودة، ولغةٌ خامسة صفوفُ إدخال.
/// </para>
/// </summary>
/// <param name="AccountCode">رمز الحساب — <b>معرّف لا نصّ</b>، فلا يُترجَم أبداً.</param>
/// <param name="Name">اسم الحساب: سجلٌّ عربي إلزامي وترجماته.</param>
/// <param name="Debit">مجموع المدين بعملة الشركة.</param>
/// <param name="Credit">مجموع الدائن بعملة الشركة.</param>
public sealed record TrialBalanceRow(string AccountCode, TranslatedName Name, decimal Debit, decimal Credit);

/// <summary>
/// ميزان المراجعة كاملاً: صفوفه، ومجموعاه، وحكم توازنه.
/// <para>
/// <b>ولماذا المجموعان هنا لا في طبقة أعلى:</b> جمع عمود مالي حسابٌ على المال. في طبقة
/// HTTP يمنعه البند (أ) من القاعدة 13 — ومنعه صحيح؛ وفي المتصفّح يُنتج الفخّ نفسه الذي
/// بُني له شكل السلك، لأن <c>Number</c> في JavaScript فاصلة عائمة ثنائية. والموضع
/// الصحيح <c>sum()</c> على <c>numeric</c> داخل PostgreSQL: جمعٌ مضبوط بلا فاصلة عائمة
/// في أي خطوة، ومن <b>الاستعلام نفسه</b> الذي أنتج الصفوف — لا من استعلام ثانٍ قد يقرأ
/// لقطة أخرى.
/// </para>
/// <para>
/// <b>ولماذا سجلٌّ مسمّى لا صفٌّ مجهول الاسم (‏tuple):</b> كان هذا النوع صفّاً مجهولاً
/// فترةً، لا لسبب في المجال بل لأن قائمة <c>PublishedModuleSurface</c> في القاعدة 13 كانت
/// في ملف خارج ملكية الفرع الذي كتب النوع، فصار الالتفاف أرخص من تسجيل القرار. وقد
/// أُنزل القرار: الاسم مُضاف إلى القائمة بسببه المكتوب بجانبه. والصفّ المجهول كان أسوأ
/// للحارس نفسه لا أفضل: <c>System.ValueTuple</c> ليس نوعاً في تجميعة وحدة، فلا يراه
/// الحارس أصلاً — أي أن مفردةً من مفردات الدفتر كانت تعبر إلى طبقة HTTP <b>خارج</b>
/// إحصاء السطح المنشور بدل أن تكون فيه.
/// </para>
/// </summary>
/// <param name="Rows">الصفوف مرتّبة برمز الحساب.</param>
/// <param name="TotalDebit">مجموع المدين بعملة الشركة، من <c>sum()</c> لا من جمع الصفوف.</param>
/// <param name="TotalCredit">مجموع الدائن بعملة الشركة، من <c>sum()</c> كذلك.</param>
/// <param name="Balanced">
/// هل تساوى المجموعان؟ يُحسم هنا كي لا يُقارَن مبلغان في JavaScript. وميزانٌ غير متوازن
/// <b>يُرى</b>: لا يُقرَّب ولا يُخفى.
/// </param>
public sealed record TrialBalanceReport(
    IReadOnlyList<TrialBalanceRow> Rows,
    decimal TotalDebit,
    decimal TotalCredit,
    bool Balanced);

/// <summary>
/// حسابٌ واحد كما يراه من يريد أن يُرحّل عليه — <b>بشروطه لا باسمه وحده</b>.
/// <para>
/// <b>ولماذا الشروط على الحساب لا في وثيقة:</b> الدفتر يرفض السطر بـ
/// <c>ledger.posting.missing_subledger</c> حين يغيب طرفُ الأستاذ المساعد، وبـ
/// <c>guard.GR-COA-002</c> حين يغيب بُعدٌ إلزامي. والرسالتان ممتازتان — تُسمّيان الحساب
/// والطرف والبُعد — لكنّ العميل كان <b>لا يبلغهما إلا بأن يُرحِّل فيُرفَض</b>: لا سطح
/// منشور يقول له، قبل الترحيل، إنّ هذا الحساب يطلب عميلاً أو مركز تكلفة. أي أنّ الشرط
/// كان معلوماً للخادم ومجهولاً للعميل، والفارق بينهما يُدفَع ثمنُه قيداً مرفوضاً.
/// </para>
/// <para>
/// <b>وهذا نموذج قراءة لا مفتاح ترحيل:</b> لا شيء هنا يُنشئ سطراً، ولا يختار دوراً، ولا
/// يستبدل مصفوفة الترحيل. القاعدة 2 قائمة كما هي — الوحدة تصف حدثاً والمصفوفة تختار
/// الحساب — وما يُنشَر هنا هو <b>ما على الشاشة اليدوية أن تعرفه</b> قبل أن تسأل الدفتر.
/// </para>
/// </summary>
/// <param name="Code">رمز الحساب — معرّف لا نصّ، فلا يُترجَم أبداً.</param>
/// <param name="Name">اسم الحساب: سجلٌّ عربي إلزامي وترجماته (ADR-0021).</param>
/// <param name="ParentCode">رمز الأب، أو <c>null</c> لجذر. الشجرة تُبنى منه.</param>
/// <param name="Level">مستوى الحساب في الشجرة، من 1 إلى 4.</param>
/// <param name="AccountType">‏asset · liability · equity · revenue · expense.</param>
/// <param name="NaturalSide">‏debit أو credit — الجانب الطبيعي.</param>
/// <param name="IsPostable">هل يقبل سطراً مباشرةً؟ التجميعي لا يقبل (‏GR-COA-001).</param>
/// <param name="IsContra">هل هو حساب مقابل يقف على غير جانبه الطبيعي؟</param>
/// <param name="SubledgerType">
/// نوع طرف الأستاذ المساعد الذي يطلبه الحساب، و<c>none</c> إن لم يطلب شيئاً. وغيابُ
/// الطرف على حساب يطلبه هو <c>ledger.posting.missing_subledger</c> بعينه.
/// </param>
/// <param name="RequiredDimensions">
/// الأبعاد الإلزامية على كل سطر يقع على هذا الحساب، وقد تكون فارغة. وغيابُ أحدها هو
/// <c>guard.GR-COA-002</c> بعينه.
/// </param>
/// <param name="CurrencyMode">‏any أو company_only أو fixed.</param>
/// <param name="CurrencyCode">العملة المثبَّتة حين يكون النمط <c>fixed</c>، وإلا <c>null</c>.</param>
/// <param name="IsActive">هل الحساب مستعمَل؟ المعطَّل لا يُعرَض للاختيار.</param>
public sealed record ChartAccount(
    string Code,
    TranslatedName Name,
    string? ParentCode,
    int Level,
    string AccountType,
    string NaturalSide,
    bool IsPostable,
    bool IsContra,
    string SubledgerType,
    IReadOnlyList<string> RequiredDimensions,
    string CurrencyMode,
    string? CurrencyCode,
    bool IsActive);

/// <summary>
/// دليل حسابات شركة واحدة كاملاً، ومعه عدّاداه.
/// <para>
/// <b>ولماذا الدليل كلّه لا القابل للترحيل وحده:</b> الشجرة تُعرَض بآبائها. قائمةٌ من
/// الأوراق وحدها تجعل العميل يخترع تجميعاً من بادئات الرموز — وهو اشتقاقٌ يصحّ اليوم
/// ويكذب عند أول دليل عميل لا تُطابق فيه البادئةُ الأبَ. و<see cref="ChartAccount.IsPostable"/>
/// على كل مدخل يجعل الترشيح عند العميل مجّانياً وبلا طلبٍ ثانٍ.
/// </para>
/// <para>
/// <b>والعدّادان يصلان محسوبَين</b> لا لأن جمعهما صعب، بل لأن عميلاً يعدّ بنفسه لا يملك
/// ما يقارن به حين تصل الصفحة ناقصة: العدّاد المُعلَن هو ما يجعل النقص <b>يُرى</b>.
/// </para>
/// </summary>
/// <param name="Accounts">الحسابات مرتّبة برمزها ترتيباً حرفياً ثابتاً.</param>
/// <param name="AccountCount">عدد الحسابات كلّها.</param>
/// <param name="PostableCount">عدد ما يقبل الترحيل منها.</param>
public sealed record ChartOfAccountsReport(
    IReadOnlyList<ChartAccount> Accounts,
    int AccountCount,
    int PostableCount);

/// <summary>
/// قراءات التدقيق على الدفتر: إعادة التحقق من السلسلة، وميزان المراجعة.
/// <para>
/// كلاهما <b>قراءة محضة</b> ويعملان بدور التطبيق نفسه — الذي لا يملك
/// <c>UPDATE</c> ولا <c>DELETE</c>. أي أن أداة التدقيق لا تستطيع أن تُصلح ما
/// تكتشفه، وهذا هو المطلوب بالضبط.
/// </para>
/// </summary>
public sealed class LedgerAuditService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly LedgerRuntime _runtime;

    /// <summary>ينشئ خدمة التدقيق.</summary>
    public LedgerAuditService(IEntitlementEnforcer enforcer, LedgerRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _runtime = runtime;
    }

    /// <summary>
    /// يعيد التحقق من سلسلة نطاق (شركة × دفتر × سنة مالية) كاملاً، من بصمة التكوين
    /// حتى الرأس، ويسمّي <b>أول</b> تسلسل منحرف.
    /// <para>
    /// المستند يُعاد بناؤه من <b>الحقيقة المجالية المخزَّنة</b> — الأعمدة نفسها —
    /// لا من <c>canonical_bytes</c> المخزَّنة: مقارنة البايتات بنفسها لا تُثبت شيئاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل الحقيقي — من الاعتماد، لا فاعل نظام. محور «المستخدم الفاعل» يقرأه.</param>
    /// <param name="book">الدفتر.</param>
    /// <param name="fiscalYear">السنة المالية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Read)]
    public async ValueTask<Result<LedgerChainReport>> VerifyChainAsync(
        TenantId tenant,
        UserId actor,
        string book,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ledger, EntitlementAccess.Read, "Ledger.VerifyChain", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<LedgerChainReport>.Failure(gate.Errors);
        }

        List<ChainRecord> records = await ReadChainAsync(tenant.Value, book, fiscalYear, cancellationToken).ConfigureAwait(false);

        byte[] genesis = JournalEntrySchema.Genesis(
            tenant.Value.ToString("D", CultureInfo.InvariantCulture), book, fiscalYear);

        ChainVerification verification = ChainVerifier.VerifyChain(records, genesis);

        return Result<LedgerChainReport>.Success(new LedgerChainReport(
            verification.Ok,
            verification.Checked,
            verification.FirstDivergentSequence,
            verification.Verdict,
            verification.ReasonAr,
            verification.Detail));
    }

    /// <summary>
    /// ميزان المراجعة <b>من السطور غير القابلة للتعديل</b> لا من جدول الأرصدة.
    /// <para>
    /// وهذا هو الفحص الذي يعني شيئاً: جدول الأرصدة إسقاط، والسطور هي الحقيقة.
    /// مقارنة الاثنين هي التي تُظهر انحراف الإسقاط — وانحرافه هو «الرقم الخاطئ
    /// الصامت» بعينه (ADR-0004 · فخ-06).
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل الحقيقي — من الاعتماد، لا فاعل نظام. محور «المستخدم الفاعل» يقرأه.</param>
    /// <param name="book">الدفتر.</param>
    /// <param name="periodCode">رمز الفترة، أو <c>null</c> لكل الفترات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Read)]
    public async ValueTask<Result<TrialBalanceReport>> TrialBalanceFromLinesAsync(
        TenantId tenant,
        UserId actor,
        string book,
        string? periodCode,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ledger, EntitlementAccess.Read, "Ledger.TrialBalance", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<TrialBalanceReport>.Failure(gate.Errors);
        }

        List<TrialBalanceRow> rows = [];
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        await using NpgsqlConnection connection =
            await _runtime.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // ── المجموعان من الاستعلام نفسه، بـ grouping sets ────────────────────
        // مجموعة تجميع للتفصيل وأخرى فارغة للإجمالي: رحلة واحدة، ولقطة واحدة من
        // البيانات (استعلامان يفصل بينهما ترحيل يُنتجان صفوفاً ومجموعاً لا يتطابقان).
        // و‏coalesce لأن مجموعة التجميع الفارغة تُنتج صفّاً ولو كان الدخل صفراً.
        await using NpgsqlCommand command = new(
            """
            select l.account_code, a.name_ar,
                   coalesce(sum(l.debit_company), 0) as debit,
                   coalesce(sum(l.credit_company), 0) as credit,
                   grouping(l.account_code) as is_total,
                   -- الترجمات في **الرحلة نفسها**: استعلام فرعي قياسي على المفتاح
                   -- الأساسي لجدول الترجمات، لا انضمام يضاعف الصفوف فيفسد sum().
                   -- ويعود null لصفّ الإجمالي، وهو صفّ بلا حساب فلا اسم له أصلاً.
                   (select jsonb_object_agg(t.language_tag, t.name)
                      from ledger.name_translation t
                     where t.company_id = $1 and t.entity_kind = 'account'
                       and t.entity_key = l.account_code) as translations
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
              join ledger.account a on a.company_id = l.company_id and a.account_code = l.account_code
             where l.company_id = $1 and e.book_id = $2 and ($3::text is null or e.period_code = $3)
             group by grouping sets ((l.account_code, a.name_ar), ())
             order by grouping(l.account_code), l.account_code
            """, connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(book);
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)periodCode ?? DBNull.Value });

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.GetInt32(4) == 1)
            {
                totalDebit = reader.GetDecimal(2);
                totalCredit = reader.GetDecimal(3);
                continue;
            }

            rows.Add(new TrialBalanceRow(
                reader.GetString(0),
                new TranslatedName(reader.GetString(1), Translations(reader, 5)),
                reader.GetDecimal(2),
                reader.GetDecimal(3)));
        }

        // ‏المساواة تُحسم هنا لا عند العميل: مقارنتها في JavaScript تعيد الفخّ نفسه
        // الذي بُني له شكل السلك — ‏Number فاصلة عائمة ثنائية.
        return Result<TrialBalanceReport>.Success(
            new TrialBalanceReport(rows, totalDebit, totalCredit, totalDebit == totalCredit));
    }

    /// <summary>
    /// دليل حسابات الشركة <b>بشروط الترحيل على كل حساب</b> — قراءةٌ محضة.
    /// <para>
    /// <b>ولماذا وُجدت هذه الدالّة:</b> قِيس على <c>data/chart-of-accounts/accounts.csv</c>
    /// أنّ 180 حساباً، منها 109 يقبل الترحيل، منها <b>42 يطلب طرف أستاذ مساعد</b> و<b>45
    /// يطلب بُعداً إلزامياً</b>. ولم يكن أيٌّ من ذلك منشوراً على أي سطح: شاشةُ قيدٍ يدوية
    /// بُنيت من العقد المنشور وحده <b>لم تستطع تكوين قيد صالح من أول محاولة</b>، وأول
    /// ترحيل حقيقي لها رُفض بـ<c>ledger.posting.missing_subledger</c> و<c>guard.GR-COA-002</c>.
    /// أي أنّ الشرط كان يُكتشَف <b>بالرفض</b> وحده.
    /// </para>
    /// <para>
    /// <b>ولا استحقاق ثانٍ:</b> البوّابة هنا هي <c>IEntitlementEnforcer</c> نفسها التي
    /// يمرّ بها ميزان المراجعة وإعادة التحقق، بحقّ <see cref="EntitlementAccess.Read"/>.
    /// ومستأجرٌ انقطع اشتراكه ينحدر إلى <b>القراءة فقط لا إلى المنع</b> (ADR-0034) —
    /// فهذه الدالّة تخدمه، وهو ما يُشهَد عليه من فوق HTTP لا من داخل العملية.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل الحقيقي — من الاعتماد، لا فاعل نظام (فخ-58).</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ledger, EntitlementAccess.Read)]
    public async ValueTask<Result<ChartOfAccountsReport>> ChartOfAccountsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ledger, EntitlementAccess.Read, "Ledger.ChartOfAccounts", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ChartOfAccountsReport>.Failure(gate.Errors);
        }

        List<ChartAccount> accounts = [];
        int postable = 0;

        await using NpgsqlConnection connection =
            await _runtime.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // ‏الترتيب في الاستعلام لا عند العميل: مخرَجٌ مرتَّب ترتيباً كلّياً ثابتاً هو
        // الفارق بين مقارنةِ استجابتين ومقارنةِ مجموعتين (فخ-10). والترتيب حرفي على
        // الرمز — `C` — لا ترتيب مُقابَلة يتبع ثقافة الخادم (فخ-38).
        //
        // والترجمات في **الرحلة نفسها**: استعلام فرعي قياسي على المفتاح الأساسي لجدول
        // الترجمات، لا انضمام يضاعف الصفوف.
        await using NpgsqlCommand command = new(
            """
            select a.account_code, a.name_ar, a.parent_code, a.account_level,
                   a.account_type, a.natural_side, a.is_postable, a.is_contra,
                   a.subledger_type, a.required_dimensions, a.currency_mode,
                   a.currency_code, a.is_active,
                   (select jsonb_object_agg(t.language_tag, t.name)
                      from ledger.name_translation t
                     where t.company_id = a.company_id and t.entity_kind = 'account'
                       and t.entity_key = a.account_code) as translations
              from ledger.account a
             where a.company_id = $1
             order by a.account_code collate "C"
            """, connection);
        command.Parameters.AddWithValue(tenant.Value);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            bool isPostable = reader.GetBoolean(6);
            if (isPostable)
            {
                postable++;
            }

            accounts.Add(new ChartAccount(
                reader.GetString(0),
                new TranslatedName(reader.GetString(1), Translations(reader, 13)),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                isPostable,
                reader.GetBoolean(7),
                reader.GetString(8),
                reader.GetFieldValue<string[]>(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetBoolean(12)));
        }

        return Result<ChartOfAccountsReport>.Success(
            new ChartOfAccountsReport(accounts, accounts.Count, postable));
    }

    /// <summary>
    /// يقرأ خريطة الترجمات من عمود <c>jsonb</c>. والغياب خريطة فارغة لا استثناء:
    /// حسابٌ بلا ترجمة حالةٌ مشروعة تماماً، ويرتدّ عرضه إلى السجلّ العربي.
    /// </summary>
    private static Dictionary<string, string> Translations(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return [];
        }

        Dictionary<string, string> map = new(StringComparer.Ordinal);

        using JsonDocument document = JsonDocument.Parse(reader.GetFieldValue<string>(ordinal));

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            string? value = property.Value.GetString();

            if (!string.IsNullOrWhiteSpace(value))
            {
                map[property.Name] = value;
            }
        }

        return map;
    }

    /// <summary>سطر قيد مقروءاً من التخزين، بكل عموده — لا بستّة منها.</summary>
    private sealed record StoredLine(
        int LineNo,
        string AccountCode,
        string RoleCode,
        string Qualifier,
        decimal Debit,
        decimal Credit,
        string Currency,
        decimal FxRate,
        decimal DebitCompany,
        decimal CreditCompany,
        string? BranchId,
        string? CostCenterId,
        string? ProjectId,
        string? PropertyId,
        string? UnitId,
        string? WarehouseId,
        string? BoqItemId,
        string SubledgerKind,
        string? SubledgerPartyId,
        string Description,
        string DescriptionAr);

    /// <summary>
    /// يقرأ السلسلة كاملة ويعيد بناء كل مستند <b>بمخطّط إصداره المخزَّن</b>.
    /// <para>
    /// وهذا هو موضع «التوزيع بالإصدار» عملياً: سجل كُتب تحت v1 يُعاد بناؤه بحقول
    /// v1 بالضبط، وسجل v2 بحقول v2. سلسلة واحدة قد تحمل الاثنين — أول ترقية في
    /// دفتر قائم تنتج ذلك حتماً — وإعادة بناء الكل بمخطّط واحد تكسر نصف السلسلة
    /// بلا أن يتغيّر حرف في البيانات.
    /// </para>
    /// </summary>
    private async Task<List<ChainRecord>> ReadChainAsync(
        Guid companyId,
        string book,
        int fiscalYear,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, List<StoredLine>> lines = new();

        await using NpgsqlConnection connection =
            await _runtime.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand command = new(
            """
            select l.entry_id, l.line_no, l.account_code, l.role_code, l.qualifier,
                   l.debit, l.credit, l.currency, l.fx_rate, l.debit_company, l.credit_company,
                   l.branch_id, l.cost_center_id, l.project_id, l.property_id, l.unit_id,
                   l.warehouse_id, l.boq_item_id, l.subledger_kind, l.subledger_party_id,
                   l.description, l.description_ar
              from ledger.journal_line l
              join ledger.chain_link c on c.entry_id = l.entry_id
             where c.company_id = $1 and c.book_id = $2 and c.fiscal_year = $3
             order by l.entry_id, l.line_no
            """, connection))
        {
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(book);
            command.Parameters.AddWithValue(fiscalYear);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                Guid entryId = reader.GetGuid(0);
                if (!lines.TryGetValue(entryId, out List<StoredLine>? list))
                {
                    list = [];
                    lines[entryId] = list;
                }

                list.Add(new StoredLine(
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetDecimal(5),
                    reader.GetDecimal(6),
                    reader.GetString(7),
                    reader.GetDecimal(8),
                    reader.GetDecimal(9),
                    reader.GetDecimal(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.IsDBNull(13) ? null : reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.IsDBNull(15) ? null : reader.GetString(15),
                    reader.IsDBNull(16) ? null : reader.GetString(16),
                    reader.IsDBNull(17) ? null : reader.GetString(17),
                    reader.GetString(18),
                    reader.IsDBNull(19) ? null : reader.GetString(19),
                    reader.GetString(20),
                    reader.GetString(21)));
            }
        }

        List<ChainRecord> records = [];

        await using NpgsqlCommand chain = new(
            """
            select c.chain_seq, c.canon_version, c.prev_hash, c.entry_hash,
                   e.entry_id, e.entry_no, e.entry_date, e.posted_at, e.status, e.actor,
                   e.memo, e.memo_ar, e.source_doc_type, e.source_doc_id, e.idempotency_key, e.currency,
                   e.period_code, e.source_module, e.posting_trigger_code, e.posting_generation,
                   e.event_code, e.reverses_entry_id, e.reversal_reason_ar, e.reversal_reason_en,
                   e.closed_period_permission, e.closed_period_authoriser
              from ledger.chain_link c
              join ledger.journal_entry e on e.entry_id = c.entry_id
             where c.company_id = $1 and c.book_id = $2 and c.fiscal_year = $3
             order by c.chain_seq
            """, connection);
        chain.Parameters.AddWithValue(companyId);
        chain.Parameters.AddWithValue(book);
        chain.Parameters.AddWithValue(fiscalYear);

        await using NpgsqlDataReader chainReader = await chain.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await chainReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            Guid entryId = chainReader.GetGuid(4);
            string canonVersion = chainReader.GetString(1);
            List<StoredLine> entryLines = lines.TryGetValue(entryId, out List<StoredLine>? found) ? found : [];

            CanonicalDocument document = canonVersion == CanonicalV2.Version
                ? RebuildV2(companyId, book, fiscalYear, chainReader, entryLines)
                : RebuildV1(companyId, book, fiscalYear, chainReader, entryLines);

            records.Add(new ChainRecord
            {
                Sequence = chainReader.GetInt64(0),
                CanonVersion = canonVersion,
                Document = document,
                StoredPreviousHash = chainReader.GetFieldValue<byte[]>(2),
                StoredHash = chainReader.GetFieldValue<byte[]>(3),
            });
        }

        return records;
    }

    /// <summary>إعادة بناء مستند v2 من الحقيقة المجالية المخزَّنة.</summary>
    private static CanonicalDocument RebuildV2(
        Guid companyId, string book, int fiscalYear, NpgsqlDataReader row, List<StoredLine> entryLines)
    {
        CanonicalDocumentBuilder builder = JournalEntrySchema.V2.NewDocument();
        builder.Set("tenant_id", CanonicalValue.Text(companyId.ToString("D", CultureInfo.InvariantCulture)));
        builder.Set("book_id", CanonicalValue.Text(book));
        builder.Set("fiscal_year", CanonicalValue.Integer(fiscalYear));
        builder.Set("entry_id", CanonicalValue.Uuid(row.GetGuid(4)));
        builder.Set("entry_no", CanonicalValue.Integer(row.GetInt64(5)));
        builder.Set("entry_date", CanonicalValue.Date(row.GetFieldValue<DateOnly>(6)));
        builder.Set("period_code", CanonicalValue.Text(row.GetString(16)));
        builder.Set("posted_at", CanonicalValue.Instant(row.GetFieldValue<DateTime>(7)));
        builder.Set("status", CanonicalValue.Token(row.GetString(8)));
        builder.Set("reverses_entry_id", row.IsDBNull(21)
            ? CanonicalValue.Null()
            : CanonicalValue.Uuid(row.GetGuid(21)));
        builder.Set("reversal_reason_ar", CanonicalValue.TextOrNull(row.IsDBNull(22) ? null : row.GetString(22)));
        builder.Set("reversal_reason_en", CanonicalValue.TextOrNull(row.IsDBNull(23) ? null : row.GetString(23)));
        builder.Set("source_module", CanonicalValue.Text(row.GetString(17)));
        builder.Set("source_doc_type", CanonicalValue.Text(row.GetString(12)));
        builder.Set("source_doc_id", CanonicalValue.Text(row.GetString(13)));
        builder.Set("posting_trigger_code", CanonicalValue.Text(row.GetString(18)));
        builder.Set("posting_generation", CanonicalValue.Integer(row.GetInt32(19)));
        builder.Set("event_code", CanonicalValue.Text(row.GetString(20)));
        builder.Set("idempotency_key", CanonicalValue.Text(row.GetString(14)));
        builder.Set("currency", CanonicalValue.Token(row.GetString(15)));
        builder.Set("actor", CanonicalValue.Text(row.GetString(9)));
        builder.Set("closed_period_permission", CanonicalValue.TextOrNull(row.IsDBNull(24) ? null : row.GetString(24)));
        builder.Set("closed_period_authoriser", CanonicalValue.TextOrNull(row.IsDBNull(25) ? null : row.GetString(25)));
        builder.Set("memo", CanonicalValue.Text(row.GetString(10)));
        builder.Set("memo_ar", CanonicalValue.Text(row.GetString(11)));

        builder.SetGroup("lines", entryLines.Select(static line => new Action<CanonicalItemBuilder>(item =>
        {
            item.Set("line_no", CanonicalValue.Integer(line.LineNo));
            item.Set("account_code", CanonicalValue.Text(line.AccountCode));
            item.Set("role_code", CanonicalValue.Text(line.RoleCode));
            item.Set("qualifier", CanonicalValue.Text(line.Qualifier));
            item.Set("debit", CanonicalValue.Amount(line.Debit));
            item.Set("credit", CanonicalValue.Amount(line.Credit));
            item.Set("currency", CanonicalValue.Token(line.Currency));
            item.Set("fx_rate", CanonicalValue.Rate(line.FxRate));
            item.Set("debit_company", CanonicalValue.Amount(line.DebitCompany));
            item.Set("credit_company", CanonicalValue.Amount(line.CreditCompany));
            item.Set("branch_id", CanonicalValue.TextOrNull(line.BranchId));
            item.Set("cost_center_id", CanonicalValue.TextOrNull(line.CostCenterId));
            item.Set("project_id", CanonicalValue.TextOrNull(line.ProjectId));
            item.Set("property_id", CanonicalValue.TextOrNull(line.PropertyId));
            item.Set("unit_id", CanonicalValue.TextOrNull(line.UnitId));
            item.Set("warehouse_id", CanonicalValue.TextOrNull(line.WarehouseId));
            item.Set("boq_item_id", CanonicalValue.TextOrNull(line.BoqItemId));
            item.Set("tax_code", CanonicalValue.Null());
            item.Set("subledger_kind", CanonicalValue.Text(line.SubledgerKind));
            item.Set("subledger_party_id", CanonicalValue.TextOrNull(line.SubledgerPartyId));
            item.Set("description", CanonicalValue.Text(line.Description));
            item.Set("description_ar", CanonicalValue.Text(line.DescriptionAr));
        })));

        return builder.Build();
    }

    /// <summary>إعادة بناء مستند v1 — <b>مجمَّد</b>، بحقول v1 وحدها.</summary>
    private static CanonicalDocument RebuildV1(
        Guid companyId, string book, int fiscalYear, NpgsqlDataReader row, List<StoredLine> entryLines)
    {
        CanonicalDocumentBuilder builder = JournalEntrySchema.V1.NewDocument();
        builder.Set("tenant_id", CanonicalValue.Text(companyId.ToString("D", CultureInfo.InvariantCulture)));
        builder.Set("book_id", CanonicalValue.Text(book));
        builder.Set("fiscal_year", CanonicalValue.Integer(fiscalYear));
        builder.Set("entry_id", CanonicalValue.Uuid(row.GetGuid(4)));
        builder.Set("entry_no", CanonicalValue.Integer(row.GetInt64(5)));
        builder.Set("entry_date", CanonicalValue.Date(row.GetFieldValue<DateOnly>(6)));
        builder.Set("posted_at", CanonicalValue.Instant(row.GetFieldValue<DateTime>(7)));
        builder.Set("status", CanonicalValue.Token(row.GetString(8)));
        builder.Set("actor", CanonicalValue.Text(row.GetString(9)));
        builder.Set("memo", CanonicalValue.Text(row.GetString(10)));
        builder.Set("memo_ar", CanonicalValue.Text(row.GetString(11)));
        builder.Set("source_ref", CanonicalValue.Text(row.GetString(12) + "/" + row.GetString(13)));
        builder.Set("idempotency_key", CanonicalValue.Text(row.GetString(14)));
        builder.Set("currency", CanonicalValue.Token(row.GetString(15)));

        builder.SetGroup("lines", entryLines.Select(static line => new Action<CanonicalItemBuilder>(item =>
        {
            item.Set("line_no", CanonicalValue.Integer(line.LineNo));
            item.Set("account_code", CanonicalValue.Text(line.AccountCode));
            item.Set("debit", CanonicalValue.Amount(line.Debit));
            item.Set("credit", CanonicalValue.Amount(line.Credit));
            item.Set("cost_center", CanonicalValue.TextOrNull(line.CostCenterId));
            item.Set("description", CanonicalValue.Text(line.Description));
        })));

        return builder.Build();
    }
}
