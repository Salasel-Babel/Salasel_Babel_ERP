# مقياس فخّ التقويم الثقافي / culture-calendar spike

أداة قياس واحدة لفئة واحدة من الأعطال: **التحويل المعتمِد على الثقافة على قيمة تُحفَظ أو
تُجزَّأ أو تُفهرَس أو تُقارَن.**

```bash
dotnet run --project spikes/culture-calendar
```

تُنتج الجدول المستشهَد به في [`docs/evidence/measurements.md` §3.8](../../docs/evidence/measurements.md)
و[`docs/evidence/traps.md`](../../docs/evidence/traps.md) فخ-38 وفخ-39.

لا قاعدة بيانات ولا شبكة ولا حزمة خارجية: الفخّ كلّه في وقت التشغيل، ويُعاد إنتاجه في أقلّ من ثانية.

> **`InvariantGlobalization=false` هنا مقصود وإلزامي**: هو إعداد المنتج نفسه في
> `Directory.Build.props`. تشغيل المقياس بـ`true` يُخفي كل شيء ويجعله يبدو سليماً — وهذا
> بعينه هو التعارض الموصوف في [`ADR-0007`](../../docs/decisions/ADR-0007-journal-entry-hash-chain.md).

القاعدة التي تمنع ارتداد هذا الفخّ في المنتج:
`tests/Babel.ArchitectureTests/Rule10_NoCultureDependentPersistence.cs`.
