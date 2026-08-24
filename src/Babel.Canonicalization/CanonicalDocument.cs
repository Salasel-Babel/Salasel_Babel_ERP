using System.Collections.ObjectModel;

namespace Babel.Canonicalization;

/// <summary>عنصر داخل مجموعة مكرّرة (سطر قيد مثلاً). حقول قياسية فقط في v1.</summary>
public sealed record CanonicalItem(IReadOnlyList<KeyValuePair<string, CanonicalValue>> Fields);

/// <summary>حقل مكتوب في المستند: قيمة قياسية أو مجموعة عناصر.</summary>
public sealed record CanonicalEntry(string Name, CanonicalValue? Value, IReadOnlyList<CanonicalItem>? Items)
{
    public bool IsGroup => Items is not null;
}

/// <summary>
/// موقع السجل في السلسلة. <b>يدخل البايتات المُجزَّأة</b>، ولا يعيش بجوارها.
///
/// لو عاش الرابط في عمود مجاور فقط، لصارت السلسلة زينة: مهاجم يعيد كتابة ذلك
/// العمود، وتبقى كل بصمة فردية صحيحة عند التحقق منها وحدها. هذا بالضبط ما تفعله
/// ZATCA بحمل <c>ICV</c> و<c>PIH</c> <b>داخل</b> جسم الفاتورة المُوقَّع.
/// </summary>
public readonly record struct ChainPosition
{
    /// <summary>رقم التسلسل داخل نطاق السلسلة. يبدأ من 1.</summary>
    public long Sequence { get; }

    /// <summary>بصمة السجل السابق، 32 بايت بالضبط. للسجل الأول: بصمة التكوين.</summary>
    public byte[] PreviousHash { get; }

    public ChainPosition(long sequence, byte[] previousHash)
    {
        ArgumentNullException.ThrowIfNull(previousHash);
        if (sequence < 1)
            throw new CanonicalizationException(CanonErrors.ChainBadSequence,
                $"رقم التسلسل {sequence} غير صالح: السلسلة تبدأ من 1، والفجوات تُثبَت إيجاباً لا تُستنتج.");
        if (previousHash.Length != 32)
            throw new CanonicalizationException(CanonErrors.ChainBadPreviousHash,
                $"البصمة السابقة طولها {previousHash.Length} بايت، والمطلوب 32 بالضبط (SHA-256). " +
                "للسجل الأول استخدم Canonicalizer.Genesis(scope).");

        Sequence = sequence;
        PreviousHash = (byte[])previousHash.Clone();
    }

    public bool Equals(ChainPosition other)
        => Sequence == other.Sequence && PreviousHash.AsSpan().SequenceEqual(other.PreviousHash);

    public override int GetHashCode() => HashCode.Combine(Sequence, Convert.ToHexString(PreviousHash));
}

/// <summary>
/// مستند جاهز للتوحيد القياسي: مخطّط مُثبَّت، حقول بترتيب المخطّط، وموقع سلسلة.
///
/// <b>لا يمكن الحصول على بايتات من مستند غير مرتبط بالسلسلة.</b>
/// <see cref="Canonicalizer.Canonicalize"/> يرمي إن كان <see cref="Chain"/> فارغاً.
/// هذا يجعل «نسيان» إدخال رقم التسلسل والبصمة السابقة في البايتات <b>مستحيلاً</b>،
/// لا مجرّد «مخالفاً للتعليمات».
/// </summary>
public sealed record CanonicalDocument
{
    /// <summary>المخطّط الذي بُني عليه المستند.</summary>
    public CanonicalSchema Schema { get; }

    /// <summary>الحقول بترتيب المخطّط.</summary>
    public IReadOnlyList<CanonicalEntry> Entries { get; }

    /// <summary>موقع السلسلة، أو <c>null</c> قبل الربط.</summary>
    public ChainPosition? Chain { get; init; }

    /// <summary>نوع المستند.</summary>
    public string Kind => Schema.Kind;

    /// <summary>إصدار الشكل القانوني.</summary>
    public string CanonVersion => Schema.CanonVersion;

    internal CanonicalDocument(CanonicalSchema schema, IReadOnlyList<CanonicalEntry> entries, ChainPosition? chain)
    {
        Schema = schema;
        Entries = entries;
        Chain = chain;
    }

    /// <summary>يربط المستند بموقع في السلسلة. يعيد نسخة جديدة؛ المستندات غير قابلة للتعديل.</summary>
    public CanonicalDocument Bind(long sequence, byte[] previousHash)
        => this with { Chain = new ChainPosition(sequence, previousHash) };

    /// <summary>يفكّ الربط — يُستخدم في المتحقّق عند إعادة الربط برقم مخزَّن.</summary>
    public CanonicalDocument Unbind() => this with { Chain = null };
}

/// <summary>
/// بانٍ مقيَّد بمخطّط. يقبل الحقول بأي ترتيب استدعاء، ويُخرجها <b>بترتيب المخطّط دائماً</b>.
/// يرفض: حقلاً غير معروف، حقلاً مكرّراً، حقلاً مطلوباً ناقصاً، ونوعاً مخالفاً.
/// </summary>
public sealed class CanonicalDocumentBuilder
{
    /// <summary>سقف عدد عناصر المجموعة الواحدة.</summary>
    public const int MaxGroupItems = 100_000;

    private readonly CanonicalSchema _schema;
    private readonly Dictionary<string, CanonicalValue> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<CanonicalItem>> _groups = new(StringComparer.Ordinal);

    internal CanonicalDocumentBuilder(CanonicalSchema schema) => _schema = schema;

    /// <summary>يضبط قيمة حقل قياسي.</summary>
    public CanonicalDocumentBuilder Set(string name, CanonicalValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var field = ResolveScalar(name);
        RequireKind(field, value, name);
        if (!_values.TryAdd(name, value))
            throw new CanonicalizationException(CanonErrors.SchemaDuplicateField,
                $"الحقل «{name}» ضُبط مرّتين.", -1, name);
        return this;
    }

    /// <summary>يضبط مجموعة مكرّرة. <b>الترتيب المُمرَّر هو الترتيب المُجزَّأ</b> — رتّبها أنت صراحةً.</summary>
    public CanonicalDocumentBuilder SetGroup(string name, IEnumerable<Action<CanonicalItemBuilder>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var field = ResolveGroup(name);
        if (_groups.ContainsKey(name))
            throw new CanonicalizationException(CanonErrors.SchemaDuplicateField,
                $"المجموعة «{name}» ضُبطت مرّتين.", -1, name);

        var list = new List<CanonicalItem>();
        foreach (var configure in items)
        {
            var ib = new CanonicalItemBuilder(field, name);
            configure(ib);
            list.Add(ib.Build());
            if (list.Count > MaxGroupItems)
                throw new CanonicalizationException(CanonErrors.DocumentTooManyItems,
                    $"المجموعة «{name}» تجاوزت {MaxGroupItems} عنصراً.", -1, name);
        }

        _groups[name] = list;
        return this;
    }

    /// <summary>يبني المستند غير المرتبط. يُربط بعدها عبر <see cref="Canonicalizer.Compute"/>.</summary>
    public CanonicalDocument Build()
    {
        var entries = new List<CanonicalEntry>(_schema.Fields.Count);

        foreach (var f in _schema.Fields)
        {
            if (f.IsGroup)
            {
                if (_groups.TryGetValue(f.Name, out var items))
                {
                    entries.Add(new CanonicalEntry(f.Name, null, new ReadOnlyCollection<CanonicalItem>(items)));
                }
                else if (f.Required)
                {
                    throw new CanonicalizationException(CanonErrors.SchemaMissingField,
                        $"المجموعة المطلوبة «{f.Name}» غير مضبوطة. " +
                        "مجموعة فارغة تُضبط صراحةً بقائمة فارغة — الغياب لا يُستنتج.", -1, f.Name);
                }
                else
                {
                    entries.Add(new CanonicalEntry(f.Name, null, ReadOnlyCollection<CanonicalItem>.Empty));
                }
                continue;
            }

            if (_values.TryGetValue(f.Name, out var v))
            {
                entries.Add(new CanonicalEntry(f.Name, v, null));
            }
            else if (f.Required)
            {
                throw new CanonicalizationException(CanonErrors.SchemaMissingField,
                    $"الحقل المطلوب «{f.Name}» غير مضبوط. لا قيم افتراضية ضمنية في الشكل القانوني.",
                    -1, f.Name);
            }
            else
            {
                entries.Add(new CanonicalEntry(f.Name, CanonicalValue.Null(), null));
            }
        }

        return new CanonicalDocument(_schema, new ReadOnlyCollection<CanonicalEntry>(entries), null);
    }

    private SchemaField ResolveScalar(string name)
    {
        if (_schema.IsExcluded(name))
            throw new CanonicalizationException(CanonErrors.SchemaExcludedField,
                $"الحقل «{name}» مستثنى صراحةً من التجزئة في هذا المخطّط. " +
                "إدراجه يغيّر كل بصمة ويُبطل كل تحقّق سابق. إن كان يجب أن يُجزَّأ، فذلك v2 لا تعديل v1.",
                -1, name);
        if (!_schema.TryGet(name, out var f))
            throw new CanonicalizationException(CanonErrors.SchemaUnknownField,
                $"الحقل «{name}» غير معرّف في المخطّط {_schema.Kind}.", -1, name);
        if (f.IsGroup)
            throw new CanonicalizationException(CanonErrors.SchemaUnknownField,
                $"«{name}» مجموعة، استخدم SetGroup.", -1, name);
        return f;
    }

    private SchemaField ResolveGroup(string name)
    {
        if (!_schema.TryGet(name, out var f) || !f.IsGroup)
            throw new CanonicalizationException(CanonErrors.SchemaUnknownField,
                $"المجموعة «{name}» غير معرّفة في المخطّط {_schema.Kind}.", -1, name);
        return f;
    }

    internal static void RequireKind(SchemaField field, CanonicalValue value, string path)
    {
        if (value.Kind == CanonicalKind.Null)
        {
            if (field.Required)
                throw new CanonicalizationException(CanonErrors.SchemaMissingField,
                    $"الحقل «{path}» مطلوب ولا يقبل الغياب.", -1, path);
            return;
        }
        if (value.Kind != field.Kind)
            throw new CanonicalizationException(CanonErrors.SchemaUnknownField,
                $"الحقل «{path}» نوعه {field.Kind} في المخطّط، ومُرِّرت قيمة {value.Kind}.", -1, path);
    }
}

/// <summary>بانٍ لعنصر داخل مجموعة. نفس قواعد المخطّط والترتيب.</summary>
public sealed class CanonicalItemBuilder
{
    private readonly SchemaField _group;
    private readonly string _groupName;
    private readonly Dictionary<string, CanonicalValue> _values = new(StringComparer.Ordinal);

    internal CanonicalItemBuilder(SchemaField group, string groupName)
    {
        _group = group;
        _groupName = groupName;
    }

    /// <summary>يضبط قيمة حقل داخل العنصر.</summary>
    public CanonicalItemBuilder Set(string name, CanonicalValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var f = _group.GroupFields!.FirstOrDefault(x => x.Name == name)
                ?? throw new CanonicalizationException(CanonErrors.SchemaUnknownField,
                    $"الحقل «{name}» غير معرّف داخل المجموعة «{_groupName}».", -1, name);
        CanonicalDocumentBuilder.RequireKind(f, value, $"{_groupName}/{name}");
        if (!_values.TryAdd(name, value))
            throw new CanonicalizationException(CanonErrors.SchemaDuplicateField,
                $"الحقل «{name}» ضُبط مرّتين داخل «{_groupName}».", -1, name);
        return this;
    }

    internal CanonicalItem Build()
    {
        var list = new List<KeyValuePair<string, CanonicalValue>>(_group.GroupFields!.Count);
        foreach (var f in _group.GroupFields!)
        {
            if (_values.TryGetValue(f.Name, out var v)) list.Add(new(f.Name, v));
            else if (f.Required)
                throw new CanonicalizationException(CanonErrors.SchemaMissingField,
                    $"الحقل المطلوب «{f.Name}» غير مضبوط داخل «{_groupName}».", -1, f.Name);
            else list.Add(new(f.Name, CanonicalValue.Null()));
        }
        return new CanonicalItem(new ReadOnlyCollection<KeyValuePair<string, CanonicalValue>>(list));
    }
}
