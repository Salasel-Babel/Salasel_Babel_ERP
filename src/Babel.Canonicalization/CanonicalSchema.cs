using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Babel.Canonicalization;

/// <summary>سبب استثناء حقل من التجزئة. جزء من المواصفة المُصدَّرة.</summary>
public enum ExclusionReason
{
    /// <summary>بصمة السجل نفسه — لا يمكن أن تجزّئ نفسها.</summary>
    SelfHash,
    /// <summary>قيمة مشتقّة من إسقاط: أرصدة، مجاميع، عدّادات محسوبة.</summary>
    ProjectionDerived,
    /// <summary>بيانات تشغيلية متغيّرة: حالة المزامنة، عدد المحاولات، طوابع الصف.</summary>
    OperationalMetadata,
    /// <summary>عمود مطبَّع للبحث — مشتقّ من عمود مُوقَّع، ويتغيّر مع تغيّر قواعد البحث.</summary>
    SearchNormalised,
    /// <summary>قياس عن بُعد: عنوان IP، وكيل المستخدم، معرّف الجلسة.</summary>
    Telemetry,
    /// <summary>عرض فقط: تنسيق، ألوان، ترتيب العرض، صور مصغّرة.</summary>
    Presentation
}

/// <summary>حقل مستثنى صراحةً من التوحيد القياسي، مع سببه.</summary>
public sealed record ExcludedField(string Name, ExclusionReason Reason, string RationaleAr);

/// <summary>تعريف حقل في المخطّط. مجموعة تعني حقلاً مكرّراً (سطور).</summary>
public sealed record SchemaField(
    string Name,
    CanonicalKind Kind,
    bool Required = true,
    IReadOnlyList<SchemaField>? GroupFields = null)
{
    public bool IsGroup => Kind == CanonicalKind.Group;
}

/// <summary>
/// <b>ترتيب الحقول مواصفة، لا سلوك.</b>
///
/// لا انعكاس (reflection)، ولا ترتيب قاموس، ولا سلوك مُسلسِل JSON: قائمة مرتّبة
/// مكتوبة يدوياً، والمُوحِّد يكتب <b>بترتيب المخطّط</b> مهما كان ترتيب استدعاءات
/// <c>Set()</c>. إعادة ترتيب خصائص صنف C#، أو ترقية مكتبة تسلسل، لا تُغيّر بايتة واحدة.
///
/// ويحمل المخطّط أيضاً <b>مجموعة الاستثناء</b> صراحةً: الحقول التي قُرِّر عمداً
/// ألّا تُجزَّأ، مع سبب كل واحد. مجموعات الاستثناء الضمنية هي بالضبط ما تسقط
/// عنده تنفيذات XMLDSig؛ فهنا هي معلنة ومُختبَرة ومُبصَّمة.
/// </summary>
public sealed class CanonicalSchema
{
    /// <summary>نوع المستند، مثال: <c>babel.journal.entry</c>.</summary>
    public string Kind { get; }

    /// <summary>إصدار الشكل القانوني الذي كُتب هذا المخطّط له.</summary>
    public string CanonVersion { get; }

    /// <summary>الحقول بترتيبها القانوني.</summary>
    public IReadOnlyList<SchemaField> Fields { get; }

    /// <summary>مجموعة الاستثناء المُعلنة.</summary>
    public IReadOnlyList<ExcludedField> Exclusions { get; }

    /// <summary>
    /// بصمة SHA-256 على إعلان المخطّط نفسه (الأسماء والأنواع والترتيب والاستثناءات).
    /// اختبار ذهبي يثبّت هذه القيمة، فأي تعديل على المخطّط يُسقط البناء فوراً بدلاً
    /// من أن يُكتشف بعد مليون قيد.
    /// </summary>
    public string Fingerprint { get; }

    private readonly Dictionary<string, SchemaField> _byName;

    public CanonicalSchema(
        string kind,
        string canonVersion,
        IReadOnlyList<SchemaField> fields,
        IReadOnlyList<ExcludedField> exclusions)
    {
        Kind = RequireName(kind, allowDots: true);
        CanonVersion = canonVersion;
        Fields = new ReadOnlyCollection<SchemaField>([.. fields]);
        Exclusions = new ReadOnlyCollection<ExcludedField>([.. exclusions]);

        _byName = new Dictionary<string, SchemaField>(StringComparer.Ordinal);
        foreach (var f in Fields)
        {
            RequireName(f.Name, allowDots: false);
            if (!_byName.TryAdd(f.Name, f))
                throw new CanonicalizationException(CanonErrors.SchemaDuplicateField,
                    $"الحقل «{f.Name}» مكرّر في المخطّط.", -1, f.Name);

            if (f.IsGroup)
            {
                if (f.GroupFields is null || f.GroupFields.Count == 0)
                    throw new CanonicalizationException(CanonErrors.SchemaMissingField,
                        $"المجموعة «{f.Name}» بلا حقول.", -1, f.Name);
                var inner = new HashSet<string>(StringComparer.Ordinal);
                foreach (var g in f.GroupFields)
                {
                    RequireName(g.Name, allowDots: false);
                    if (g.IsGroup)
                        throw new CanonicalizationException(CanonErrors.SchemaNestedGroup,
                            $"المجموعة «{f.Name}» تحتوي مجموعة «{g.Name}». " +
                            "v1 يمنع التداخل: مستوى واحد من التكرار فقط، حتى يبقى الشكل السلكي بسيطاً ومُبرهناً.",
                            -1, g.Name);
                    if (!inner.Add(g.Name))
                        throw new CanonicalizationException(CanonErrors.SchemaDuplicateField,
                            $"الحقل «{g.Name}» مكرّر داخل المجموعة «{f.Name}».", -1, g.Name);
                }
            }
        }

        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in Exclusions)
        {
            if (_byName.ContainsKey(e.Name))
                throw new CanonicalizationException(CanonErrors.SchemaExcludedField,
                    $"الحقل «{e.Name}» مُدرج ومستثنى في آن واحد.", -1, e.Name);
            if (!excluded.Add(e.Name))
                throw new CanonicalizationException(CanonErrors.SchemaDuplicateField,
                    $"الاستثناء «{e.Name}» مكرّر.", -1, e.Name);
        }

        Fingerprint = ComputeFingerprint();
    }

    internal bool TryGet(string name, out SchemaField field) => _byName.TryGetValue(name, out field!);

    /// <summary>هل هذا الاسم مستثنى صراحةً؟</summary>
    public bool IsExcluded(string name) => Exclusions.Any(e => e.Name == name);

    /// <summary>يبدأ بناء مستند وفق هذا المخطّط.</summary>
    public CanonicalDocumentBuilder NewDocument() => new(this);

    private string ComputeFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append("babel.canon.schema/").Append(CanonVersion).Append('\n');
        sb.Append("kind=").Append(Kind).Append('\n');
        foreach (var f in Fields)
        {
            sb.Append("field=").Append(f.Name).Append('|').Append(f.Kind).Append('|').Append(f.Required).Append('\n');
            if (f.GroupFields is null) continue;
            foreach (var g in f.GroupFields)
                sb.Append("  item=").Append(g.Name).Append('|').Append(g.Kind).Append('|').Append(g.Required).Append('\n');
        }
        foreach (var e in Exclusions)
            sb.Append("excluded=").Append(e.Name).Append('|').Append(e.Reason).Append('\n');
        sb.Append("end\n");
        return Convert.ToHexString(
            SHA256.HashData(new UTF8Encoding(false).GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    internal static string RequireName(string name, bool allowDots)
    {
        ArgumentNullException.ThrowIfNull(name);
        var ok = name.Length is > 0 and <= 64 && name[0] is >= 'a' and <= 'z';
        if (ok)
            foreach (var ch in name)
                if (!(ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch == '_' || (allowDots && ch == '.')))
                { ok = false; break; }

        if (!ok)
            throw new CanonicalizationException(CanonErrors.FieldNameInvalid,
                $"اسم غير صالح «{name}». المسموح: [a-z][a-z0-9_]{{0,63}}" + (allowDots ? " مع النقطة." : "."),
                -1, name);
        return name;
    }

    /// <summary>عرض المواصفة نصّياً — يُستخدم في التوثيق وفي مقارنة الانحراف.</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"schema {Kind} ({CanonVersion})  fingerprint={Fingerprint}\n");
        foreach (var f in Fields)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  {f.Name,-24} {f.Kind,-8} {(f.Required ? "required" : "optional")}\n");
            if (f.GroupFields is null) continue;
            foreach (var g in f.GroupFields)
                sb.Append(CultureInfo.InvariantCulture, $"      {g.Name,-20} {g.Kind,-8} {(g.Required ? "required" : "optional")}\n");
        }
        sb.Append("  -- مجموعة الاستثناء --\n");
        foreach (var e in Exclusions)
            sb.Append(CultureInfo.InvariantCulture, $"  ! {e.Name,-24} {e.Reason,-20} {e.RationaleAr}\n");
        return sb.ToString();
    }
}
