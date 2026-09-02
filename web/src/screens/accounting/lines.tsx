/* ═══════════════════════════════════════════════════════════════════════════
   سطور المستندات — تُكتب نصّاً، وتعبر نصّاً  ·  Document lines: text in, text out
   ───────────────────────────────────────────────────────────────────────────
   **كل رقمٍ هنا `string` من الحقل إلى السلك.** لا `number`، ولا `parseFloat`،
   ولا حسابٌ في المتصفّح — ولا حتى مجموعُ سطرٍ واحد. والسبب منشورٌ في العقد
   حرفاً: مجاميع المستند «تُحسب في الوحدة على السطر ثم تُجمع، ومجموعٌ يرسله
   العميل كان سيصير مصدر حقيقة ثانياً يستطيع أن ينحرف». فجدولُ السطور أدناه
   **بلا عمود مجموع** — وذلك ليس نقصاً: عمودٌ يجمع في المتصفّح يفتح الباب
   الذي أُغلق في العقد، ويعرض رقماً قد يخالف ما يُرحَّل.

   والقيمة تُخزَّن **كما كُتبت حرفاً بحرف**: «10.50» تبقى «10.50» ولا تصير
   «10.5». والتحويل الوحيد عند الحدّ: `Money.wire` و`asQuantity` و`asTaxRate`
   تتحقّق من النحو المنشور قبل أن يغادر النصّ، فلا يعبر ما يرفضه الخادم.

   ولا حسابَ ولا رمزَ حساب على أي سطر: السطر يحمل `itemGroup` — **مؤهّل
   دور** — والمصفوفة وحدها تحوّله إلى حساب.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { asQuantity, asTaxRate } from "../../api/generated/brands";
import type { PurchaseLine, SalesLine } from "../../api/generated/types";
import { Money } from "../../api/money";
import { useT } from "../../i18n/react";
import { AccField, AccRow, DropLineButton, isMoneyText, isQuantityText, isTaxRateText } from "./parts";
import { TAX_CLASSIFICATIONS } from "./contract";

/* ═══════════════════════════════════════════ ١ · سطرُ مبيعات مسوّدة */

/** سطر مبيعات كما يُكتب قبل أن يعبر — كلّه نصوص. */
export interface DraftSalesLine {
  descriptionAr: string;
  descriptionEn: string;
  itemGroup: string;
  quantity: string;
  unitPrice: string;
  discount: string;
  taxClassification: string;
  taxRate: string;
}

/** سطرٌ فارغ، وبتصنيفٍ ضريبيٍّ أوّلَ ما يُقترح — لا صفرٌ صامت. */
export function emptySalesLine(): DraftSalesLine {
  return {
    descriptionAr: "",
    descriptionEn: "",
    itemGroup: "",
    quantity: "",
    unitPrice: "",
    discount: "0",
    taxClassification: TAX_CLASSIFICATIONS[0] ?? "",
    taxRate: "",
  };
}

/** هل السطر جاهزٌ للعبور — بالنحو المنشور لا بتخمين؟ */
export function salesLineReady(line: DraftSalesLine): boolean {
  return (
    line.descriptionAr !== "" &&
    line.descriptionEn !== "" &&
    line.itemGroup !== "" &&
    isQuantityText(line.quantity) &&
    isMoneyText(line.unitPrice) &&
    isMoneyText(line.discount) &&
    line.taxClassification !== "" &&
    isTaxRateText(line.taxRate)
  );
}

/** يحوّل السطر إلى شكله على السلك. ويرمي إن خالف النحو — عند الحدّ لا بعده. */
export function toSalesLine(line: DraftSalesLine): SalesLine {
  return {
    description: { ar: line.descriptionAr, en: line.descriptionEn },
    discount: Money.wire(line.discount),
    itemGroup: line.itemGroup,
    quantity: asQuantity(line.quantity),
    taxClassification: line.taxClassification,
    taxRate: asTaxRate(line.taxRate),
    unitPrice: Money.wire(line.unitPrice),
  };
}

/* ═══════════════════════════════════════════ ٢ · سطرُ مشتريات مسوّدة */

/** سطر مشتريات كما يُكتب — ومعه صنفُه وواقعةُ استرداد ضريبته. */
export interface DraftPurchaseLine {
  descriptionAr: string;
  descriptionEn: string;
  itemGroup: string;
  itemId: string;
  quantity: string;
  unitPrice: string;
  taxClassification: string;
  taxRate: string;
  /** **واقعةٌ ضريبية عن السطر لا تُشتقّ من تصنيفه** — ولذلك تُسأل صراحةً. */
  taxRecoverable: string;
}

/** سطرٌ فارغ. */
export function emptyPurchaseLine(): DraftPurchaseLine {
  return {
    descriptionAr: "",
    descriptionEn: "",
    itemGroup: "",
    itemId: "",
    quantity: "",
    unitPrice: "",
    taxClassification: TAX_CLASSIFICATIONS[0] ?? "",
    taxRate: "",
    taxRecoverable: "true",
  };
}

/** هل السطر جاهزٌ للعبور؟ */
export function purchaseLineReady(line: DraftPurchaseLine): boolean {
  return (
    line.descriptionAr !== "" &&
    line.descriptionEn !== "" &&
    line.itemGroup !== "" &&
    line.itemId !== "" &&
    isQuantityText(line.quantity) &&
    isMoneyText(line.unitPrice) &&
    line.taxClassification !== "" &&
    isTaxRateText(line.taxRate)
  );
}

/** يحوّل السطر إلى شكله على السلك. */
export function toPurchaseLine(line: DraftPurchaseLine): PurchaseLine {
  return {
    description: { ar: line.descriptionAr, en: line.descriptionEn },
    itemGroup: line.itemGroup,
    itemId: line.itemId,
    quantity: asQuantity(line.quantity),
    taxClassification: line.taxClassification,
    taxRate: asTaxRate(line.taxRate),
    taxRecoverable: line.taxRecoverable === "true",
    unitPrice: Money.wire(line.unitPrice),
  };
}

/* ══════════════════════════════════════ ٣ · قائمة اقتراح التصنيف الضريبي
   **اقتراحٌ لا حصر**: العقد ينشر التصنيف نصّاً لا مجموعةً مغلقة «فلا قيد
   تحقّق واحد يُغلقه، وتضييقُه بعد نشره يفرض v2». فقائمةٌ مغلقة في المتصفّح
   كانت ستمنع ما يقبله الخادم. */

/** معرّف قائمة الاقتراح — واحدةٌ لكل الشاشات. */
export const TAX_CLASS_LIST = "acc-tax-classifications";

/** قائمةُ اقتراحٍ بالتصنيفات المستعمَلة اليوم. */
export function TaxClassificationOptions(): ReactNode {
  return (
    <datalist id={TAX_CLASS_LIST}>
      {TAX_CLASSIFICATIONS.map((value) => (
        <option key={value} value={value} />
      ))}
    </datalist>
  );
}

/* ═══════════════════════════════════════════ ٤ · محرّر سطر المبيعات */

/**
 * محرّر سطر مبيعات — صفّان مستويان من أربعة حقول.
 * @param props السطر ومحدّثه.
 */
export function SalesLineEditor(props: {
  readonly line: DraftSalesLine;
  readonly onChange: (next: DraftSalesLine) => void;
  readonly idPrefix: string;
}): ReactNode {
  const { t } = useT();
  const { line, onChange, idPrefix } = props;
  const set = (patch: Partial<DraftSalesLine>) => onChange({ ...line, ...patch });
  return (
    <>
      <TaxClassificationOptions />
      <AccRow cols={4} testId="acc-sales-line-row-1">
        <AccField
          id={idPrefix + "-desc-ar"}
          label={t("accounting.field.descriptionAr")}
          hint={t("accounting.field.descriptionArHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-desc-ar"}
            className="ctl"
            lang="ar"
            dir="rtl"
            autoComplete="off"
            data-testid="acc-line-desc-ar"
            value={line.descriptionAr}
            onChange={(e) => set({ descriptionAr: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-desc-en"}
          label={t("accounting.field.descriptionEn")}
          hint={t("accounting.field.descriptionEnHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-desc-en"}
            className="ctl"
            lang="en"
            dir="ltr"
            autoComplete="off"
            data-testid="acc-line-desc-en"
            value={line.descriptionEn}
            onChange={(e) => set({ descriptionEn: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-group"}
          label={t("accounting.field.itemGroup")}
          hint={t("accounting.field.itemGroupHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-group"}
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            spellCheck={false}
            data-testid="acc-line-group"
            value={line.itemGroup}
            onChange={(e) => set({ itemGroup: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-qty"}
          label={t("accounting.field.quantity")}
          hint={t("accounting.field.quantityHint")}
          error={
            line.quantity !== "" && !isQuantityText(line.quantity)
              ? t("accounting.field.quantityBad")
              : undefined
          }
          source="typed"
          required
        >
          <input
            id={idPrefix + "-qty"}
            className="ctl amt-input"
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            aria-invalid={line.quantity !== "" && !isQuantityText(line.quantity)}
            data-testid="acc-line-qty"
            value={line.quantity}
            onChange={(e) => set({ quantity: e.target.value })}
          />
        </AccField>
      </AccRow>
      <AccRow cols={4} testId="acc-sales-line-row-2">
        <AccField
          id={idPrefix + "-price"}
          label={t("accounting.field.unitPrice")}
          hint={t("accounting.field.unitPriceHint")}
          error={
            line.unitPrice !== "" && !isMoneyText(line.unitPrice)
              ? t("accounting.field.moneyBad")
              : undefined
          }
          source="typed"
          required
        >
          <input
            id={idPrefix + "-price"}
            className="ctl amt-input"
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            aria-invalid={line.unitPrice !== "" && !isMoneyText(line.unitPrice)}
            data-testid="acc-line-price"
            value={line.unitPrice}
            onChange={(e) => set({ unitPrice: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-discount"}
          label={t("accounting.field.discount")}
          hint={t("accounting.field.discountHint")}
          error={
            line.discount !== "" && !isMoneyText(line.discount)
              ? t("accounting.field.moneyBad")
              : undefined
          }
          source="typed"
          required
        >
          <input
            id={idPrefix + "-discount"}
            className="ctl amt-input"
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            aria-invalid={line.discount !== "" && !isMoneyText(line.discount)}
            data-testid="acc-line-discount"
            value={line.discount}
            onChange={(e) => set({ discount: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-taxclass"}
          label={t("accounting.field.taxClassification")}
          hint={t("accounting.field.taxClassificationHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-taxclass"}
            className="ctl mono"
            dir="ltr"
            list={TAX_CLASS_LIST}
            autoComplete="off"
            spellCheck={false}
            data-testid="acc-line-taxclass"
            value={line.taxClassification}
            onChange={(e) => set({ taxClassification: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-taxrate"}
          label={t("accounting.field.taxRate")}
          hint={t("accounting.field.taxRateHint")}
          error={
            line.taxRate !== "" && !isTaxRateText(line.taxRate)
              ? t("accounting.field.rateBad")
              : undefined
          }
          source="typed"
          required
        >
          <input
            id={idPrefix + "-taxrate"}
            className="ctl amt-input"
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            aria-invalid={line.taxRate !== "" && !isTaxRateText(line.taxRate)}
            data-testid="acc-line-taxrate"
            value={line.taxRate}
            onChange={(e) => set({ taxRate: e.target.value })}
          />
        </AccField>
      </AccRow>
    </>
  );
}

/**
 * جدولُ سطور المبيعات المكتوبة — **بلا عمود مجموع**، والسببُ في صدر الملفّ.
 * @param props السطور وإسقاطُ سطر.
 */
export function SalesLineTable(props: {
  readonly lines: readonly DraftSalesLine[];
  readonly onDrop: (index: number) => void;
}): ReactNode {
  const { t } = useT();
  return (
    <div className="acc-table" data-testid="acc-sales-lines">
      <table>
        <caption className="visually-hidden">{t("accounting.lines.title")}</caption>
        <thead>
          <tr>
            <th scope="col">{t("accounting.field.descriptionAr")}</th>
            <th scope="col">{t("accounting.field.itemGroup")}</th>
            <th scope="col" className="n">{t("accounting.field.quantity")}</th>
            <th scope="col" className="n">{t("accounting.field.unitPrice")}</th>
            <th scope="col" className="n">{t("accounting.field.discount")}</th>
            <th scope="col">{t("accounting.field.taxClassification")}</th>
            <th scope="col" className="n">{t("accounting.field.taxRate")}</th>
            <th scope="col">{t("accounting.field.action")}</th>
          </tr>
        </thead>
        <tbody>
          {props.lines.map((line, index) => (
            <tr key={index} data-testid={"acc-sales-line-" + String(index)}>
              <td>{line.descriptionAr}</td>
              <td><span className="mono acc-id">{line.itemGroup}</span></td>
              <td className="n"><span className="mono">{line.quantity}</span></td>
              <td className="n"><span className="mono">{line.unitPrice}</span></td>
              <td className="n"><span className="mono">{line.discount}</span></td>
              <td><span className="mono acc-id">{line.taxClassification}</span></td>
              <td className="n"><span className="mono">{line.taxRate}</span></td>
              <td>
                <DropLineButton
                  onClick={() => props.onDrop(index)}
                  testId={"acc-sales-line-drop-" + String(index)}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ═══════════════════════════════════════════ ٥ · محرّر سطر المشتريات */

/**
 * محرّر سطر مشتريات — ثلاثة صفوفٍ مستوية.
 * @param props السطر ومحدّثه.
 */
export function PurchaseLineEditor(props: {
  readonly line: DraftPurchaseLine;
  readonly onChange: (next: DraftPurchaseLine) => void;
  readonly idPrefix: string;
}): ReactNode {
  const { t } = useT();
  const { line, onChange, idPrefix } = props;
  const set = (patch: Partial<DraftPurchaseLine>) => onChange({ ...line, ...patch });
  return (
    <>
      <TaxClassificationOptions />
      <AccRow cols={3} testId="acc-purchase-line-row-1">
        <AccField
          id={idPrefix + "-desc-ar"}
          label={t("accounting.field.descriptionAr")}
          hint={t("accounting.field.descriptionArHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-desc-ar"}
            className="ctl"
            lang="ar"
            dir="rtl"
            autoComplete="off"
            data-testid="acc-pline-desc-ar"
            value={line.descriptionAr}
            onChange={(e) => set({ descriptionAr: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-desc-en"}
          label={t("accounting.field.descriptionEn")}
          hint={t("accounting.field.descriptionEnHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-desc-en"}
            className="ctl"
            lang="en"
            dir="ltr"
            autoComplete="off"
            data-testid="acc-pline-desc-en"
            value={line.descriptionEn}
            onChange={(e) => set({ descriptionEn: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-item"}
          label={t("accounting.field.itemId")}
          hint={t("accounting.field.itemIdHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-item"}
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            spellCheck={false}
            data-testid="acc-pline-item"
            value={line.itemId}
            onChange={(e) => set({ itemId: e.target.value })}
          />
        </AccField>
      </AccRow>
      <AccRow cols={3} testId="acc-purchase-line-row-2">
        <AccField
          id={idPrefix + "-group"}
          label={t("accounting.field.itemGroup")}
          hint={t("accounting.field.itemGroupHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-group"}
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            spellCheck={false}
            data-testid="acc-pline-group"
            value={line.itemGroup}
            onChange={(e) => set({ itemGroup: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-qty"}
          label={t("accounting.field.quantity")}
          hint={t("accounting.field.quantityHint")}
          error={
            line.quantity !== "" && !isQuantityText(line.quantity)
              ? t("accounting.field.quantityBad")
              : undefined
          }
          source="typed"
          required
        >
          <input
            id={idPrefix + "-qty"}
            className="ctl amt-input"
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            aria-invalid={line.quantity !== "" && !isQuantityText(line.quantity)}
            data-testid="acc-pline-qty"
            value={line.quantity}
            onChange={(e) => set({ quantity: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-price"}
          label={t("accounting.field.unitPrice")}
          hint={t("accounting.field.unitPriceHint")}
          error={
            line.unitPrice !== "" && !isMoneyText(line.unitPrice)
              ? t("accounting.field.moneyBad")
              : undefined
          }
          source="typed"
          required
        >
          <input
            id={idPrefix + "-price"}
            className="ctl amt-input"
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            aria-invalid={line.unitPrice !== "" && !isMoneyText(line.unitPrice)}
            data-testid="acc-pline-price"
            value={line.unitPrice}
            onChange={(e) => set({ unitPrice: e.target.value })}
          />
        </AccField>
      </AccRow>
      <AccRow cols={3} testId="acc-purchase-line-row-3">
        <AccField
          id={idPrefix + "-taxclass"}
          label={t("accounting.field.taxClassification")}
          hint={t("accounting.field.taxClassificationHint")}
          source="typed"
          required
        >
          <input
            id={idPrefix + "-taxclass"}
            className="ctl mono"
            dir="ltr"
            list={TAX_CLASS_LIST}
            autoComplete="off"
            spellCheck={false}
            data-testid="acc-pline-taxclass"
            value={line.taxClassification}
            onChange={(e) => set({ taxClassification: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-taxrate"}
          label={t("accounting.field.taxRate")}
          hint={t("accounting.field.taxRateHint")}
          error={
            line.taxRate !== "" && !isTaxRateText(line.taxRate)
              ? t("accounting.field.rateBad")
              : undefined
          }
          source="typed"
          required
        >
          <input
            id={idPrefix + "-taxrate"}
            className="ctl amt-input"
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            aria-invalid={line.taxRate !== "" && !isTaxRateText(line.taxRate)}
            data-testid="acc-pline-taxrate"
            value={line.taxRate}
            onChange={(e) => set({ taxRate: e.target.value })}
          />
        </AccField>
        <AccField
          id={idPrefix + "-recoverable"}
          label={t("accounting.field.taxRecoverable")}
          hint={t("accounting.field.taxRecoverableHint")}
          source="typed"
          required
        >
          <select
            id={idPrefix + "-recoverable"}
            className="ctl"
            data-testid="acc-pline-recoverable"
            value={line.taxRecoverable}
            onChange={(e) => set({ taxRecoverable: e.target.value })}
          >
            <option value="true">{t("accounting.value.yes")}</option>
            <option value="false">{t("accounting.value.no")}</option>
          </select>
        </AccField>
      </AccRow>
    </>
  );
}

/**
 * جدولُ سطور المشتريات المكتوبة — **بلا عمود مجموع**.
 * @param props السطور وإسقاطُ سطر.
 */
export function PurchaseLineTable(props: {
  readonly lines: readonly DraftPurchaseLine[];
  readonly onDrop: (index: number) => void;
}): ReactNode {
  const { t } = useT();
  return (
    <div className="acc-table" data-testid="acc-purchase-lines">
      <table>
        <caption className="visually-hidden">{t("accounting.lines.title")}</caption>
        <thead>
          <tr>
            <th scope="col">{t("accounting.field.descriptionAr")}</th>
            <th scope="col">{t("accounting.field.itemId")}</th>
            <th scope="col">{t("accounting.field.itemGroup")}</th>
            <th scope="col" className="n">{t("accounting.field.quantity")}</th>
            <th scope="col" className="n">{t("accounting.field.unitPrice")}</th>
            <th scope="col" className="n">{t("accounting.field.taxRate")}</th>
            <th scope="col">{t("accounting.field.taxRecoverable")}</th>
            <th scope="col">{t("accounting.field.action")}</th>
          </tr>
        </thead>
        <tbody>
          {props.lines.map((line, index) => (
            <tr key={index} data-testid={"acc-purchase-line-" + String(index)}>
              <td>{line.descriptionAr}</td>
              <td><span className="mono acc-id">{line.itemId}</span></td>
              <td><span className="mono acc-id">{line.itemGroup}</span></td>
              <td className="n"><span className="mono">{line.quantity}</span></td>
              <td className="n"><span className="mono">{line.unitPrice}</span></td>
              <td className="n"><span className="mono">{line.taxRate}</span></td>
              <td>{line.taxRecoverable === "true" ? t("accounting.value.yes") : t("accounting.value.no")}</td>
              <td>
                <DropLineButton
                  onClick={() => props.onDrop(index)}
                  testId={"acc-purchase-line-drop-" + String(index)}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
