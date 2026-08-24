using System.Text.Json;
using System.Text.Json.Serialization;

namespace Babel.Api.Wire;

/// <summary>
/// قيمة عشرية <b>كما وصلت نصّاً</b>، بلا تحليل بعد.
/// <para>
/// الفصل بين «وصلت» و«حُلِّلت» مقصود: المحوّل يعرف <b>نوع الرمز</b> ولا يعرف اسم الحقل
/// ولا مقياسه المسموح، والمُطابِق يعرف الاثنين. فلو حُلِّلت القيمة داخل المحوّل لخرجت
/// رسالة خطأ بلا اسم حقل وبلا المقياس المطلوب — وهي رسالة تُرسل مطوّر الواجهة إلى
/// نصف ساعة تخمين.
/// </para>
/// </summary>
/// <param name="Raw">النصّ كما ورد.</param>
[JsonConverter(typeof(WireDecimalJsonConverter))]
internal readonly record struct WireDecimal(string Raw)
{
    /// <inheritdoc />
    public override string ToString() => Raw;
}

/// <summary>
/// محوّل يفرض <b>نوع الرمز</b> قبل أي شيء آخر: المبلغ نصّ، ولا يكون رمزاً رقمياً أبداً.
/// <para>
/// <b>هذه هي النقطة التي يُغلق عندها مسار فقدان الدقّة.</b> رمز رقمي في حقل مالي يعني
/// أن العميل — وأولهم <c>JSON.parse</c> في المتصفّح — قد مرّر القيمة على <c>double</c>
/// ثنائي قبل أن ترحل، فيصل إلى الخادم ما تبقّى منها. ولا يستطيع الخادم أن يستردّ ما
/// فُقد؛ يستطيع فقط أن <b>يرفض القناة</b>.
/// </para>
/// </summary>
internal sealed class WireDecimalJsonConverter : JsonConverter<WireDecimal>
{
    /// <inheritdoc />
    public override WireDecimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            throw new WireFormatException(
                "wire.money.number_token",
                "الحقل المالي وصل رمزاً رقمياً في JSON. المبالغ تعبر السلك نصّاً حصراً: الرمز الرقمي "
                + "يمرّ في أغلب عملاء JSON على فاصلة عائمة ثنائية، فيقع فقدان الدقّة عند العميل قبل "
                + "أن يصل الطلب — ولا يملك الخادم استرداده، بل رفض القناة.",
                "A monetary field arrived as a JSON number token. Amounts cross the wire as strings only: a "
                + "number token is routed through a binary double by most JSON clients, so precision is lost on "
                + "the client before the request leaves — the server cannot recover it, only refuse the channel.");
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new WireFormatException(
                "wire.money.not_a_string",
                "الحقل المالي يجب أن يكون نصّاً في JSON.",
                "A monetary field must be a JSON string.");
        }

        return new WireDecimal(reader.GetString() ?? string.Empty);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, WireDecimal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Raw);
    }
}
