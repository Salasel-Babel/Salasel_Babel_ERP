using System.Reflection;
using System.Text;

namespace BabelDemoCompany;

/// <summary>قارئ CSV صغير يكفي ملفّات <c>data/</c> المضمَّنة في هذه التجميعة.</summary>
internal static class Csv
{
    /// <summary>يقرأ ملفاً مضمَّناً صفوفاً بمفاتيح ترويسته.</summary>
    /// <param name="logicalName">الاسم المنطقي للمورد المضمَّن.</param>
    public static IReadOnlyList<Dictionary<string, string>> Embedded(string logicalName)
    {
        Assembly assembly = typeof(Csv).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException("مورد مضمَّن مفقود: " + logicalName);
        using StreamReader reader = new(stream, Encoding.UTF8);

        List<string> lines = [];
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        string[] header = Split(lines[0]);
        List<Dictionary<string, string>> rows = [];

        for (int i = 1; i < lines.Count; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }

            string[] cells = Split(lines[i]);
            Dictionary<string, string> row = new(StringComparer.Ordinal);
            for (int c = 0; c < header.Length; c++)
            {
                row[header[c]] = c < cells.Length ? cells[c] : string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string[] Split(string line)
    {
        List<string> cells = [];
        StringBuilder current = new();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    cells.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        cells.Add(current.ToString());
        return [.. cells];
    }
}
