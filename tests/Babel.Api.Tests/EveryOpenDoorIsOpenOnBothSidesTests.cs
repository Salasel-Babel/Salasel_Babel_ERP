using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الأبواب المفتوحة مُعلَنة في موضعين، وهذا الحارس يجعلهما مجموعةً واحدة.</b>
/// <para>
/// «أي المسارات تُفتح بلا اعتماد؟» سؤالٌ له جوابان في هذا المستودع: قائمةٌ في
/// <c>RequestPrincipal.IsAnonymous</c> يقرؤها الخادم، و<c>security: []</c> في العقد
/// المنشور يقرؤه فريق الواجهة والعميل المُولَّد. <b>ولا شيء كان يربطهما.</b>
/// </para>
/// <para>
/// والانحراف يقع في الاتجاهين ولكلٍّ ثمنه:
/// </para>
/// <list type="bullet">
///   <item><b>مفتوحٌ في الخادم ومغلقٌ في العقد:</b> بابٌ يخدم الإنترنت بلا اعتماد ولا
///         يعلم به أحد — لأن الوثيقة تقول إنه محميّ، فلا يُراجَع بوصفه باباً مفتوحاً.
///         وهذا هو الاتجاه القاتل.</item>
///   <item><b>مفتوحٌ في العقد ومغلقٌ في الخادم:</b> عميلٌ يُبنى على وعدٍ لا يُوفى،
///         فيصطدم بـ401 في الإنتاج بعد أن مرّ في التطوير.</item>
/// </list>
/// <para>
/// <b>وهو نفس شكل فخ-84 حرفياً</b> — عقدٌ له أكثر من طرف وحارسٌ على طرفٍ واحد — منقولاً
/// من «العميل المُولَّد» إلى «قائمة الأبواب المفتوحة». والفحص يقرأ العقد المُودَع، ويطرق
/// كل بابٍ فيه <b>بلا اعتماد</b>، ويقارن جوابَ الخادم بما وعد به العقد.
/// </para>
/// </summary>
public sealed class EveryOpenDoorIsOpenOnBothSidesTests
{
    /// <summary>أفعال العمليات التي تُعدّ في الوثيقة.</summary>
    private static readonly string[] Verbs = ["get", "post", "put", "patch", "delete"];

    [Fact]
    public async Task ما_يعده_العقد_مفتوحاً_مفتوحٌ_وما_عداه_مُغلق_بلا_استثناء()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        string committed = await Http.ReadTextAsync(
            Path.Combine(RepositoryPaths.Root, "contracts/openapi/v1.json"));

        using JsonDocument document = JsonDocument.Parse(committed);
        JsonElement paths = document.RootElement.GetProperty("paths");

        List<string> disagreements = [];
        int open = 0;
        int shut = 0;

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (!Verbs.Contains(operation.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                // ‏security: [] في العملية تعني «هذا الباب بلا اعتماد». وغيابها يعني
                // الوراثة من security العلوي — أي bearerAuth إلزامية.
                bool contractSaysOpen =
                    operation.Value.TryGetProperty("security", out JsonElement security)
                    && security.GetArrayLength() == 0;

                using HttpResponseMessage response = await api.Call(Http.Request(
                    new HttpMethod(operation.Name.ToUpperInvariant()),
                    Concrete(path.Name),
                    credential: null,
                    Verbs.Contains(operation.Name, StringComparer.Ordinal) && operation.Name != "get" ? "{}" : null));

                // ‏**المعيار هو رمز الحدّ لا رمز الحالة**: بابٌ مفتوح قد يردّ 400 على جسم
                // ناقص وهو مفتوح فعلاً، وبابٌ مُغلق يردّ 401 وauth.credential_missing —
                // وهو الرمز الذي يكتبه وسيط المصادقة وحده قبل أي توجيه.
                bool serverSaysShut = response.StatusCode == HttpStatusCode.Unauthorized
                    && await IsMissingCredentialAsync(response);

                if (contractSaysOpen)
                {
                    open++;
                    if (serverSaysShut)
                    {
                        disagreements.Add(
                            $"{operation.Name} {path.Name}: العقد يقول «بلا اعتماد» والخادم يردّ auth.credential_missing");
                    }
                }
                else
                {
                    shut++;
                    if (!serverSaysShut)
                    {
                        disagreements.Add(
                            FormattableString.Invariant(
                                $"{operation.Name} {path.Name}: العقد يقول «باعتماد» والخادم خدم الطلب بلا اعتماد ({(int)response.StatusCode})"));
                    }
                }

                Console.WriteLine(
                    FormattableString.Invariant(
                        $"{operation.Name} {path.Name} · العقد: {(contractSaysOpen ? "مفتوح" : "مُغلق")} · الخادم: {(serverSaysShut ? "مُغلق" : "مفتوح")} ({(int)response.StatusCode})"));
            }
        }

        Assert.True(
            disagreements.Count == 0,
            "قائمة الأبواب المفتوحة تختلف بين الخادم والعقد المنشور — وهو فخ-84 بشكله الثاني:\n"
            + string.Join('\n', disagreements));

        // حارس اللافراغ **من الطرفين**: فحصٌ لا يرى باباً مفتوحاً واحداً لا يُثبت أن
        // المفتوح مفتوح، وفحصٌ لا يرى باباً مُغلقاً واحداً لا يُثبت أن المُغلق مُغلق.
        Assert.True(open >= 3, FormattableString.Invariant($"وُجد {open} باب مفتوح فقط في العقد — الماسح ضامر"));
        Assert.True(shut >= 10, FormattableString.Invariant($"وُجد {shut} باب مُغلق فقط في العقد — الماسح ضامر"));
    }

    private static async Task<bool> IsMissingCredentialAsync(HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync(ApiFixture.Token);

        if (text.Length == 0)
        {
            return false;
        }

        using JsonDocument problem = JsonDocument.Parse(text);
        return problem.RootElement.TryGetProperty("code", out JsonElement code)
            && string.Equals(code.GetString(), "auth.credential_missing", StringComparison.Ordinal);
    }

    /// <summary>
    /// يستبدل وسائط المسار بقيم سليمة الشكل.
    /// <para>
    /// وأي قيمة تكفي: وسيط المصادقة يقع <b>قبل التوجيه</b>، فهو لا يقرأ وسيط مسار أصلاً.
    /// والقيم سليمة الشكل رغم ذلك كي لا يُخلَط رفضٌ شكليٌّ بجوابٍ عن المصادقة.
    /// </para>
    /// </summary>
    private static string Concrete(string template) => template
        .Replace("{companyId}", ApiTestDatabase.CompanyA.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{entryId}", "01a00000-0000-7000-8000-000000000001", StringComparison.Ordinal)
        .Replace("{documentType}", "sales.invoice", StringComparison.Ordinal)
        .Replace("{costCenterCode}", "main", StringComparison.Ordinal);
}
