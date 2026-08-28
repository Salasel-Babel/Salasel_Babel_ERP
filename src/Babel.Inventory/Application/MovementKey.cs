using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Contracts.Inventory;

namespace Babel.Inventory.Application;

/// <summary>
/// ترميز هوية حركة مخزون في مفتاح واحد ثابت العرض.
/// <para>
/// <b>ولماذا بصمة لا سلسلة موصولة:</b> السلسلة المبنية بالوصل على فاصل قد يحتويه أحد
/// المكوّنات هي عطب تصادم بذاتها — <c>("A/B","C")</c> و<c>("A","B/C")</c> تُنتجان
/// البايتات نفسها. فيُرمَّز كل مكوّن <b>مسبوقاً بطوله</b> قبل التجزئة: الطول الصريح
/// يجعل حدود المكوّنات غير قابلة للتزوير مهما كان محتواها.
/// </para>
/// <para>وهو الشكل نفسه المعتمد في بوّابة ترحيل المبيعات، وللسبب نفسه.</para>
/// </summary>
internal static class MovementKey
{
    /// <summary>بادئة المفتاح وإصدار ترميزه — الإصدار في المفتاح كي يُقرأ شكله من قيمته.</summary>
    private const string Prefix = "inv:v1:";

    /// <summary>يبني المفتاح من هوية الحركة.</summary>
    /// <param name="source">هوية الحركة.</param>
    public static string Of(InventoryMovementSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        StringBuilder canonical = new();
        Append(canonical, source.Module.ToString());
        Append(canonical, source.DocumentType);
        Append(canonical, source.DocumentId);
        Append(canonical, source.TriggerCode);
        Append(canonical, source.Generation.ToString(CultureInfo.InvariantCulture));
        Append(canonical, source.EventCode);

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Prefix + Convert.ToHexStringLower(digest);
    }

    private static void Append(StringBuilder canonical, string component)
        => canonical.Append(component.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(component);
}
