using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Ledger.Persistence.Migrations
{
    /// <summary>
    /// هوية الترحيل تشمل رمز الحدث (D-3).
    /// <para>
    /// كان المفتاح <c>uq_posting_identity</c> = (شركة · نوع المستند · رقم المستند ·
    /// رمز الإطلاق · الجيل). والمستند الواحد يُنتج <b>حدثين مختلفين عند الإطلاق
    /// نفسه</b> في حالات يومية لا استثنائية: فاتورة مبيعات تعترف بالإيراد وتُنزل
    /// المخزون بالتكلفة؛ وفاتورة مورد تُثبت الالتزام وتعترف بفرق سعر مقابل استلام
    /// سابق؛ ودفعةٌ تُسدّد التزاماً وتسجّل رسماً بنكياً؛ ومسيرُ رواتب يُثبت الأجر
    /// وحصة المنشأة في التأمينات. وبلا رمز الحدث في المفتاح كان الثاني منهما
    /// يُعدّ «مُرحَّلاً سلفاً»: لا يُكتب، ولا يُرفع خطأ، والدفتر يبقى متوازناً،
    /// والسلسلة تبقى صحيحة. العَرَض الوحيد دفتر مساعد لا يطابق حسابه الضابط.
    /// </para>
    /// <para>
    /// <b>لماذا هذه الهجرة آمنة على بيانات قائمة:</b> المفتاح الجديد <b>أوسع</b>
    /// من القديم بعمود، وتوسيع مفتاح فريد لا يُنتج تصادماً جديداً أبداً — كل صفّ
    /// كان فريداً بخمسة أعمدة يبقى فريداً بستة. وإسقاط فهرس وإنشاؤه لا يمرّ على
    /// <c>REVOKE UPDATE, DELETE</c> (تلك صلاحيات صفوف لدور التطبيق، والهجرة بدور
    /// المالك)، ولا يوقظ المشغّل المؤجَّل <c>trg_journal_entry_balanced</c> لأنه
    /// مشغّل صفوف <c>after insert</c> لا مشغّل DDL.
    /// </para>
    /// <para>
    /// <b>وما لا تفعله:</b> لا تُعيد كتابة صفّ واحد. الدفتر يُضاف إليه فقط
    /// (ADR-0002)، و<c>event_code</c> من البايتات المُجزَّأة في الشكل القانوني v2 —
    /// فتعبئته بأثر رجعي تكسر كل بصمة تالية. ولذلك إن وُجد صفّ برمز حدث فارغ
    /// تتوقّف الهجرة برسالة تسمّي الصفوف: العلاج عكسٌ مشروع وإعادة ترحيل، لا
    /// <c>UPDATE</c>.
    /// </para>
    /// </summary>
    internal partial class PostingIdentityIncludesEventCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            // ── حارس قبل أي DDL: تاريخٌ برمز حدث فارغ لا يُصحَّح بـUPDATE ────
            migrationBuilder.Sql("""
                do $guard$
                declare
                    v_blank bigint;
                begin
                    select count(*) into v_blank
                      from ledger.journal_entry
                     where event_code is null or length(btrim(event_code)) = 0;

                    if v_blank > 0 then
                        raise exception
                            'BLANK_EVENT_CODE_IN_HISTORY rows=% : يوجد % قيداً برمز حدث فارغ. رمز الحدث يدخل البايتات المُجزَّأة، وتعبئته بأثر رجعي تكسر سلسلة البصمات — العلاج عكسٌ مشروع وإعادة ترحيل لا تعديل / entries with a blank event code exist; the event code is inside the hashed bytes, so back-filling it would break the hash chain — reverse and re-post instead of editing',
                            v_blank, v_blank
                            using errcode = 'check_violation';
                    end if;
                end
                $guard$;
                """);

            migrationBuilder.DropIndex(
                name: "uq_posting_identity",
                schema: "ledger",
                table: "journal_entry");

            migrationBuilder.AlterColumn<string>(
                name: "event_code",
                schema: "ledger",
                table: "journal_entry",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "");

            migrationBuilder.CreateIndex(
                name: "uq_posting_identity",
                schema: "ledger",
                table: "journal_entry",
                columns: new[] { "company_id", "source_doc_type", "source_doc_id", "posting_trigger_code", "posting_generation", "event_code" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_journal_entry_event_code",
                schema: "ledger",
                table: "journal_entry",
                sql: "length(btrim(event_code)) > 0");

            // ── والمسار الخام يجب أن يوافق النموذج، وإلا تصادم مسارا هوية ──
            // ‏ledger.post_entry تحمل فحص الإحكام بنفسها (مرّتين: قبل القفل
            // وتحته). فهرسٌ مُصلَح ودالةٌ على المفتاح القديم أسوأ من العطب
            // الأصلي: مساران يختلفان في تعريف «هذا رُحّل من قبل».
            migrationBuilder.Sql(LedgerSchemaDeployer.Script("PostEntryFunction.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "uq_posting_identity",
                schema: "ledger",
                table: "journal_entry");

            migrationBuilder.DropCheckConstraint(
                name: "ck_journal_entry_event_code",
                schema: "ledger",
                table: "journal_entry");

            migrationBuilder.AlterColumn<string>(
                name: "event_code",
                schema: "ledger",
                table: "journal_entry",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "uq_posting_identity",
                schema: "ledger",
                table: "journal_entry",
                columns: new[] { "company_id", "source_doc_type", "source_doc_id", "posting_trigger_code", "posting_generation" },
                unique: true);

            // ملاحظة صريحة: التراجع يُعيد شكل المخطّط ولا يُعيد نصّ الدالة القديم.
            // والنتيجة بعد التراجع أن الحدث الثاني يُرفض بانتهاك فهرس فريد —
            // ضياع **صاخب** لا صامت. وهذا مقصود: التراجع لا يُعيد تركيب العطب.
        }
    }
}
