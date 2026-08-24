using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Audit;
using Babel.SharedKernel;

namespace Babel.Api.Wire;

/// <summary>
/// التحويل بين شكل السلك وعقد الترحيل.
/// <para>
/// <b>وهذا هو كل ما يفعله هذا الملف: نقل.</b> لا يختار دوراً، ولا يختار حدثاً، ولا يحسب
/// مبلغاً، ولا يجمع سطراً إلى سطر. كل قيمة تخرج من هنا وصلت من العميل أو هي افتراضٌ
/// <b>معلن في العقد المنشور</b> (‏<c>book = MAIN</c>، <c>currency = SAR</c>،
/// <c>exchangeRate = 1</c>، <c>generation = 1</c>).
/// </para>
/// <para>
/// ولاحظ كيف تُقرأ التعدادات: <c>Enum.TryParse</c> بأسماء <b>حسّاسة لحالة الأحرف</b>
/// (‏<c>ignoreCase: false</c>). وهذا ليس تشدّداً أسلوبياً: المطابقة غير الحسّاسة تفتح
/// باب <c>tr-TR</c> — حيث <c>I</c> الكبيرة تُصغَّر إلى <c>ı</c> بلا نقطة — وأي مطابقة
/// تمرّ على تحويل حالة أحرف واعٍ باللغة تصير حجّة لغوية في مسار محاسبي. الاسم يُطابَق
/// حرفياً أو يُرفض (‏فخ-39).
/// </para>
/// </summary>
internal static class WireMapping
{
    /// <summary>أقصى عدد سطور في طلب واحد — حدٌّ معلن يمنع طلباً يُرهق المحرّك قبل أن يُرفض.</summary>
    public const int MaxLines = 500;

    /// <summary>يحوّل طلب الترحيل الوارد إلى عقد الترحيل.</summary>
    /// <param name="dto">الطلب كما وصل.</param>
    /// <param name="companyId">الشركة من المسار — بعد التحقق من أن الاعتماد يبلغها.</param>
    /// <param name="actor">الفاعل من الاعتماد، لا من الجسم.</param>
    public static PostingRequest ToPostingRequest(PostJournalEntryRequestDto dto, Guid companyId, UserId actor)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Lines.Count > MaxLines)
        {
            throw WireNumbers.Reject(
                "wire.lines.too_many",
                "lines",
                FormattableString.Invariant($"عدد السطور يتجاوز الحدّ المعلن {MaxLines}."),
                FormattableString.Invariant($"The number of lines exceeds the published limit of {MaxLines}."));
        }

        CurrencyCode currency = ReadCurrency(dto.Currency, "currency");

        return new PostingRequest
        {
            Tenant = new TenantId(companyId),
            IdempotencyKey = ReadIdempotencyKey(dto.IdempotencyKey),
            Source = new SourceDocument(
                ReadEnum<BabelModule>(dto.Source.Module, "source.module"),
                ReadRequiredText(dto.Source.DocumentType, "source.documentType", 64),
                ReadRequiredText(dto.Source.DocumentId, "source.documentId", 128)),
            Trigger = ReadEnum<PostingTrigger>(dto.Trigger, "trigger"),
            DocumentDate = ReadDate(dto.DocumentDate, "documentDate"),
            Narration = ReadLocalized(dto.Narration, "narration"),
            Lines = [.. dto.Lines.Select((line, i) => ToPostingLine(line, currency, i))],
            Event = string.IsNullOrEmpty(dto.Event)
                ? PostingEventCode.None
                : new PostingEventCode(ReadRequiredText(dto.Event, "event", 128)),
            Amounts = [.. dto.Amounts.Select((a, i) => new PostingAmount(
                ReadRequiredText(a.Name, FormattableString.Invariant($"amounts[{i}].name"), 64),
                Money.Of(
                    WireNumbers.ParseStrict(a.Value.Raw, WireNumbers.MoneyScale, FormattableString.Invariant($"amounts[{i}].value")),
                    currency)))],
            Facts = [.. dto.Facts.Select((f, i) => new PostingFact(
                ReadRequiredText(f.Name, FormattableString.Invariant($"facts[{i}].name"), 128),
                ReadText(f.Value, FormattableString.Invariant($"facts[{i}].value"), 256)))],
            Dimensions = [.. dto.Dimensions.Select((d, i) => new PostingDimension(
                ReadRequiredText(d.Name, FormattableString.Invariant($"dimensions[{i}].name"), 64),
                ReadText(d.Value, FormattableString.Invariant($"dimensions[{i}].value"), 128)))],
            Book = ReadRequiredText(dto.Book, "book", 32),
            Currency = currency,
            ExchangeRate = WireNumbers.ParseStrict(dto.ExchangeRate.Raw, WireNumbers.RateScale, "exchangeRate"),
            Generation = dto.Generation,
            Actor = actor,
            ClosedPeriodAuthorisation = ToAuthorisation(dto.ClosedPeriodAuthorisation),
        };
    }

    /// <summary>يحوّل طلب العكس الوارد إلى عقد العكس.</summary>
    /// <param name="dto">الطلب كما وصل.</param>
    /// <param name="companyId">الشركة من المسار.</param>
    /// <param name="entryId">القيد المراد عكسه، من المسار.</param>
    /// <param name="actor">الفاعل من الاعتماد.</param>
    public static ReversalRequest ToReversalRequest(
        ReverseJournalEntryRequestDto dto,
        Guid companyId,
        Guid entryId,
        UserId actor)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ReversalRequest
        {
            Tenant = new TenantId(companyId),
            EntryId = entryId,
            Reason = ReadLocalized(dto.Reason, "reason"),
            Actor = actor,
            ReversalDate = dto.ReversalDate is null ? null : ReadDate(dto.ReversalDate, "reversalDate"),
            ClosedPeriodAuthorisation = ToAuthorisation(dto.ClosedPeriodAuthorisation),
        };
    }

    /// <summary>يحوّل الإيصال إلى شكله على السلك.</summary>
    /// <param name="receipt">الإيصال.</param>
    public static PostingReceiptDto ToDto(PostingReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return new PostingReceiptDto(
            receipt.JournalEntryId.ToString("D", CultureInfo.InvariantCulture),
            WireNumbers.FormatInt64(receipt.EntryNumber),
            receipt.EntryHash,
            receipt.WasAlreadyPosted,
            WireNumbers.FormatInt64(receipt.ChainSequence),
            receipt.PeriodCode,
            receipt.Generation,
            receipt.LineCount);
    }

    /// <summary>يحوّل حكم السلسلة إلى شكله على السلك.</summary>
    /// <param name="report">الحكم.</param>
    public static ChainVerificationDto ToDto(LedgerChainReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ChainVerificationDto(
            report.Ok,
            report.Checked,
            report.FirstDivergentSequence is { } sequence ? WireNumbers.FormatInt64(sequence) : null,
            report.Verdict,
            report.ReasonAr,
            report.Detail);
    }

    /// <summary>
    /// يحوّل ميزان المراجعة إلى شكله على السلك — <b>بمجموعيه كما وصلا من الدفتر</b>.
    /// </summary>
    /// <param name="book">الدفتر.</param>
    /// <param name="periodCode">الفترة، أو <c>null</c>.</param>
    /// <param name="report">الميزان كما جاء من الدفتر: صفوفه ومجموعاه وحكم توازنه.</param>
    /// <remarks>
    /// <b>ولا حساب واحد هنا:</b> المجموعان محسوبان بـ<c>sum()</c> على <c>numeric</c> داخل
    /// PostgreSQL، وحكم التوازن محسوم في الدفتر. وهذا السطر <b>ينسّق</b> عشرياً إلى نصّ
    /// ولا يجمع ولا يقارن — والقاعدة 13 تفحص ذلك في IL لا في مراجعة.
    /// </remarks>
    public static TrialBalanceDto ToDto(
        string book,
        string? periodCode,
        (IReadOnlyList<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit, bool Balanced) report)
    {
        ArgumentNullException.ThrowIfNull(report.Rows);

        return new TrialBalanceDto(
            report.Balanced,
            book,
            periodCode,
            report.Rows.Count,
            [.. report.Rows.Select(static row => new TrialBalanceRowDto(
                row.AccountCode,
                row.NameAr,
                row.NameEn,
                new WireDecimal(WireNumbers.FormatMoney(row.Debit)),
                new WireDecimal(WireNumbers.FormatMoney(row.Credit))))],
            new WireDecimal(WireNumbers.FormatMoney(report.TotalCredit)),
            new WireDecimal(WireNumbers.FormatMoney(report.TotalDebit)));
    }

    private static PostingLine ToPostingLine(PostingLineDto dto, CurrencyCode currency, int index)
    {
        string prefix = FormattableString.Invariant($"lines[{index}]");

        return new PostingLine
        {
            Role = ReadEnum<PostingRole>(dto.Role, prefix + ".role"),
            Side = ReadEnum<PostingSide>(dto.Side, prefix + ".side"),
            Amount = Money.Of(
                WireNumbers.ParseStrict(dto.Amount.Raw, WireNumbers.MoneyScale, prefix + ".amount"),
                currency),
            Scope = dto.Scope is null
                ? PostingScope.None
                : new PostingScope(
                    ReadOptional(dto.Scope.BranchId, prefix + ".scope.branchId", 64),
                    ReadOptional(dto.Scope.CostCenterId, prefix + ".scope.costCenterId", 64),
                    ReadOptional(dto.Scope.ProjectId, prefix + ".scope.projectId", 64)),
            Subledger = dto.Subledger is null
                ? SubledgerReference.None
                : new SubledgerReference(
                    ReadEnum<SubledgerKind>(dto.Subledger.Kind, prefix + ".subledger.kind"),
                    ReadRequiredText(dto.Subledger.PartyId, prefix + ".subledger.partyId", 128)),
            Narration = dto.Narration is null ? null : ReadLocalized(dto.Narration, prefix + ".narration"),
            Qualifier = ReadText(dto.Qualifier ?? string.Empty, prefix + ".qualifier", 64),
            Dimensions = dto.Dimensions is null
                ? []
                : [.. dto.Dimensions.Select((d, i) => new PostingDimension(
                    ReadRequiredText(d.Name, FormattableString.Invariant($"{prefix}.dimensions[{i}].name"), 64),
                    ReadText(d.Value, FormattableString.Invariant($"{prefix}.dimensions[{i}].value"), 128)))],
        };
    }

    private static ClosedPeriodAuthorisation? ToAuthorisation(ClosedPeriodAuthorisationDto? dto) => dto is null
        ? null
        : new ClosedPeriodAuthorisation(
            ReadRequiredText(dto.PermissionCode, "closedPeriodAuthorisation.permissionCode", 64),
            new UserId(ReadGuid(dto.AuthorisedBy, "closedPeriodAuthorisation.authorisedBy")),
            ReadLocalized(dto.Reason, "closedPeriodAuthorisation.reason"));

    /// <summary>
    /// يقرأ عضو تعداد <b>بالاسم الحرفي</b>. لا مطابقة بحالة أحرف متساهلة، ولا رقم يُقبل
    /// مكان الاسم — الرقم يجعل إعادة ترتيب التعداد تغييراً صامتاً في معنى كل طلب محفوظ.
    /// </summary>
    private static TEnum ReadEnum<TEnum>(string? value, string field)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
        {
            throw WireNumbers.Reject("wire.enum.missing", field, "قيمة مفقودة.", "The value is missing.");
        }

        // رقم في موضع اسم: مرفوض صراحةً. Enum.TryParse يقبل «13» ويعيده عضواً.
        if (value.Length > 0 && (char.IsAsciiDigit(value[0]) || value[0] is '-' or '+'))
        {
            throw WireNumbers.Reject(
                "wire.enum.numeric_not_accepted",
                field,
                "القيمة رقم، والمقبول اسم العضو حرفياً: الرقم يجعل إعادة ترتيب التعداد تغييراً صامتاً في معنى كل طلب محفوظ.",
                "The value is numeric; a literal member name is required. Numbers make reordering the enum a silent change to the meaning of every stored request.");
        }

        if (!Enum.TryParse(value, ignoreCase: false, out TEnum parsed) || !Enum.IsDefined(parsed))
        {
            throw WireNumbers.Reject(
                "wire.enum.unknown_member",
                field,
                $"القيمة «{value}» ليست عضواً معرَّفاً. المقبول حرفياً: {string.Join(" · ", Enum.GetNames<TEnum>())}.",
                $"The value '{value}' is not a defined member. Accepted literally: {string.Join(" | ", Enum.GetNames<TEnum>())}.");
        }

        return parsed;
    }

    private static IdempotencyKey ReadIdempotencyKey(string? value)
    {
        string text = ReadRequiredText(value, "idempotencyKey", 128);

        foreach (char c in text)
        {
            bool allowed = c is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '-' or '_' or ':' or '.';
            if (!allowed)
            {
                throw WireNumbers.Reject(
                    "wire.idempotency_key.charset",
                    "idempotencyKey",
                    "مفتاح الحصانة محارف ASCII آمنة فقط [0-9A-Za-z-_:.] — فهو يدخل مفتاحاً أساسياً ويُجزَّأ.",
                    "The idempotency key accepts safe ASCII only [0-9A-Za-z-_:.]; it becomes part of a primary key and is hashed.");
            }
        }

        return new IdempotencyKey(text);
    }

    private static CurrencyCode ReadCurrency(string? value, string field)
    {
        string text = ReadRequiredText(value, field, 3);

        if (text.Length != 3 || text.Any(static c => c is < 'A' or > 'Z'))
        {
            throw WireNumbers.Reject(
                "wire.currency.malformed",
                field,
                "رمز العملة ثلاثة محارف ISO 4217 لاتينية كبيرة بالضبط. والمحارف اللاتينية شرط سلامة سلسلة التجزئة، لا تفضيل عرض.",
                "A currency code is exactly three upper-case ASCII ISO 4217 letters. ASCII here is a hash-chain safety requirement, not a display preference.");
        }

        return CurrencyCode.FromString(text);
    }

    /// <summary>
    /// يقرأ تاريخاً ميلادياً بصيغة <c>yyyy-MM-dd</c> <b>بثقافة ثابتة</b>.
    /// <para>
    /// وهذا هو الموضع الذي يفصل بين خادم سليم وخادم يكتب الفترة <c>1448-03</c>: تحت
    /// <c>ar-SA</c> يكون التقويم الافتراضي أم القرى، فأي تحليل أو تنسيق تاريخ لا يمرّر
    /// <c>InvariantCulture</c> صراحةً يقرأ ويكتب بالهجري بلا استثناء ولا سطر سجل (فخ-38).
    /// </para>
    /// </summary>
    private static DateOnly ReadDate(string? value, string field)
    {
        string text = ReadRequiredText(value, field, 10);

        foreach (char c in text)
        {
            if (WireNumbers.IsNonLatinDigit(c))
            {
                throw WireNumbers.Reject(
                    "wire.date.non_latin_digits",
                    field,
                    "الأرقام غير اللاتينية مرفوضة في التاريخ رفضاً صريحاً.",
                    "Non-Latin digits are explicitly refused in a date.");
            }
        }

        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            throw WireNumbers.Reject(
                "wire.date.malformed",
                field,
                "التاريخ ميلادي بصيغة yyyy-MM-dd حصراً. صيغة أخرى، أو تقويم آخر، تُقرأ فترة مالية مختلفة.",
                "The date is Gregorian in yyyy-MM-dd only. Any other spelling or calendar reads as a different fiscal period.");
        }

        return date;
    }

    private static Guid ReadGuid(string? value, string field)
    {
        string text = ReadRequiredText(value, field, 36);

        if (!Guid.TryParseExact(text, "D", out Guid parsed))
        {
            throw WireNumbers.Reject(
                "wire.guid.malformed",
                field,
                "المعرّف بصيغة 8-4-4-4-12 بأحرف صغيرة أو كبيرة، بلا أقواس.",
                "The identifier must be in 8-4-4-4-12 form, without braces.");
        }

        return parsed;
    }

    private static LocalizedName ReadLocalized(LocalizedTextDto? dto, string field)
    {
        if (dto is null)
        {
            throw WireNumbers.Reject("wire.text.missing", field, "قيمة مفقودة.", "The value is missing.");
        }

        return new LocalizedName(
            ReadRequiredText(dto.Ar, field + ".ar", 512),
            ReadRequiredText(dto.En, field + ".en", 512));
    }

    private static string ReadRequiredText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw WireNumbers.Reject("wire.text.missing", field, "قيمة نصّية مفقودة أو فارغة.", "A required text value is missing or blank.");
        }

        return ReadText(value, field, maxLength);
    }

    private static string ReadText(string value, string field, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw WireNumbers.Reject(
                "wire.text.too_long",
                field,
                FormattableString.Invariant($"النصّ أطول من الحدّ المعلن {maxLength} محرفاً."),
                FormattableString.Invariant($"The text is longer than the published limit of {maxLength} characters."));
        }

        // المحارف الاتجاهية غير المرئية تُرفض **عند الحدّ** لا تُزال عند التجزئة: إزالتها
        // لاحقاً تعني أن ما وُقّع غير ما أُدخل، وأن نصّين مختلفين بصرياً يحملان البصمة
        // نفسها (فخ-23).
        foreach (char c in value)
        {
            if (IsBidiControl(c))
            {
                throw WireNumbers.Reject(
                    "wire.text.bidi_control",
                    field,
                    "النصّ يحمل محرف تحكّم اتجاهي غير مرئي. يُرفض عند الحدّ ولا يُنظَّف بعده: ما يُوقَّع يجب أن يكون ما أُدخل.",
                    "The text carries an invisible bidirectional control character. It is refused at the boundary and never scrubbed afterwards: what is signed must be what was entered.");
            }
        }

        return value;
    }

    private static string? ReadOptional(string? value, string field, int maxLength) =>
        string.IsNullOrEmpty(value) ? null : ReadText(value, field, maxLength);

    /// <summary>محارف التحكّم الاتجاهية — مكتوبة بهروبها لا بذاتها، فهي غير مرئية في المحرّر.</summary>
    private static bool IsBidiControl(char c) =>
        c is '\u200E' or '\u200F' or '\u061C'
            or (>= '\u202A' and <= '\u202E')
            or (>= '\u2066' and <= '\u2069');
}
