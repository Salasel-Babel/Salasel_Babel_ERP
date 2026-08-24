# Babel.Canonicalization

مكتبة التوحيد القياسي — **الطريق الوحيد إلى دالة التجزئة** في هذا النظام.

> 📕 **المواصفة الملزمة: [SPEC.md](SPEC.md).** اقرأها قبل أي تعديل.
> وفيها التحذير الذي يعلو على كل شيء: **لا تغيّر الشكل القانوني بعد أول توقيع.**

---

## العقد

```csharp
byte[]    Canonicalize(CanonicalDocument doc)                               // بايتات حتمية
ChainLink Compute(CanonicalDocument doc, long sequence, byte[] previousHash)
byte[]    Genesis(string scope)                                             // prev_hash للسجل 1
```

**ولا يوجد طريق ثالث.** و`Canonicalize` يرفض أي مستند غير مرتبط بموقع في السلسلة،
فيصير إدخال `chain_seq` و`prev_hash` داخل البايتات المُجزَّأة **مستحيل النسيان بالبناء**.

```csharp
ChainVerification VerifyChain(records, genesisHash)   // -> أول رقم تسلسل منحرف
```

---

## بلا اعتماديات — قاعدة، لا تفضيل

هذا المشروع **لا يرجع إلى أي حزمة خارجية**: لا EF Core، ولا Npgsql، ولا
`System.Text.Json`، ولا وحدة من وحدات النظام. البايتات المُجزَّأة لا يجوز أن تتحرّك
لأن حزمة رُقّيت.

(`Npgsql` تظهر في مشروع الاختبارات وحده، لاختبار الدورة الحقيقية مع PostgreSQL.)

---

## البنية

| المسار | الدور |
|---|---|
| `Canonicalizer.cs` | المُوحِّد، السجلّ، `ChainLink` — **الطريق الوحيد** |
| `CanonicalRuntime.cs` | حارس بيئة التشغيل: يكشف وضع العولمة الثابتة **سلوكياً** |
| `TextRules.cs` | الحدّ يُحوِّل (`CleanForInput`)، والمُجزِّئ يتحقّق (`RequireCanonical`) |
| `ArabicSearch.cs` | تطبيع البحث — **ناتجه لا يُجزَّأ أبداً**، ونوعه `SearchKey` لا `string` |
| `Amounts.cs` | مقياس 4، ثقافة ثابتة، **رفض لا تقريب** |
| `Instants.cs` | UTC بالميكروثانية، رفض `Unspecified`، التقاط مرّة واحدة |
| `CanonicalSchema.cs` | ترتيب الحقول + **مجموعة الاستثناء** المُعلنة والمُبصَّمة |
| `CanonicalDocument.cs` | بانٍ مقيَّد بمخطّط، يكتب بترتيب المخطّط لا بترتيب الاستدعاء |
| `ChainVerifier.cs` | إعادة التحقق، وإرجاع **أول** تسلسل منحرف |
| `Schemas/JournalEntrySchema.cs` | المخطّط المرجعي `babel.journal.entry` |

---

## التشغيل

```bash
# البناء والاختبارات (88 اختباراً، منها دورة حقيقية مع PostgreSQL محلية)
dotnet test --project tests/Babel.Canonicalization.Tests/Babel.Canonicalization.Tests.csproj

# المتجهات الذهبية — يخرج برمز غير صفري عند أي انحراف
dotnet run --project tools/Babel.Canonicalization.Golden -- --verify

# المخطّط ومجموعة الاستثناء
dotnet run --project tools/Babel.Canonicalization.Golden -- --schema
```

اختبارات PostgreSQL تقرأ الاتصال من `BABEL_CANON_TEST_DB` وتسقط إلى اتصال محلي بلا
كلمة مرور. **لا كلمات مرور في المستودع.**

---

## المصائد الخمس التي عليك معرفتها قبل أن تكتب سطراً بجوار هذه المكتبة

1. **وضع العولمة الثابتة** (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`) يجعل
   `String.Normalize` عملية لا شيء **بصمت**، ويجعل `IsNormalized` يكذب. مقيس.
   الحارس هنا يكشفه سلوكياً وينفجر عند التحميل.
2. **لغة النظام `ar-SA`** تجعل `100.5m.ToString("0.0000")` يعطي `100٫5000`،
   و`DateTime.ToString("d")` يعطي تاريخاً هجرياً **يحمل بداخله `U+200F`**. مقيس.
3. **Npgsql تقصّ الـticks ولا تقرّب**، و**تقبل `DateTimeKind.Unspecified` بصمت**. مقيس.
4. **PostgreSQL تقرّب «نصف بعيداً عن الصفر»** و.NET تقرّب «نصف إلى الزوجي». مقيس.
   ولذلك: رفض، لا تقريب.
5. **تطبيع البحث العربي مطلوب فعلاً في هذا المشروع** — وتشغيله على عمود موقَّع يكسر
   كل سلسلة. العمودان منفصلان، والنوع `SearchKey` يمنع الخلط عند الترجمة.

التفاصيل والقياسات كاملة في [SPEC.md](SPEC.md).
