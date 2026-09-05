/* ═══════════════════════════════════════════════════════════════════════════
   قسم العقارات — لقطاتٌ للمراجعة البصرية، وفحصٌ سلوكي معها
   ───────────────────────────────────────────────────────────────────────────
   لا خادمَ حيّاً هنا ولا قاعدة: كل نداءٍ إلى `/api/v1` **يُعترَض ويُجاب** بجسمٍ
   مطابقٍ للعقد — والمهمّ أنه يمرّ بفاكّ الترميز المُولَّد نفسه في المتصفّح،
   فمبلغٌ لا يطابق نحو العقد يُسقط الصفحة ولا يُصوَّر. أي أن اللقطة لا تكذب في
   شكل المال.
   ═══════════════════════════════════════════════════════════════════════════ */
import path from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test, type Page } from "@playwright/test";

const OUT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../artifacts");
const COMPANY = "00000000-0000-4000-8000-00000000cafe";

const PROPERTY = {
  id: "11111111-1111-4111-8111-111111111111",
  code: "PRP-001",
  nameAr: "برج السلام — طريق الملك فهد",
  nameTranslations: [{ name: "en", value: "Al-Salam Tower — King Fahd Road" }],
  ownershipModel: "managed_for_others",
  ownerId: "22222222-2222-4222-8222-222222222222",
  ownerShareNumerator: "3",
  ownerShareDenominator: "4",
};

const LEASE = {
  id: "33333333-3333-4333-8333-333333333333",
  ejarContractNumber: "EJR-2026-0000007",
  propertyId: PROPERTY.id,
  unitId: "44444444-4444-4444-8444-444444444444",
  lesseeId: "55555555-5555-4555-8555-555555555555",
  startsOn: "2026-01-01",
  endsOn: "2026-12-31",
  state: "BILLABLE",
  totalRent: "240000.0000",
};

function scheduleLine(seq: number, from: string, to: string, due: string, invoiced: boolean) {
  return {
    id: "line-" + String(seq).padStart(2, "0") + "-0000-4000-8000-000000000000",
    seq,
    periodFrom: from,
    periodTo: to,
    dueOn: due,
    amount: "60000.0000",
    isInvoiced: invoiced,
  };
}

const SCHEDULE = {
  leaseId: LEASE.id,
  lines: [
    scheduleLine(1, "2026-01-01", "2026-03-31", "2026-01-05", true),
    scheduleLine(2, "2026-04-01", "2026-06-30", "2026-04-05", true),
    scheduleLine(3, "2026-07-01", "2026-09-30", "2026-07-05", false),
    scheduleLine(4, "2026-10-15", "2026-12-31", "2026-10-05", false),
  ],
};

const ARREARS = {
  asOf: "2026-08-31",
  isReconciled: false,
  controlTotal: "412750.0000",
  divergence: "-1250.0000",
  totals: {
    notDue: "120000.0000",
    days1To30: "60000.0000",
    days31To60: "45000.0000",
    days61To90: "30000.0000",
    over90: "156500.0000",
    total: "411500.0000",
  },
  parties: [
    {
      partyId: "55555555-5555-4555-8555-555555555555",
      code: "TEN-0001",
      nameAr: "مؤسسة النور التجارية",
      nameTranslations: [{ name: "en", value: "Al-Noor Trading Est." }],
      bands: {
        notDue: "60000.0000",
        days1To30: "60000.0000",
        days31To60: "0.0000",
        days61To90: "30000.0000",
        over90: "96500.0000",
        total: "246500.0000",
      },
    },
    {
      partyId: "66666666-6666-4666-8666-666666666666",
      code: "TEN-0002",
      nameAr: "شركة الواحة للتجزئة",
      nameTranslations: [{ name: "en", value: "Al-Waha Retail Co." }],
      bands: {
        notDue: "60000.0000",
        days1To30: "0.0000",
        days31To60: "45000.0000",
        days61To90: "0.0000",
        over90: "60000.0000",
        total: "165000.0000",
      },
    },
  ],
};

const RECEIPT = {
  id: "77777777-7777-4777-8777-777777777777",
  number: "RCV-2026-0104",
  received: "60000.0000",
  state: "POSTED",
  entryId: "88888888-8888-4888-8888-888888888888",
  allocationEntryId: null,
  isAllocated: false,
  alreadyPosted: false,
  eventCode: "realestate.collection.unallocated",
};

const INVOICE = {
  id: "99999999-9999-4999-8999-999999999999",
  number: "RNT-2026-0031",
  state: "POSTED",
  net: "60000.0000",
  tax: "9000.0000",
  gross: "69000.0000",
  vatTreatment: "standard",
  exemptionReasonCode: "",
  exemptionReasonPending: false,
  eventCode: "realestate.rent.accrued.managed",
  entryId: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  alreadyPosted: false,
};

async function stub(page: Page): Promise<void> {
  await page.route("**/health", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA", status: "ok" }),
    })
  );
  await page.route("**/api/v1/**", (route) => {
    const url = route.request().url();
    const json = (body: unknown) =>
      route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) });
    if (/\/properties\/[^/]+$/.test(url)) return json(PROPERTY);
    if (/\/lease-registrations\/[^/]+\/schedule$/.test(url)) return json(SCHEDULE);
    if (/\/lease-registrations\/[^/]+$/.test(url)) return json(LEASE);
    if (/tenant-arrears-aging/.test(url)) return json(ARREARS);
    if (/\/tenant-receipts(\?|$)/.test(url)) return json(RECEIPT);
    if (/\/rent-invoices(\?|$)/.test(url)) return json(INVOICE);
    return route.fulfill({
      status: 404,
      contentType: "application/problem+json",
      body: JSON.stringify({
        type: "about:blank",
        title: "Not found",
        titleAr: "غير موجود",
        status: 404,
        detail: "no stub",
        detailAr: "لا جواب مُعدّ",
        instance: url,
        code: "http.not_found",
        traceId: "00-shot-0",
        errors: [],
      }),
    });
  });
}

async function open(page: Page, route: string, theme: "dark" | "light"): Promise<void> {
  await page.addInitScript(
    ([company, chosen]) => {
      globalThis.localStorage.setItem(
        "sb-api-config",
        JSON.stringify({ baseUrl: "", token: "shot", companyId: company, book: "MAIN", period: "" })
      );
      globalThis.localStorage.setItem("sb-theme", chosen);
      globalThis.localStorage.setItem("sb-locale", "ar");
    },
    [COMPANY, theme] as const
  );
  await page.emulateMedia({ colorScheme: theme });
  await page.goto(route);
  await page.evaluate(() => document.fonts.ready);
}

test.use({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2, locale: "ar-SA" });

for (const theme of ["dark", "light"] as const) {
  test("لقطات قسم العقارات — " + theme, async ({ page }) => {
    test.setTimeout(120_000);
    await stub(page);

    /* ── السجلّ العقاري: قراءةٌ بمعرّف، ونموذجٌ مُدار بحصّة غير كاملة ─────── */
    await open(page, "/realestate", theme);
    await page.waitForSelector('[data-testid="realestate-register"]');
    await page.fill('[data-testid="re-lookup-id"]', PROPERTY.id);
    await page.click('[data-testid="re-lookup-go"]');
    await page.waitForSelector('[data-testid="re-lookup-result"]');
    await expect(page.locator('[data-testid="re-property-share-open"]')).toBeVisible();
    await page.waitForTimeout(900);
    await page.screenshot({ path: path.join(OUT, `realestate-register-${theme}.png`), fullPage: true });

    /* ── العقد: مدّته على الشريط، وقسطان متقاطعان يُرفعان تعارضاً ────────── */
    await open(page, "/realestate/lease", theme);
    await page.waitForSelector('[data-testid="realestate-lease"]');
    await page.fill('[data-testid="re-lease-id"]', LEASE.id);
    await page.click('[data-testid="re-lease-open-go"]');
    await page.waitForSelector('[data-testid="re-lease-header"]');
    await page.click('[data-testid="re-schedule-load"]');
    await page.waitForSelector('[data-testid="re-schedule-band"]');
    await expect(page.locator('[data-testid="re-schedule-table"]')).toBeVisible();
    await page.waitForTimeout(900);
    await page.screenshot({ path: path.join(OUT, `realestate-lease-${theme}.png`), fullPage: true });

    /* ── المتأخّرات: مصالحةٌ لا تُغلق، وشرائح الأعمار تحتها ──────────────── */
    await open(page, "/realestate/arrears", theme);
    await page.waitForSelector('[data-testid="realestate-arrears"]');
    await page.click('[data-testid="re-arrears-load"]');
    await page.waitForSelector('[data-testid="re-arrears-table"]');
    await expect(
      page.locator('[data-testid="re-arrears-reconciliation"][data-reconciled="false"]')
    ).toBeVisible();
    await page.waitForTimeout(900);
    await page.screenshot({ path: path.join(OUT, `realestate-arrears-${theme}.png`), fullPage: true });
  });
}
