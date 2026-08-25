using System.Globalization;
using Babel.Canonicalization;
using Babel.Canonicalization.Schemas;
using Babel.Contracts.Posting;
using Babel.Ledger.PostingMatrix;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>الحدّ الذي لا تعبره هجرة الترجمات — مثبَّتاً باختبار لا بتعليق.</b>
/// <para>
/// ‏ADR-0021 بند 3 يقول: «الترجمة لا تدخل البايتات المُجزَّأة». وهذا الاختبار يُثبت أن
/// المخالفة <b>قائمة اليوم</b> ولم تُحدثها الهجرة: <c>matrix.business_event.name_en</c> —
/// وهو <b>نصّ عرض</b> بقاعدة التصنيف في §6.2 (قارئه يقرّر ولا يُصلح، وحذفه يترك سطراً
/// بلا معنى) — ينساب إلى <c>ledger.journal_line.description</c>، وهو <b>حقل من الحقول
/// المُجزَّأة</b> في المخطّط القانوني v2.
/// </para>
/// <para>
/// <b>ولماذا اختبارٌ لا إصلاح:</b> إخراج <c>description</c> من المخطّط <b>يغيّر الشكل
/// القانوني</b>. و v1 مجمَّد بمتّجهات ذهبية و v2 حيّ، وحذف حقل من المجموعة المُجزَّأة
/// كسرٌ لمصنوع مختوم — أي إصدار ثالث. فالحدّ يُثبَّت هنا كي لا يُنقَل هذا العمود إلى
/// جدول الترجمات بحسن نيّة، لا كي يُصلَح في هذه الهجرة.
/// </para>
/// <para>
/// <b>وهذه ثغرة حقيقية في قاعدة §6.2 لا في هذه الشيفرة:</b> القاعدة تصنّف النصّ «عرضاً»
/// أو «تشخيصاً»، ولا صنف فيها لنصّ <b>عرضٍ صار واقعةً مُوقَّعة</b> بدخوله البايتات.
/// مرفوعة في التقرير وفي وثيقة القرار.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class DisplayTextInsideTheHashedBytesTests : IAsyncLifetime
{
    private const string Book = "HASHTXT";

    /// <summary>الحدث الذي يُرحَّل به هنا — قالبٌ في المصفوفة يحمل اسماً بلغتين.</summary>
    private const string EventCode = "realestate.rent.accrual.own_property";

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void حقلا_البيان_من_الحقول_المُجزَّأة_في_المخطّط_القانوني_v2()
    {
        // الادّعاء الأول: description و description_ar **داخل** البايتات المُوقَّعة،
        // ومقروءان من المخطّط نفسه لا من قائمة مكتوبة في هذا الملف.
        SchemaField lines = Assert.Single(
            JournalEntrySchema.V2.Fields,
            static field => field.Name == "lines");

        string[] names = [.. (lines.GroupFields ?? []).Select(static field => field.Name)];

        Proof.Require(
            names.Contains("description", StringComparer.Ordinal)
                && names.Contains("description_ar", StringComparer.Ordinal),
            "‏description و description_ar حقلان مُجزَّآن في v2",
            string.Join("، ", names.Where(static n => n.StartsWith("description", StringComparison.Ordinal))));

        // ولا يُخلطان بالمشتقّ: description_ar_search **مستثنى** صراحةً.
        Proof.Require(
            JournalEntrySchema.V2.Exclusions.Any(static field => field.Name == "description_ar_search"),
            "المشتقّ البحثي مستثنى، والأصل مُجزَّأ — عمودان لا عمود",
            "description_ar_search");
    }

    [Fact]
    public void اسم_حدث_المصفوفة_نصُّ_عرضٍ_وهو_داخل_البايتات_المُجزَّأة_فعلاً()
    {
        // الادّعاء الثاني، وهو الحدّ نفسه: القيمة المكتوبة في العمود المُجزَّأ هي
        // **حرفياً** اسم الحدث في ملفّ المصفوفة تحت data/ — لا نصّ ولّده المحرك.
        MatrixEvent? definition = MatrixCatalog.Default.Find(EventCode);

        Proof.Require(definition is not null, "الحدث موجود في المصفوفة", EventCode);

        Proof.Require(
            definition!.NameEn.Length > 0 && definition.NameAr.Length > 0,
            "اسم الحدث في المصفوفة زوجٌ ثابت ar/en اليوم",
            definition.NameAr + " · " + definition.NameEn);
    }

    [Fact]
    public async Task القيمة_المكتوبة_في_عمود_description_هي_اسم_الحدث_الإنجليزي_حرفياً()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        MatrixEvent definition = MatrixCatalog.Default.Find(EventCode)
            ?? throw new InvalidOperationException("الحدث « " + EventCode + " » غير موجود في المصفوفة.");

        await LedgerTestEnvironment.EnsureCounterAsync(LedgerTestEnvironment.TenantA, Book, token);

        PostingRequest request = Requests.RentAccrual(
            LedgerTestEnvironment.TenantA,
            "HASHTXT-1",
            1_200.0000m,
            new DateOnly(2026, 6, 10),
            LedgerTestEnvironment.OwnProperty) with
        {
            Book = Book,
            IdempotencyKey = new IdempotencyKey("hashtext:HASHTXT-1"),
        };

        Result<PostingReceipt> posted = await _harness.Posting.PostAsync(request, token);

        Proof.Require(
            posted.IsSuccess,
            "قيد القالب رُحّل",
            posted.IsSuccess
                ? posted.Value.EntryNumber.ToString(CultureInfo.InvariantCulture)
                : string.Join(" | ", posted.Errors.Select(static e => e.Code)));

        await using NpgsqlConnection connection = LedgerHarness.OpenOwner();
        await using NpgsqlCommand command = new(
            """
            select distinct l.description, l.description_ar
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
             where e.company_id = $1 and e.book_id = $2
            """, connection);
        command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
        command.Parameters.AddWithValue(Book);

        List<(string English, string Arabic)> descriptions = [];

        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                descriptions.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        Proof.Require(descriptions.Count > 0, "سطور القيد مكتوبة", descriptions.Count.ToString(CultureInfo.InvariantCulture));

        Proof.Require(
            descriptions.TrueForAll(pair => pair.English == definition.NameEn),
            "‏description = اسم الحدث الإنجليزي من data/ حرفياً — نصّ عرضٍ داخل عمود مُجزَّأ",
            definition.NameEn);

        Proof.Require(
            descriptions.TrueForAll(pair => pair.Arabic == definition.NameAr),
            "‏description_ar = اسم الحدث العربي من data/ حرفياً",
            definition.NameAr);

        Proof.Note(
            "وهذا هو الحدّ: نقلُ matrix.business_event.name_en إلى جدول الترجمات يغيّر "
            + "قيمة عمود يدخل البايتات المُوقَّعة لكل قيد قالبٍ تالٍ. فهو v3 لا هجرةُ عرض، "
            + "وهو خارج نطاق هذه الهجرة عمداً.");
    }

    [Fact]
    public void جدول_الترجمات_لا_يُذكر_في_المخطّط_القانوني_لا_حقلاً_ولا_استثناءً()
    {
        // الوجه الآخر من الحدّ: ما بُني في هذه الهجرة **لا يمسّ** الشكل القانوني.
        // ولا يكفي أن يغيب عن الحقول: غيابه عن الاستثناءات أيضاً يعني أنه ليس عموداً
        // في الجدولين أصلاً، فلا سؤال «لماذا لم يُجزَّأ؟» يُطرَح عليه.
        string[] hashed =
        [
            .. JournalEntrySchema.V2.Fields.Select(static field => field.Name),
            .. JournalEntrySchema.V2.Fields
                .SelectMany(static field => field.GroupFields ?? [])
                .Select(static field => field.Name),
        ];

        string[] excluded = [.. JournalEntrySchema.V2.Exclusions.Select(static field => field.Name)];

        foreach (string token in new[] { "name_translation", "language_tag", "translations", "entity_kind" })
        {
            Proof.Require(
                !hashed.Any(name => name.Contains(token, StringComparison.Ordinal)),
                "لا حقل مُجزَّأ يذكر « " + token + " »",
                string.Join("، ", hashed.Where(name => name.Contains(token, StringComparison.Ordinal))));

            Proof.Require(
                !excluded.Any(name => name.Contains(token, StringComparison.Ordinal)),
                "ولا عمود مستثنى يذكره — فهو ليس عموداً في جدولي الدفتر أصلاً",
                string.Join("، ", excluded.Where(name => name.Contains(token, StringComparison.Ordinal))));
        }
    }
}
