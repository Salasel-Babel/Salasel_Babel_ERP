using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babel.Ledger.Persistence.Migrations
{
    /// <summary>
    /// الترجمات صفوف لا أعمدة، والاسم العربي سجلٌّ لا يكون فارغاً (ADR-0021 بند 2 و3).
    /// <para>
    /// كان لكل كيان مرجعي في الدفتر عمودان ثابتان <c>name_ar</c> و<c>name_en</c>. والعمودان
    /// <b>عاجزان بنيوياً</b> عن لغة ثالثة: محاسبٌ أردي أو هندي — وسوق العمل السعودي مليء
    /// بهما — يرى السجلّ ومعه ترجمة إنجليزية لا ترجمةً بلغته، ولا علاج لذلك إلا <b>هجرة
    /// مخطّط وإصدار برمجي لكل لغة تُضاف</b>. وبعد هذه الهجرة: اللغة الخامسة صفوفُ إدخال.
    /// </para>
    /// <para>
    /// <b>لماذا العربي يبقى عموداً ولا ينزل هو أيضاً إلى الجدول:</b> لأنه ليس ترجمة. النظام
    /// السعودي يوجب مسك الدفاتر بالعربية، فالاسم العربي هو <b>شكل السجلّ القانوني</b> وهو
    /// مرجع الارتداد الوحيد حين لا ترجمة. وإنزاله صفّاً يجعل غيابه ممكناً — أي يجعل
    /// «حسابٌ بلا اسم» حالةً قابلة للتمثيل، وهي بالضبط ما تمنعه قيود
    /// <c>ck_*_name_ar_not_blank</c> المضافة هنا.
    /// </para>
    /// <para>
    /// <b>وترتيب الخطوات هو الهجرة نفسها.</b> ما ولّدته الأداة كان: أسقط الأعمدة، ثم أنشئ
    /// الجدول — وهو على قاعدة بيانات عامرة <b>إتلافٌ صامت</b> لكل اسم إنجليزي مكتوب. فالترتيب
    /// هنا مقلوب عمداً: يُنشأ الجدول، ثم <b>تُنقَل القيم</b>، ثم يُتحقَّق من أن العدد المنقول
    /// يطابق العدد الأصلي — <b>وتتوقّف الهجرة برسالة تسمّي الفرق إن لم يطابق</b> — ثم تُسقط
    /// الأعمدة. والقيمة الفارغة لا تُنقل: غياب الترجمة يُعبَّر عنه بغياب الصفّ، لا بصفٍّ فارغ
    /// يُعرض عموداً بلا عنوان.
    /// </para>
    /// <para>
    /// <b>وما لا تفعله:</b> لا تمسّ <c>ledger.journal_entry</c> ولا <c>ledger.journal_line</c>
    /// ولا <c>ledger.chain_link</c> بحرف. حقول <c>memo_ar</c> و<c>description</c> و
    /// <c>description_ar</c> و<c>reversal_reason_*</c> <b>داخل البايتات المُجزَّأة</b> في
    /// الشكل القانوني v2، وهي <b>وقائع مسجَّلة يغطّيها التوقيع لا ترجماتٍ للعرض</b>. نقلُ
    /// أيٍّ منها يغيّر الشكل القانوني، و v1 مجمَّد بمتّجهات ذهبية و v2 حيّ — فذلك إصدار ثالث،
    /// وليس هذه الهجرة.
    /// </para>
    /// <para>
    /// <b>والتراجع يعيد البيانات لا الأعمدة وحدها:</b> <c>Down</c> يُنشئ الأعمدة ثم يُعيد
    /// تعبئتها من صفوف <c>en</c> قبل إسقاط الجدول. وهو مع ذلك <b>تراجع خاسر معلَن</b>: كل
    /// لغة غير الإنجليزية تُفقَد، لأن المخطّط القديم لا موضع فيه لها — وهذا هو نصّ المشكلة
    /// التي جاءت هذه الهجرة لتحلّها.
    /// </para>
    /// </summary>
    internal partial class TranslationsAreRowsNotColumns : Migration
    {
        private const string GlobalScope = "'00000000-0000-0000-0000-000000000000'::uuid";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            // ── ١) الجدول أولاً، قبل أن يُسقَط عمودٌ واحد ────────────────────
            migrationBuilder.CreateTable(
                name: "name_translation",
                schema: "ledger",
                columns: table => new
                {
                    company_id = table.Column<System.Guid>(type: "uuid", nullable: false),
                    entity_kind = table.Column<string>(type: "text", nullable: false),
                    entity_key = table.Column<string>(type: "text", nullable: false),
                    language_tag = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_name_translation", x => new { x.company_id, x.entity_kind, x.entity_key, x.language_tag });
                    table.CheckConstraint("ck_name_translation_key_not_blank", "length(btrim(entity_key)) > 0");
                    table.CheckConstraint("ck_name_translation_kind", "entity_kind in ('account','fiscal_period','posting_role','property')");
                    table.CheckConstraint("ck_name_translation_name_not_blank", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_name_translation_not_arabic", "lower(language_tag) <> 'ar' and lower(language_tag) not like 'ar-%'");
                    table.CheckConstraint("ck_name_translation_scope", "(entity_kind = 'posting_role') = (company_id = '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_name_translation_tag_shape", "language_tag ~ '^[A-Za-z][A-Za-z0-9]*(-[A-Za-z0-9]+)*$' and length(language_tag) <= 35");
                });

            migrationBuilder.CreateIndex(
                name: "ix_name_translation_lookup",
                schema: "ledger",
                table: "name_translation",
                columns: ["company_id", "entity_kind", "language_tag"]);

            // ── ٢) نقل القيم القائمة، ثم التحقّق من أن شيئاً لم يسقط ────────
            // الفارغ لا يُنقل: غياب الترجمة صفٌّ غائب لا صفٌّ فارغ. والعدّ يقارن
            // «ما كان غير فارغ» بـ«ما وصل»، فلا يُخفي الفارغُ فقداناً حقيقياً.
            migrationBuilder.Sql($"""
                do $move$
                declare
                    v_expected bigint;
                    v_moved    bigint;
                begin
                    select (select count(*) from ledger.account          where btrim(name_en) <> '')
                         + (select count(*) from ledger.posting_role     where btrim(name_en) <> '')
                         + (select count(*) from ledger.property_dimension where btrim(name_en) <> '')
                         + (select count(*) from ledger.fiscal_period    where btrim(name_en) <> '')
                      into v_expected;

                    insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                    select company_id, 'account', account_code, 'en', btrim(name_en)
                      from ledger.account where btrim(name_en) <> ''
                    on conflict do nothing;

                    insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                    select {GlobalScope}, 'posting_role', role_code, 'en', btrim(name_en)
                      from ledger.posting_role where btrim(name_en) <> ''
                    on conflict do nothing;

                    insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                    select company_id, 'property', property_id, 'en', btrim(name_en)
                      from ledger.property_dimension where btrim(name_en) <> ''
                    on conflict do nothing;

                    insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                    select company_id, 'fiscal_period', period_code, 'en', btrim(name_en)
                      from ledger.fiscal_period where btrim(name_en) <> ''
                    on conflict do nothing;

                    select count(*) into v_moved from ledger.name_translation where language_tag = 'en';

                    if v_moved <> v_expected then
                        raise exception
                            'TRANSLATION_MOVE_INCOMPLETE expected=% moved=% : اسمٌ إنجليزي واحد على الأقل لم يصل جدول الترجمات، والأعمدة لن تُسقط / at least one English name did not reach the translation table; the columns are not dropped',
                            v_expected, v_moved;
                    end if;
                end
                $move$;
                """);

            // ── ٣) السجلّ العربي يُثبَت غير فارغ — والصفّ المخالف يُسمّى ─────
            // القيد وحده يرمي رسالة PostgreSQL خام لا تقول أي صفّ. فالفحص هنا أولاً،
            // ليقرأ من يُشغّل الهجرة **ما الذي يُصلحه** لا أن الهجرة فشلت.
            migrationBuilder.Sql("""
                do $names$
                declare v_bad text;
                begin
                    select string_agg(entry, ' · ' order by entry) into v_bad from (
                        select 'account ' || company_id || '/' || account_code as entry
                          from ledger.account where length(btrim(name_ar)) = 0
                        union all
                        select 'posting_role ' || role_code
                          from ledger.posting_role where length(btrim(name_ar)) = 0
                        union all
                        select 'property ' || company_id || '/' || property_id
                          from ledger.property_dimension where length(btrim(name_ar)) = 0
                        union all
                        select 'fiscal_period ' || company_id || '/' || period_code
                          from ledger.fiscal_period where length(btrim(name_ar)) = 0
                    ) as offenders;

                    if v_bad is not null then
                        raise exception
                            'ARABIC_NAME_BLANK rows=% : الاسم العربي هو السجلّ ومرجع الارتداد، ولا يُخترَع له بديل — تُصحَّح هذه الصفوف قبل الهجرة / the Arabic name is the record and the sole fallback; no substitute is invented for it, so these rows are corrected before migrating',
                            v_bad;
                    end if;
                end
                $names$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_account_name_ar_not_blank",
                schema: "ledger",
                table: "account",
                sql: "length(btrim(name_ar)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_posting_role_name_ar_not_blank",
                schema: "ledger",
                table: "posting_role",
                sql: "length(btrim(name_ar)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_property_name_ar_not_blank",
                schema: "ledger",
                table: "property_dimension",
                sql: "length(btrim(name_ar)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fiscal_period_name_ar_not_blank",
                schema: "ledger",
                table: "fiscal_period",
                sql: "length(btrim(name_ar)) > 0");

            // ── ٤) وأخيراً تُسقط الأعمدة، وقد صارت قيمها في مأمن ────────────
            migrationBuilder.DropColumn(name: "name_en", schema: "ledger", table: "account");
            migrationBuilder.DropColumn(name: "name_en", schema: "ledger", table: "posting_role");
            migrationBuilder.DropColumn(name: "name_en", schema: "ledger", table: "property_dimension");
            migrationBuilder.DropColumn(name: "name_en", schema: "ledger", table: "fiscal_period");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            System.ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropCheckConstraint(name: "ck_account_name_ar_not_blank", schema: "ledger", table: "account");
            migrationBuilder.DropCheckConstraint(name: "ck_posting_role_name_ar_not_blank", schema: "ledger", table: "posting_role");
            migrationBuilder.DropCheckConstraint(name: "ck_property_name_ar_not_blank", schema: "ledger", table: "property_dimension");
            migrationBuilder.DropCheckConstraint(name: "ck_fiscal_period_name_ar_not_blank", schema: "ledger", table: "fiscal_period");

            foreach (string table in new[] { "account", "posting_role", "property_dimension", "fiscal_period" })
            {
                migrationBuilder.AddColumn<string>(
                    name: "name_en",
                    schema: "ledger",
                    table: table,
                    type: "text",
                    nullable: false,
                    defaultValue: "");
            }

            // التراجع يُعيد الإنجليزية إلى عمودها قبل إسقاط الجدول. وما سواها يُفقَد،
            // ولا موضع له في المخطّط القديم — وهذا الفقد معلَن لا صامت.
            migrationBuilder.Sql("""
                update ledger.account a set name_en = t.name
                  from ledger.name_translation t
                 where t.company_id = a.company_id and t.entity_kind = 'account'
                   and t.entity_key = a.account_code and t.language_tag = 'en';

                update ledger.posting_role r set name_en = t.name
                  from ledger.name_translation t
                 where t.entity_kind = 'posting_role' and t.entity_key = r.role_code and t.language_tag = 'en';

                update ledger.property_dimension p set name_en = t.name
                  from ledger.name_translation t
                 where t.company_id = p.company_id and t.entity_kind = 'property'
                   and t.entity_key = p.property_id and t.language_tag = 'en';

                update ledger.fiscal_period f set name_en = t.name
                  from ledger.name_translation t
                 where t.company_id = f.company_id and t.entity_kind = 'fiscal_period'
                   and t.entity_key = f.period_code and t.language_tag = 'en';
                """);

            migrationBuilder.DropTable(name: "name_translation", schema: "ledger");
        }
    }
}
