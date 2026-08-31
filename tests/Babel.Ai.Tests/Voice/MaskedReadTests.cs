using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>القراءة المُقنَّعة — الشاشة تُرى بزاوية، والصوت يُسمَع في الغرفة.</b>
/// <para>
/// وحدة الموارد البشرية تُخرج الهوية والآيبان <b>مُقنَّعين</b>: آخر أربعة محارف وحدها
/// وما قبلها نقاطٌ بعددٍ ثابت لا يساوي الطول (‏<c>EmployeeService.Mask</c> — وعددٌ ثابت
/// لأن قناعاً يحفظ الطول يُسرّب الطول، وطولُ الآيبان يميّز بلد إصداره).
/// </para>
/// <para>
/// <b>وهذا الإثبات يقيس أن المسار المنطوق لا يخفّف تلك القاعدة بل يشدّدها</b>: نصٌّ
/// مُزمَعٌ نُطقُه يحمل هويةً أو آيباناً كاملاً <b>يُرفض</b>، ولو جاء من مسارٍ لم يُقصد.
/// والحارس على <b>شكل القيمة</b> لا على نيّة من كتبها — فيلتقط التسريب الذي يأتي من
/// حيث لم يُنتظر.
/// </para>
/// </summary>
public sealed class MaskedReadTests
{
    private const string NationalId = "1092837465";
    private const string Iban = "SA0380000000608010167519";

    [Fact]
    public void القناع_هو_قناع_الموارد_البشرية_نفسه_لا_قناع_ثانٍ()
    {
        Assert.Equal("••••7465", VoiceDisclosure.Mask(NationalId));
        Assert.Equal("••••7519", VoiceDisclosure.Mask(Iban));

        // وقصيرٌ لا يُكشف نصفه: القناع كامل.
        Assert.Equal("••••", VoiceDisclosure.Mask("123"));
        Assert.Equal("••••", VoiceDisclosure.Mask(null));

        // والقناع **لا يحفظ الطول**: نجومٌ بعددٍ ثابت مهما طال الأصل.
        Assert.Equal(VoiceDisclosure.Mask(NationalId).Length, VoiceDisclosure.Mask(Iban).Length);
    }

    [Fact]
    public void نص_يحمل_رقم_هوية_كاملاً_يُرفض_نُطقُه()
    {
        Result guarded = VoiceDisclosure.Guard("بطاقة الموظف أحمد الغامدي، الهوية " + NationalId);

        Assert.True(guarded.IsFailure);
        Error only = Assert.Single(guarded.Errors);
        Assert.Equal("ai.voice.masked_read_required", only.Code);
        Assert.Contains("رقم الهوية", only.MessageAr, StringComparison.Ordinal);
    }

    [Fact]
    public void نص_يحمل_آيباناً_كاملاً_يُرفض_نُطقُه()
    {
        Result guarded = VoiceDisclosure.Guard("الحساب البنكي " + Iban);

        Assert.True(guarded.IsFailure);
        Assert.Contains(guarded.Errors, error => error.MessageAr.Contains("الآيبان", StringComparison.Ordinal));
    }

    [Fact]
    public void النص_المُقنَّع_يمر_فالحارس_يمنع_التسريب_لا_القراءة()
    {
        // شاهد سلبي: بلا هذا الإثبات يكون «الحارس يرفض» ادّعاءً يمكن الوفاء به بمنع كل شيء.
        Result guarded = VoiceDisclosure.Guard(
            "بطاقة الموظف أحمد الغامدي، الهوية " + VoiceDisclosure.Mask(NationalId)
            + "، الآيبان " + VoiceDisclosure.Mask(Iban));

        Assert.True(guarded.IsSuccess);
    }

    [Fact]
    public void ملخص_نية_الموارد_البشرية_يمر_بالحارس_ولا_يحمل_قيمة_شخصية()
    {
        VoiceIntent intent = VoiceHarness.Registry.Find("hr.employee.query")!;
        Assert.True(intent.ReadsPersonalData);

        VoiceResolution resolution = SpokenCommandReader
            .Read("بيانات الموظف أحمد الغامدي", VoiceHarness.Registry, VoiceHarness.Options).Value;

        Assert.True(VoiceDisclosure.Guard(resolution.ReadbackAr).IsSuccess);
        Assert.DoesNotContain(NationalId, resolution.ReadbackAr, StringComparison.Ordinal);
    }

    [Fact]
    public void لا_نية_في_السجل_كله_تطلب_رقم_هوية_أو_آيباناً_شريحةً()
    {
        string[] forbidden = ["nationalId", "national_id", "iban", "bankAccount", "identityNumber"];
        int slots = 0;

        foreach (VoiceIntent intent in VoiceHarness.Registry.Intents)
        {
            foreach (VoiceSlot slot in intent.Slots)
            {
                slots++;
                Assert.DoesNotContain(slot.Name, forbidden, StringComparer.OrdinalIgnoreCase);
            }
        }

        Assert.True(slots >= 60, slots.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void قراءة_تُنتج_ملخصاً_يحمل_هوية_غير_مقنعة_تُرفض_قبل_أن_تعود()
    {
        // شاهد موجب مزروع: شريحةُ «رمز» تقبل الأرقام داخلها بحكم صنفها (رقمُ وحدةٍ
        // أو موقعٍ في رفّ)، فهي الباب الذي يستطيع رقمُ هويةٍ أن يدخل منه فعلاً. ولو
        // دخل، **فالقراءة نفسها تُرفض** — لا الطبقةُ التي تنطق الملخّص لاحقاً، فتلك
        // تُنسى وهذه لا تُتجاوَز.
        Result<VoiceResolution> read = SpokenCommandReader.Read(
            "حالة الوحدة " + NationalId, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsFailure);
        Assert.Contains(read.Errors, error => error.Code == "ai.voice.masked_read_required");
    }
}
