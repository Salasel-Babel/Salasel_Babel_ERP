/* ═══════════════════════════════════════════════════════════════════════════
   جدول ميزان المراجعة — الشيء الذي يعيش عليه هذا المنتج أو يموت
   ───────────────────────────────────────────────────────────────────────────
   خمسة قرارات، وكلها لصالح المحاسب لا لصالح الشيفرة:

   ١ · لوحة المفاتيح أولاً. المحاسب يكتب ولا ينقر: سطر نشط ينتقل بالأسهم
       وبـj/k، وHome/End وPgUp/PgDn، و«/» للبحث، وv لتدوير العرض، وr لإعادة
       القراءة. والسطر النشط يُمرَّر إلى المنظور تلقائياً.

   ٢ · لا حساب على المال في المتصفّح. المجموعان يصلان من الخادم محسوبَين
       بـsum() على numeric؛ والمعروض هنا **صفوف**، والفرز مقارنةٌ عشرية نصّية.

   ٣ · الأرقام معزولة: كل خانة direction:ltr + unicode-bidi:isolate +
       text-align:end + tabular-nums، فتقع الفاصلة تحت أختها في اللغات الأربع.

   ٤ · الهوية لا النصّ. مرشّح العرض data-view="all|debit|credit" ولا يُقارَن
       بنصّ زرّ — نصّ الزرّ يتغيّر بتغيّر اللغة، والهوية لا تتغيّر.

   ٥ · الحركة ثانوية ولا تؤخّر إدخالاً: لا انتقال على أي شيء يُكتب فيه.
   ═══════════════════════════════════════════════════════════════════════════ */
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactNode,
} from "react";
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import type { TrialBalance, TrialBalanceRow } from "../../api/generated/types";
import { Amount, useLocale, useT } from "../../i18n/react";
import { toLatinDigits } from "../../i18n/decimal-text";
import { SOURCE } from "../../i18n/engine";

/** أي الصفوف تُعرض. هوية ثابتة لا تتغيّر بتغيّر اللغة. */
export type ViewFilter = "all" | "debit" | "credit";

const VIEW_ORDER: readonly ViewFilter[] = ["all", "debit", "credit"];

/* ═══ الاسم: أيّهما السجلّ وأيّهما ترجمة ═══════════════════════════════════
   ADR-0021: العربية هي **السجلّ** لا «اللغة الأولى»، والترجمة عرضٌ. ولذلك
   الاسم العربي يُعرَض دائماً في عمود الحساب مهما كانت لغة الواجهة، وتُعرَض
   الترجمة المتاحة تحته حين تختلف لغة الواجهة عن لغة السجلّ.

   ولا شرط على "en" في أي موضع هنا: الإنجليزية ليست «اللغة الأخرى» بل واحدة
   من N. والشرط الوحيد هو «هل لغة الواجهة هي لغة السجلّ؟» — ويقرأ اسمها من
   الطبقة (SOURCE) لا من قائمة مكتوبة.

   ⚠ وهنا يظهر حدّ العقد: TrialBalanceRow يحمل حقلين ثابتين nameAr و nameEn
   ولا يستطيع التعبير عن لغة ثالثة. فالمحاسب الأردي أو الهندي يرى السجلّ
   ومعه ترجمة إنجليزية لا ترجمةً بلغته — وهو بالضبط ما يعالجه ADR-0021 §4
   (نقل العمودين إلى جدول ترجمات). مرفوع في التقرير. */

/** اسم الحساب كما هو في السجلّ — عربيّ دائماً. */
function recordName(row: TrialBalanceRow): string {
  return row.nameAr;
}

/** الترجمة المتاحة في العقد اليوم، وهي واحدة. */
function translatedName(row: TrialBalanceRow): string {
  return row.nameEn;
}

/**
 * جدول الميزان.
 * @param props البيانات والمرشّحات.
 */
export function TrialBalanceTable(props: {
  data: TrialBalance;
  query: string;
  view: ViewFilter;
  onView: (v: ViewFilter) => void;
  searchRef: React.RefObject<HTMLInputElement | null>;
}): ReactNode {
  const { t, tp } = useT();
  const { i18n, locale } = useLocale();
  const [sorting, setSorting] = useState<SortingState>([{ id: "accountCode", desc: false }]);
  const [activeIndex, setActiveIndex] = useState(0);
  const bodyRef = useRef<HTMLTableSectionElement | null>(null);
  const wrapRef = useRef<HTMLDivElement | null>(null);

  /* المرشّح: البحث يطبّع الأرقام أولاً — رقم حساب مكتوب بأرقام عربية-هندية
     أو ديفاناغرية يجب أن يجد صاحبه. */
  const rows = useMemo(() => {
    const needle = toLatinDigits(props.query.trim().toLowerCase());
    return props.data.rows.filter((row) => {
      if (props.view === "debit" && row.debit.isZero) return false;
      if (props.view === "credit" && row.credit.isZero) return false;
      if (!needle) return true;
      const haystack =
        toLatinDigits(row.accountCode.toLowerCase()) +
        " " +
        recordName(row).toLowerCase() +
        " " +
        translatedName(row).toLowerCase();
      return haystack.includes(needle);
    });
  }, [props.data.rows, props.query, props.view]);

  const collator = i18n.collator(locale);
  /* لغة الواجهة تختلف عن لغة السجلّ ⇒ تُعرَض الترجمة المتاحة إلى جانبه. */
  const showTranslation = locale !== SOURCE;

  /* المقارنات مملوكة هنا لا مستعارة: كل واحدة تقول صراحةً بأي شيء تقارن.
     ولا واحدة منها تمرّ بـNumber على مبلغ. */
  const comparers = useMemo<Record<string, (a: TrialBalanceRow, b: TrialBalanceRow) => number>>(
    () => ({
      accountCode: (a, b) =>
        a.accountCode < b.accountCode ? -1 : a.accountCode > b.accountCode ? 1 : 0,
      /* الترتيب بترتيب اللغة النشطة، لا "ar" مثبّتة (design/README §٧٫٢-٦). */
      name: (a, b) => collator.compare(recordName(a), recordName(b)),
      /* مقارنة عشرية نصّية — لا Number ولا parseFloat على مبلغ. */
      debit: (a, b) => (a.debit).compare(b.debit),
      credit: (a, b) => (a.credit).compare(b.credit),
    }),
    [collator]
  );

  const columns = useMemo<ColumnDef<TrialBalanceRow>[]>(
    () => [
      {
        id: "accountCode",
        accessorFn: (row) => row.accountCode,
        header: () => t("field.accountNo.label"),
      },
      {
        id: "name",
        accessorFn: (row) => recordName(row),
        header: () => t("acct.columns.account"),
      },
      { id: "debit", accessorFn: (row) => row.debit, header: () => t("acct.debitCur") },
      { id: "credit", accessorFn: (row) => row.credit, header: () => t("acct.creditCur") },
    ],
    [t]
  );

  const sortedRows = useMemo(() => {
    const rule = sorting[0];
    if (!rule) return rows;
    const compare = comparers[rule.id];
    if (!compare) return rows;
    const out = [...rows].sort((a, b) => {
      const r = compare(a, b);
      return rule.desc ? -r : r;
    });
    return out;
  }, [comparers, rows, sorting]);

  const table = useReactTable({
    data: sortedRows,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    manualSorting: true,
    getCoreRowModel: getCoreRowModel(),
  });

  const total = sortedRows.length;
  const modelRows = table.getRowModel().rows;

  useEffect(() => {
    setActiveIndex((i) => (total === 0 ? 0 : Math.min(i, total - 1)));
  }, [total]);

  /* تمرير السطر النشط إلى المنظور — بلا حركة، فالتمرير لا يؤخّر ضغطة.
     ⚠ ولا يقع عند أول رسم: كان يقع، فتقفز الصفحة عند التحميل ويختفي شريط
     التنقّل فوق الطيّة عند 360 بكسل. مقيس بلقطة الشاشة، لا بالقراءة. */
  const navigated = useRef(false);
  useEffect(() => {
    if (!navigated.current) return;
    const body = bodyRef.current;
    if (!body) return;
    const row = body.children[activeIndex] as HTMLElement | undefined;
    row?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }, [activeIndex]);

  const move = useCallback(
    (delta: number | "first" | "last") => {
      navigated.current = true;
      setActiveIndex((i) => {
        if (total === 0) return 0;
        if (delta === "first") return 0;
        if (delta === "last") return total - 1;
        return Math.max(0, Math.min(total - 1, i + delta));
      });
    },
    [total]
  );

  /* الاختصارات العامّة. تُتجاهَل داخل حقل إدخال إلا Escape — وإلا لم يستطع
     أحد كتابة حرف v في مربّع البحث. */
  useEffect(() => {
    const onKey = (e: globalThis.KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      const typing =
        !!target &&
        (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.isContentEditable);
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      if (e.key === "/" && !typing) {
        e.preventDefault();
        props.searchRef.current?.focus();
        props.searchRef.current?.select();
        return;
      }
      if (typing) return;
      switch (e.key) {
        case "ArrowDown":
        case "j":
          e.preventDefault();
          move(1);
          break;
        case "ArrowUp":
        case "k":
          e.preventDefault();
          move(-1);
          break;
        case "Home":
          e.preventDefault();
          move("first");
          break;
        case "End":
          e.preventDefault();
          move("last");
          break;
        case "PageDown":
          e.preventDefault();
          move(10);
          break;
        case "PageUp":
          e.preventDefault();
          move(-10);
          break;
        case "v": {
          e.preventDefault();
          const next = VIEW_ORDER[(VIEW_ORDER.indexOf(props.view) + 1) % VIEW_ORDER.length];
          if (next) props.onView(next);
          break;
        }
        default:
          break;
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [move, props, props.view]);

  const onTableKey = useCallback(
    (e: ReactKeyboardEvent<HTMLTableElement>) => {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
      }
    },
    []
  );

  return (
    <>
      <div className="statline" data-testid="row-summary">
        <span data-testid="matching-count">{tp("screen.trialBalance.matching", total)}</span>
        <span className="muted">{t("screen.trialBalance.sourceNote")}</span>
      </div>

      <div className="tablewrap tb-wrap" ref={wrapRef} data-testid="table-scroller">
        <table className="tb" onKeyDown={onTableKey} data-view={props.view}>
          <caption className="visually-hidden">{t("screen.trialBalance.title")}</caption>
          <thead>
            <tr>
              {table.getHeaderGroups()[0]?.headers.map((header) => {
                const numeric = header.column.id === "debit" || header.column.id === "credit";
                const sorted = header.column.getIsSorted();
                return (
                  <th
                    key={header.id}
                    scope="col"
                    className={
                      (numeric ? "n " : "start ") +
                      (header.column.id === "debit"
                        ? "h-debit"
                        : header.column.id === "credit"
                          ? "h-credit"
                          : "")
                    }
                    aria-sort={
                      sorted === "asc" ? "ascending" : sorted === "desc" ? "descending" : "none"
                    }
                  >
                    <button
                      type="button"
                      data-testid={"sort-" + header.column.id}
                      onClick={() =>
                        setSorting((old) => {
                          const current = old[0];
                          const desc =
                            current && current.id === header.column.id ? !current.desc : false;
                          return [{ id: header.column.id, desc }];
                        })
                      }
                    >
                      {flexRender(header.column.columnDef.header, header.getContext())}
                      <span aria-hidden="true">{sorted === "desc" ? "▾" : sorted === "asc" ? "▴" : "⇅"}</span>
                    </button>
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody ref={bodyRef}>
            {modelRows.map((row, index) => (
              <tr
                key={row.original.accountCode}
                data-active={index === activeIndex ? "true" : "false"}
                aria-selected={index === activeIndex}
                onClick={() => {
                  navigated.current = true;
                  setActiveIndex(index);
                }}
              >
                <td className="start">
                  <span className="acct-code">{row.original.accountCode}</span>
                </td>
                <td className="start name" title={recordName(row.original)}>
                  <span lang="ar" dir="rtl">
                    {recordName(row.original)}
                  </span>
                  {showTranslation ? (
                    <span className="alt" lang="en" dir="ltr">
                      {translatedName(row.original)}
                    </span>
                  ) : null}
                </td>
                <td className="n">
                  <Amount value={row.original.debit} />
                </td>
                <td className="n">
                  <Amount value={row.original.credit} />
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td className="start" colSpan={2} data-testid="totals-label">
                {t("acct.grandTotal")}
              </td>
              <td className="n d" data-testid="total-debit">
                <Amount value={props.data.totalDebit} />
              </td>
              <td className="n c" data-testid="total-credit">
                <Amount value={props.data.totalCredit} />
              </td>
            </tr>
          </tfoot>
        </table>
      </div>

      <p className="muted">{t("screen.trialBalance.totalsNote")}</p>
    </>
  );
}
