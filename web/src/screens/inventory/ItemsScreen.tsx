/* ═══════════════════════════════════════════════════════════════════════════
   الأصناف ووحداتها — حيث تصير «وحدات القياس المتعدّدة» مقروءة
   Items and their units — where multi-unit-of-measure becomes legible
   ───────────────────────────────────────────────────────────────────────────
   هذه الشاشة تحمل القدرة المميِّزة الأولى للقسم المخزني. وأربعة قرارات
   تحكمها، وكلّها مقيسة على العقد لا مفترَضة:

   ١ · **المعامل نسبةٌ لا عدد عشري.** العقد ينشره بسطاً ومقاماً صحيحين، وهذه
       الشاشة تعرضه كذلك وتُدخله كذلك. «الحبّة ثلث علبة» = 1/3، ولا يُمثَّل
       عشرياً بلا خسارة — والخسارة في كمّيةٍ تُضرب في تكلفة الوحدة تصل إلى
       المال. فلا يوجد في هذا الملفّ حقلٌ عشري لمعامل، ولا قسمةٌ واحدة.

   ٢ · **الصنف بلا وحدةٍ أكبر يُعلَن، لا يُترك خانةً فارغة.** «وحدة الأساس
       وحدها» جملةٌ تقول للمستخدم ما الذي سيقع إن أرسل كمّيةً بوحدةٍ أخرى:
       رفضٌ بالرمز `inventory.unit_not_convertible`. والفراغ الذي لا يُشرَح
       يُقرأ نقصاً في البيانات، وهو هنا **قرارٌ في الكتالوج**.

   ٣ · **ولا تحويل يقع في هذه الشاشة.** المتصفّح لا يحوّل كمّيةً من وحدةٍ إلى
       أخرى ولو ملك المعامل: التحويل قرارٌ عشري تملكه وحدة المخزون، وهي وحدها
       تعرف متى **لا يقع بلا باقٍ** فترفض بدل أن تقرّب. وحسابٌ ثانٍ هنا كان
       سيصير مصدر حقيقةٍ ثانياً ينحرف عن الأول بصمت.

   ٤ · **البسط والمقام عددان صحيحان يعبران السلك أعداداً.** وهذا ليس خرقاً
       لقاعدة «المال والكمّية نصوص»: العقد ينشرهما `integer` بحدٍّ أقصى
       ١٬٠٠٠٬٠٠٠٬٠٠٠ — وهو **دقيقٌ تماماً** في عائم مزدوج، بخلاف العشري.
       ومع ذلك يُتحقَّق من شكلهما بنمطٍ نصّي **قبل** أي تحويل، فلا يمرّ
       «12.5» ولا «1e3» ولا فراغ.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { addItem, listItems } from "../../api/generated/client";
import type { Item, ItemRequest, UnitFactor } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Button, EmptyState, MOTION, Panel, useMoment } from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, RefusalNextStep } from "./shared";

/* نمطُ العدد الصحيح الموجب في حدود العقد: من ١ إلى ١٬٠٠٠٬٠٠٠٬٠٠٠.
   نصّيٌّ عمداً — يُفحص الشكل قبل التحويل، لا بعده. */
const POSITIVE_INTEGER_RE = /^(?:[1-9][0-9]{0,8}|1000000000)$/;

/** سطر وحدةٍ أكبر كما يُحرَّر في النموذج — العددان **نصّان** حتى الإرسال. */
interface DraftUnit {
  key: string;
  unitCode: string;
  numerator: string;
  denominator: string;
}

let sequence = 0;
function newUnit(): DraftUnit {
  sequence += 1;
  return { key: "u" + String(sequence), unitCode: "", numerator: "", denominator: "1" };
}

/* ═══════════════════════════════════════════════ سلّم الوحدات معروضاً */

/**
 * سلّم وحدات صنفٍ: وحدة الأساس أوّلاً، ثم كل وحدةٍ أكبر بنسبتها.
 * @param props وحدة الأساس والوحدات الأكبر.
 */
function UnitLadder(props: { baseUnit: string; units: readonly UnitFactor[] }): ReactNode {
  const { t } = useT();
  return (
    <div className="uladder">
      <span className="uchip" data-base="true">
        <span className="uchip__u">{props.baseUnit}</span>
      </span>
      {props.units.map((unit) => (
        <span className="uchip" key={unit.unitCode}>
          <span className="uchip__u">{unit.unitCode}</span>
          <span className="uchip__r" dir="ltr">
            {String(unit.numerator) + "/" + String(unit.denominator)}
          </span>
        </span>
      ))}
      {props.units.length === 0 ? (
        <span className="uladder__none">{t("inventory.items.baseOnly")}</span>
      ) : null}
    </div>
  );
}

/* ═══════════════════════════════════════════════════ تفصيل الصنف المختار */

/**
 * لوح الصنف المختار: سلّمه كاملاً، وما يعنيه كل معامل بالكلمات.
 * @param props الصنف.
 */
function ItemDetail(props: { item: Item }): ReactNode {
  const { t } = useT();
  const { item } = props;
  return (
    <Panel
      title={t("inventory.items.selected")}
      note={t("inventory.items.ladderNote")}
      aside={<span className="pill pill--info">{item.code}</span>}
      testId="item-detail"
    >
      <div className="kv">
        <div>
          <div className="k">{t("inventory.items.colName")}</div>
          <div className="v" lang="ar" dir="rtl">{item.name.ar}</div>
        </div>
        <div>
          <div className="k">{t("inventory.items.nameEn")}</div>
          <div className="v" lang="en" dir="ltr">{item.name.en}</div>
        </div>
        <div>
          <div className="k">{t("inventory.items.colGroup")}</div>
          <div className="v mono" dir="ltr">{item.itemGroup}</div>
        </div>
        <div>
          <div className="k">{t("inventory.items.colBase")}</div>
          <div className="v mono">{item.baseUnit}</div>
        </div>
      </div>

      {item.units.length === 0 ? (
        <p className="alert alert--info" role="status" data-testid="item-base-only">
          {t("inventory.items.baseOnlyWhy")}
        </p>
      ) : (
        <div className="ledger" data-state="ready" data-testid="item-ladder">
          <table>
            <caption className="visually-hidden">{t("inventory.items.ladder")}</caption>
            <thead>
              <tr>
                <th scope="col">{t("inventory.items.colUnitCode")}</th>
                <th scope="col" className="n">{t("inventory.items.colNumerator")}</th>
                <th scope="col" className="n">{t("inventory.items.colDenominator")}</th>
                <th scope="col">{t("inventory.items.colMeans")}</th>
              </tr>
            </thead>
            <tbody>
              {item.units.map((unit) => (
                <tr key={unit.unitCode}>
                  <td className="code">{unit.unitCode}</td>
                  <td className="n mono" dir="ltr">{String(unit.numerator)}</td>
                  <td className="n mono" dir="ltr">{String(unit.denominator)}</td>
                  <td>
                    {t(
                      unit.denominator === 1 ? "inventory.items.meansOne" : "inventory.items.means",
                      {
                        unit: unit.unitCode,
                        base: item.baseUnit,
                        numerator: String(unit.numerator),
                        denominator: String(unit.denominator),
                      }
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <p className="muted">{t("inventory.items.gapEdit")}</p>
    </Panel>
  );
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة الأصناف ووحداتها. */
export function InventoryItemsScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const [arrived, fireArrive] = useMoment("arrive");

  /* حقول التسجيل. */
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [group, setGroup] = useState("");
  const [baseUnit, setBaseUnit] = useState("");
  const [units, setUnits] = useState<DraftUnit[]>([]);
  const [created, setCreated] = useState<Item | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const result = useQuery({
    queryKey: ["inventory-items", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listItems(transport, { companyId: config.companyId }, signal),
  });

  const items: readonly Item[] = useMemo(() => result.data?.items ?? [], [result.data]);

  /* بحثٌ نصّي على الرمز والاسمين — بلا ترتيبٍ ثقافي: الترتيب حرفيٌّ ثابت
     يأتي من الخادم، والمرشّح هنا لا يعيد ترتيباً. */
  const shown = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    if (!needle) return items;
    return items.filter(
      (item) =>
        item.code.toLocaleLowerCase().includes(needle) ||
        item.name.ar.toLocaleLowerCase().includes(needle) ||
        item.name.en.toLocaleLowerCase().includes(needle) ||
        item.itemGroup.toLocaleLowerCase().includes(needle)
    );
  }, [items, query]);

  const chosen = useMemo(
    () => items.find((item) => item.id === selected) ?? null,
    [items, selected]
  );

  const badRatios = useMemo(
    () =>
      units
        .filter(
          (unit) =>
            !POSITIVE_INTEGER_RE.test(unit.numerator) || !POSITIVE_INTEGER_RE.test(unit.denominator)
        )
        .map((unit) => unit.key),
    [units]
  );

  const ready =
    code !== "" &&
    nameAr !== "" &&
    nameEn !== "" &&
    group !== "" &&
    baseUnit !== "" &&
    units.every((unit) => unit.unitCode !== "") &&
    badRatios.length === 0;

  const update = useCallback((key: string, patch: Partial<DraftUnit>) => {
    setUnits((current) => current.map((u) => (u.key === key ? { ...u, ...patch } : u)));
  }, []);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      /* التحويل الوحيد إلى عدد في هذا الملفّ، وهو **بعد** فحص الشكل بنمطٍ
         نصّي: العقد ينشر البسط والمقام `integer` بحدٍّ يقع كاملاً داخل
         المدى الدقيق للعائم المزدوج، بخلاف المال والكمّية. */
      const wireUnits: UnitFactor[] = units.map((unit) => ({
        unitCode: unit.unitCode,
        numerator: Number(unit.numerator),
        denominator: Number(unit.denominator),
      }));
      const body: ItemRequest = {
        code,
        name: { ar: nameAr, en: nameEn },
        itemGroup: group,
        baseUnit,
        units: wireUnits,
      };
      const item = await addItem(transport, { companyId: config.companyId, body });
      setCreated(item);
      setSelected(item.id);
      fireArrive();
      await result.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [baseUnit, code, config.companyId, fireArrive, group, nameAr, nameEn, result, transport, units]);

  const startAnother = useCallback(() => {
    setCreated(null);
    setError(null);
    setCode("");
    setNameAr("");
    setNameEn("");
    setGroup("");
    setBaseUnit("");
    setUnits([]);
  }, []);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="inventory-items-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.items.title")}</h1>
          <p className="sub">{t("inventory.items.lede")}</p>
        </div>
      </header>

      <div className="statline">
        {result.data ? (
          <span className={"pill " + arrived} data-testid="item-count">
            {tp("inventory.items.count", result.data.itemCount)}
          </span>
        ) : null}
        <span className="spacer" />
        <div className="inline-group">
          <Button
            label={t("common.action.refresh")}
            onClick={() => void result.refetch()}
            testId="items-reload"
          />
        </div>
      </div>

      <div className="filterbar" role="search">
        <div className="field wide">
          <label htmlFor="inv-item-search">{t("inventory.items.search")}</label>
          <input
            id="inv-item-search"
            className="ctl"
            type="search"
            data-testid="items-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("inventory.items.searchPh")}
          />
        </div>
      </div>

      {result.isPending && result.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {result.isError ? (
        <ProblemPanel error={result.error} onRetry={() => void result.refetch()} />
      ) : null}

      {result.data && items.length === 0 ? (
        <EmptyState
          title={t("inventory.items.emptyTitle")}
          body={t("inventory.items.emptyBody")}
          testId="items-empty"
        />
      ) : null}

      {result.data && items.length > 0 && shown.length === 0 ? (
        <EmptyState
          small
          title={t("inventory.items.noMatchTitle")}
          body={t("inventory.items.noMatchBody")}
          action={<Button label={t("common.action.clearSearch")} onClick={() => setQuery("")} />}
          testId="items-no-match"
        />
      ) : null}

      {shown.length > 0 ? (
        <Panel
          title={t("inventory.items.title")}
          note={t("inventory.items.selectHint")}
          testId="items-panel"
        >
          <div className="ledger" data-state="ready" data-testid="items-table">
            <table>
              <caption className="visually-hidden">{t("inventory.items.title")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.items.colCode")}</th>
                  <th scope="col">{t("inventory.items.colName")}</th>
                  <th scope="col">{t("inventory.items.colGroup")}</th>
                  <th scope="col">{t("inventory.items.colBase")}</th>
                  <th scope="col">{t("inventory.items.colUnits")}</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((item) => (
                  <tr
                    key={item.id}
                    data-testid="item-row"
                    data-selected={item.id === selected ? "true" : undefined}
                    className={created && created.id === item.id ? MOTION.arrive : undefined}
                  >
                    <td className="code">
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm mono"
                        data-testid="item-pick"
                        aria-pressed={item.id === selected}
                        onClick={() => setSelected(item.id)}
                      >
                        {item.code}
                      </button>
                    </td>
                    <td>
                      <span lang="ar" dir="rtl">{item.name.ar}</span>
                      <span className="alt" lang="en" dir="ltr">{item.name.en}</span>
                    </td>
                    <td className="code">{item.itemGroup}</td>
                    <td className="mono">{item.baseUnit}</td>
                    <td>
                      <UnitLadder baseUnit={item.baseUnit} units={item.units} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      ) : null}

      {chosen ? <ItemDetail item={chosen} /> : null}

      {created ? (
        <section
          className={"alert alert--success " + arrived}
          role="status"
          data-testid="item-created"
        >
          <h2 style={{ marginTop: 0 }}>{t("inventory.items.created")}</h2>
          <p>{t("inventory.items.createdBody")}</p>
          <div className="inline-group">
            <Button
              label={t("inventory.items.another")}
              kind="primary"
              onClick={startAnother}
              testId="item-another"
            />
          </div>
        </section>
      ) : null}

      <Panel
        title={t("inventory.items.add")}
        note={t("inventory.items.addNote")}
        testId="item-form"
      >
        <div className="grid fields-3">
          <div className="field">
            <label htmlFor="inv-code">{t("inventory.items.code")}</label>
            <input
              id="inv-code"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="item-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
            />
            <span className="hint">{t("inventory.items.codeHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="inv-group">{t("inventory.items.group")}</label>
            <input
              id="inv-group"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="item-group"
              value={group}
              onChange={(e) => setGroup(e.target.value)}
            />
            <span className="hint">{t("inventory.items.groupHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="inv-base">{t("inventory.items.baseUnit")}</label>
            <input
              id="inv-base"
              className="ctl mono"
              autoComplete="off"
              data-testid="item-base"
              value={baseUnit}
              onChange={(e) => setBaseUnit(e.target.value)}
            />
            <span className="hint">{t("inventory.items.baseUnitHint")}</span>
          </div>
        </div>

        <div className="grid fields-half" style={{ marginTop: "var(--space-12)" }}>
          <div className="field">
            <label htmlFor="inv-name-ar">{t("inventory.items.nameAr")}</label>
            <input
              id="inv-name-ar"
              className="ctl"
              lang="ar"
              data-testid="item-name-ar"
              value={nameAr}
              onChange={(e) => setNameAr(e.target.value)}
            />
            <span className="hint">{t("inventory.items.nameHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="inv-name-en">{t("inventory.items.nameEn")}</label>
            <input
              id="inv-name-en"
              className="ctl"
              lang="en"
              dir="ltr"
              data-testid="item-name-en"
              value={nameEn}
              onChange={(e) => setNameEn(e.target.value)}
            />
          </div>
        </div>

        <h3 className="card-hd" style={{ marginTop: "var(--space-16)" }}>
          <strong>{t("inventory.items.ladder")}</strong>
        </h3>
        <p className="muted">{t("inventory.items.numeratorHint")}</p>

        {units.length === 0 ? (
          <p className="muted" data-testid="item-no-units">{t("inventory.items.noUnits")}</p>
        ) : null}

        <div className="stack">
          {units.map((unit) => (
            <fieldset key={unit.key} className="card card-pad" data-testid="item-unit-row">
              <div className="grid fields-4">
                <div className="field">
                  <label htmlFor={"inv-u-" + unit.key}>{t("inventory.items.unitCode")}</label>
                  <input
                    id={"inv-u-" + unit.key}
                    className="ctl mono"
                    autoComplete="off"
                    data-testid="item-unit-code"
                    value={unit.unitCode}
                    onChange={(e) => update(unit.key, { unitCode: e.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor={"inv-n-" + unit.key}>{t("inventory.items.numerator")}</label>
                  <input
                    id={"inv-n-" + unit.key}
                    className={"ctl mono" + (badRatios.includes(unit.key) ? " is-invalid" : "")}
                    dir="ltr"
                    inputMode="numeric"
                    autoComplete="off"
                    aria-invalid={badRatios.includes(unit.key)}
                    data-testid="item-unit-numerator"
                    value={unit.numerator}
                    onChange={(e) => update(unit.key, { numerator: e.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor={"inv-d-" + unit.key}>{t("inventory.items.denominator")}</label>
                  <input
                    id={"inv-d-" + unit.key}
                    className={"ctl mono" + (badRatios.includes(unit.key) ? " is-invalid" : "")}
                    dir="ltr"
                    inputMode="numeric"
                    autoComplete="off"
                    aria-invalid={badRatios.includes(unit.key)}
                    data-testid="item-unit-denominator"
                    value={unit.denominator}
                    onChange={(e) => update(unit.key, { denominator: e.target.value })}
                  />
                </div>
                <div className="field">
                  <Button
                    label={t("inventory.items.removeUnit")}
                    kind="danger"
                    size="sm"
                    onClick={() => setUnits((c) => c.filter((u) => u.key !== unit.key))}
                  />
                </div>
              </div>
            </fieldset>
          ))}
        </div>

        <button
          type="button"
          className="addline"
          data-testid="item-add-unit"
          onClick={() => setUnits((c) => [...c, newUnit()])}
        >
          {t("inventory.items.addUnit")}
        </button>

        <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
          <Button
            label={busy ? t("common.state.loading") : t("inventory.items.submit")}
            kind="primary"
            disabled={!ready || busy}
            loading={busy}
            onClick={() => void submit()}
            testId="item-submit"
          />
        </div>
      </Panel>

      {error ? (
        <>
          <ProblemPanel error={error} />
          <RefusalNextStep error={error} />
        </>
      ) : null}
    </section>
  );
}
