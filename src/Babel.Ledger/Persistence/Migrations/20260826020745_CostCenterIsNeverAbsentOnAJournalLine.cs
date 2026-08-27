using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Ledger.Persistence.Migrations
{
    /// <summary>
    /// مركز التكلفة لا يغيب عن سطر قيد — مفروضاً في قاعدة البيانات (ADR-0026).
    /// <para>
    /// ‏ADR-0026 يقرّر أن لكل منشأة مركز تكلفة واحداً على الأقل وأن <c>CostCenterId</c> لا
    /// يكون فارغاً في أي موضع. وكان ذلك مفروضاً عند حدٍّ واحد — تأسيس المنشأة — ومنقوضاً
    /// عند حدٍّ آخر: العمود يقبل <c>null</c>، والعقد يقبل غيابه، والنوع يقول <c>string?</c>.
    /// هذه الهجرة تُغلق الحدّ الأخير: <b>القيد يلزم أيّ كاتب</b> — نصّ SQL يدوي، أو أداة
    /// استيراد، أو هجرة مستقبلية سهت — لا من يمرّ بـ C# فحسب.
    /// </para>
    /// <para>
    /// <b>ولا تكتب هذه الهجرة بايتاً واحداً في <c>journal_line</c>. وهذا ليس تحفّظاً:</b>
    /// سطر القيد <b>واقعة مُجزَّأة</b>. إعادة التحقق من السلسلة تُعيد بناء البايتات القانونية
    /// من هذا الصفّ نفسه وتقارنها بالبصمة المخزَّنة (‏ADR-0007 · <c>LedgerAuditService</c>)،
    /// و<c>cost_center</c> حقلٌ في تلك البايتات في الشكلين v1 وv2 معاً. فتعبئةُ عمودٍ فارغ
    /// على قيد مُرحَّل تجعل <b>دفتراً سليماً يُبلّغ عن عبث</b> — وهو أسوأ ما يمكن أن تفعله
    /// هجرة بدفتر: تحويل الحارس إلى كاذب.
    /// </para>
    /// <para>
    /// <b>ولا تُخترَع قيمة كذلك.</b> «مركز التكلفة الافتراضي» على قيدٍ لم يُرحَّل عليه
    /// <b>إفادةٌ محاسبية كاذبة</b>: تقول إن مصروفاً حُمِّل على مركز لم يُحمَّل عليه، ويقرؤها
    /// تقرير تكلفة بعد سنتين على أنها واقعة. والغياب الصادق أرخص من الحضور الكاذب — وهو
    /// المبدأ المنظِّم لسجلّ المصائد كلّه.
    /// </para>
    /// <para>
    /// <b>فما يقع على دفتر عامر:</b> يُضاف القيد <c>not valid</c> — فيلزم <b>كل</b> إدراج
    /// وتحديث من هذه اللحظة، ولا يفحص التاريخ. ثم <b>تُحاوَل المصادقة</b>: إن لم يكن في
    /// الدفتر سطرٌ واحد بلا مركز — وهي حال كل دفتر لهذا المنتج اليوم — نجحت المصادقة وصار
    /// <c>pg_constraint.convalidated = true</c>، أي أن الثابتة <b>تامّة على الجدول كلّه</b>.
    /// وإن وُجدت سطور سبقت الثابتة، تُترك كما هي، و<b>تُطبَع أعدادها ومعرّفات قيودها</b>،
    /// ويبقى <c>convalidated = false</c> — وهو الموضع الذي تقول فيه قاعدة البيانات نفسها:
    /// «ألزم من هنا فصاعداً، وهذه السطور سبقتني». <b>لا صمت، ولا كذب، ولا إعادة كتابة.</b>
    /// </para>
    /// <para>
    /// <b>ولماذا لا <c>set not null</c> على العمود:</b> لأنه يفحص الجدول كلّه ويسقط على
    /// الدفتر الذي رفضنا إعادة كتابته — فيصير الترقية مستحيلة بدل أن تكون جزئية معلَنة.
    /// والقيد يحمل الضمان نفسه لكل كتابة جديدة، ويحمله للجدول كلّه متى صُودق عليه.
    /// </para>
    /// <para>
    /// <b>والعكس محروس في الشيفرة لا هنا:</b> عكسُ سطرٍ سبق الثابتة يُنتج سطر عكسٍ بلا
    /// مركز، ويرفضه <c>PostingService</c> بـ<c>ledger.posting.missing_cost_center</c>
    /// مسمّياً السطر ودوره — لا برمز <c>23514</c> خامّ يسمّي جدولاً.
    /// </para>
    /// </summary>
    internal partial class CostCenterIsNeverAbsentOnAJournalLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            // ── ١) القيد أولاً، غير مُصادَق: يلزم من الآن ولا يمسّ التاريخ ───
            migrationBuilder.Sql("""
                alter table ledger.journal_line
                  add constraint ck_journal_line_cost_center_present
                  check (cost_center_id is not null and length(btrim(cost_center_id)) > 0)
                  not valid;
                """);

            // ── ٢) ثم المصادقة، مشروطةً بالتاريخ نفسه ────────────────────────
            // والعدّ يقع **قبل** المحاولة كي تُطبع الحصيلة في الحالتين: دفترٌ نظيف
            // يقول «صفر، والقيد مُصادَق»، ودفترٌ سبق الثابتة يقول كم سطراً وأي قيود.
            migrationBuilder.Sql("""
                do $costcenter$
                declare
                    v_legacy  bigint;
                    v_entries text;
                begin
                    select count(*) into v_legacy
                      from ledger.journal_line
                     where cost_center_id is null or length(btrim(cost_center_id)) = 0;

                    if v_legacy = 0 then
                        alter table ledger.journal_line
                          validate constraint ck_journal_line_cost_center_present;

                        raise notice
                            'COST_CENTER_CONSTRAINT_VALIDATED : لا سطر واحد بلا مركز تكلفة، فالقيد مُصادَق على الجدول كلّه / not one line lacks a cost centre, so the constraint is validated over the whole table';
                    else
                        select string_agg(distinct entry_id::text, ' · ' order by entry_id::text)
                          into v_entries
                          from (
                            select entry_id
                              from ledger.journal_line
                             where cost_center_id is null or length(btrim(cost_center_id)) = 0
                             order by entry_id
                             limit 50
                          ) as affected;

                        raise notice
                            'COST_CENTER_PRE_INVARIANT_LINES lines=% entries=% : سطورٌ مُرحَّلة سبقت الثابتة. لا تُكتب ولا يُخترَع لها مركز — بايتاتها مُجزَّأة والسلسلة تُعيد بناءها منها. والقيد يلزم من الآن فصاعداً ويبقى غير مُصادَق (convalidated = false) إعلاناً لذلك / posted lines that predate the invariant. They are neither written to nor given an invented centre — their bytes are hashed and the chain rebuilds them. The constraint binds from now on and stays unvalidated (convalidated = false) to say so',
                            v_legacy, coalesce(v_entries, '—');
                    end if;
                end
                $costcenter$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            // التراجع يُسقط القيد ولا شيء غيره — لأن الصعود لم يكتب شيئاً.
            migrationBuilder.DropCheckConstraint(
                name: "ck_journal_line_cost_center_present",
                schema: "ledger",
                table: "journal_line");
        }
    }
}
