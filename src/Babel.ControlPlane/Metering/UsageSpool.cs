using System.Text;
using System.Text.Json;

namespace Babel.ControlPlane.Metering;

/// <summary>
/// مخزن احتياطي محلّي يُضاف إليه فقط.
///
/// <para><b>المشكلة التي يحلّها:</b> قاعدة التحكّم قد تكون غير متاحة لحظة وقوع
/// الاستعمال. إسقاط الحدث حينها يعني <b>خسارة إيراد لا تُسترجَع</b>؛ وحفظه في
/// الذاكرة يعني خسارته عند أول إعادة تشغيل. فالحدث يُكتب على القرص
/// و<c>fsync</c> قبل أن يُقال للنداء إنه قُبل.</para>
///
/// <para>إعادة التصريف مُحكَمة: نفس مفتاح الإحكام + <c>ON CONFLICT DO NOTHING</c>،
/// فتصريف ملف صُرِّف نصفه لا يُضاعف شيئاً.</para>
/// </summary>
public sealed class UsageSpool(string path)
{
    private readonly Lock _gate = new();
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Path { get; } = path;

    public int Append(IEnumerable<UsageEvent> events)
    {
        var lines = events.Select(e => JsonSerializer.Serialize(e, Json)).ToList();
        if (lines.Count == 0) return 0;

        lock (_gate)
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using var fs = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
            var bytes = Encoding.UTF8.GetBytes(string.Join('\n', lines) + '\n');
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);   // fsync: بلا هذا فالوعد بالمتانة كذب
        }
        return lines.Count;
    }

    public List<UsageEvent> ReadAll()
    {
        lock (_gate)
        {
            if (!File.Exists(Path)) return [];
            var result = new List<UsageEvent>();
            foreach (var line in File.ReadAllLines(Path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var e = JsonSerializer.Deserialize<UsageEvent>(line, Json);
                if (e is not null) result.Add(e);
            }
            return result;
        }
    }

    public int Count => ReadAll().Count;

    public void Clear()
    {
        lock (_gate) { if (File.Exists(Path)) File.Delete(Path); }
    }
}
