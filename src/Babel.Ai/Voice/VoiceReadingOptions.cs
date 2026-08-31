namespace Babel.Ai.Voice;

/// <summary>
/// ما يُحقن في القراءة كي تكون حتمية. <b>ولا يُقرأ شيء منه من ساعة الجهاز داخل المحرّك</b>:
/// قارئٌ يسأل <c>DateTime.Today</c> بنفسه يعطي نتيجةً مختلفة كل يوم، فلا يُعاد تشغيل عطلٍ
/// وقع أمس.
/// </summary>
/// <param name="Today">تاريخ اليوم بصيغة ISO الميلادية، أو لا شيء فلا يُملأ تاريخ إطلاقاً.</param>
/// <param name="StatutoryTaxRate">
/// النسبة النظامية حين لا تُنطق — <b>نصّ لا عدد عائم</b>، وتُوسَم «من الإعدادات» لا «منطوق».
/// </param>
public sealed record VoiceReadingOptions(string? Today = null, string? StatutoryTaxRate = null);
