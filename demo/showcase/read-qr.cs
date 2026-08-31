#:project ../../src/Babel.Compliance.Zatca/Babel.Compliance.Zatca.csproj
// ═══════════════════════════════════════════════════════════════════════════
// أداة العرض: تُشغّل **قارئ الرمز المشحون نفسه** لا نسخةً منه.
//
// السبب أن مشهد الرمز يجب أن يكون حقيقياً: ما يظهر على الشاشة هو مُخرَج
// `ZatcaQrReader.Read` الحرفي — بما فيه نصّ الرفض عند حمولة معطوبة.
// ولا يُعاد بناء منطق الفكّ في المتصفّح، لأن نسخةً ثانية تنحرف عن الأصل بصمت.
//
//   dotnet run demo/showcase/read-qr.cs -- <base64>
// المُخرَج: أسطر `مفتاح<tab>قيمة`، وأوّلها `refused` بـ0 أو 1.
// ═══════════════════════════════════════════════════════════════════════════
using System.Globalization;
using Babel.Compliance.Zatca.Qr;

if (args.Length != 1)
{
    Console.Error.WriteLine("الاستعمال: dotnet run demo/showcase/read-qr.cs -- <base64>");
    return 2;
}

try
{
    ZatcaQrContents contents = ZatcaQrReader.Read(args[0]);

    Console.WriteLine("refused\t0");
    Console.WriteLine("sellerName\t" + contents.SellerName);
    Console.WriteLine("sellerVatNumber\t" + contents.SellerVatNumber);
    Console.WriteLine("issuedAt\t" + contents.IssuedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
    Console.WriteLine("grossTotal\t" + contents.GrossTotal.ToString(CultureInfo.InvariantCulture));
    Console.WriteLine("taxTotal\t" + contents.TaxTotal.ToString(CultureInfo.InvariantCulture));
    Console.WriteLine("phase\t" + contents.Phase.ToString());
    Console.WriteLine("attested\t" + (contents.IsCryptographicallyAttested ? "1" : "0"));

    foreach (ZatcaQrTagLength length in contents.TagLengths)
    {
        Console.WriteLine(FormattableString.Invariant($"tag\t{length.Tag}\t{length.ByteLength}"));
    }

    return 0;
}
catch (ZatcaQrException failure)
{
    Console.WriteLine("refused\t1");
    Console.WriteLine("reason\t" + failure.Message.ReplaceLineEndings(" "));
    return 0;
}
