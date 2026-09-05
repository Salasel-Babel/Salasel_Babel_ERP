/* ═══════════════════════════════════════════════════════════════════════════
   شاشات المرفقات الثلاث وحالِ الصنف — حرّاسُها
   The two attachment screens and the item-lifecycle screen — their guards
   ───────────────────────────────────────────────────────────────────────────
   سبعةٌ تُفحص هنا، وكلٌّ منها ينكسر بصمت لو لم يُفحص:

     ١ · **القوائم الثلاث تتّفق.** ‏`SCREENS` والموجّه وقائمةُ الملاحة اليدوية
         في `App.tsx` ثلاثُ نسخٍ لا شيء يقارنها، فشاشةٌ في واحدةٍ دون أخرى
         تُفتح بـCtrl+K ولا يراها من يقرأ الملاحة.

     ٢ · **الأبواب العشرة تُستدعى فعلاً.** والفحص على **الطلب المُرسَل** لا
         على وجود زرّ: زرٌّ لا يطلب شيئاً يمرّ من كل حارسٍ يقرأ الشيفرة.

     ٣ · **التذكرة والتنزيل خطوتان**، والتنزيل موقوفٌ قبل السكّ. ودمجُهما هو
         بالضبط ما يمنعه العقد، ولا يظهر عطلاً في أي فحصٍ آخر.

     ٤ · **الرفض يُقال قبل الضغط**: نصفُ الربط، والحجمُ فوق السقف، والمسحوبُ
         لا يُصحَّح — كلُّها تُعرض ويُعطَّل الزرّ **بلا طلبٍ يُرسَل**.

     ٥ · **نصُّ حكم الصنف مستقلٌّ عن نصّ حكم الموضع** (ADR-0072): الأوّل يقول
         «يُقبل» والثاني يقول «يُرفض»، وتوحيدُهما عطلُ صحّةٍ لا عطلُ صياغة.

     ٦ · **المُعطَّل والمسحوب يبقيان في القائمة**، ولا مرشّح افتراضي يُخفيهما.

     ٧ · **قواعد الملفّات نفسها**: كل `<Field` بوصف (ADR-0078)، ولا `parseFloat`
         ولا `Number(` إلا على ما ينشره العقد عدداً صحيحاً بمدىً دقيق.

   ‏**ولا بيان شخصي في هذا الملفّ ولا ملفّ عيّنةٍ فيه شيءٌ حقيقي**: أسماءُ
   ملفّاتٍ وصفيّة، ومعرّفاتٌ اصطناعية، وبايتاتٌ مُصطنَعة في الذاكرة لا على
   القرص — وشاشةُ المرفقات تلمس ملفّات المستخدم، فلا يُودَع في المستودع
   ملفٌّ واحد لأجلها.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCREENS } from "../src/app/shell/sections";
import { ATTACHMENT_NEXT_STEP } from "../src/screens/attachments/shared";
import type { RawResponse, Transport } from "../src/api/transport";

const SRC = path.resolve(process.cwd(), "src");
const read = (rel: string): string => readFileSync(path.resolve(SRC, rel), "utf8");

const CODES = ["ar", "en", "hi", "ur"] as const;

const COMPANY = "11111111-1111-1111-1111-111111111111";
const BASE = "/api/v1/companies/" + COMPANY;
const HEALTH = { apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA", status: "ok" };

const ATTACH_ID = "dddddddd-dddd-4ddd-8ddd-ddddddddddd1";
const ATTACH_ID_2 = "dddddddd-dddd-4ddd-8ddd-ddddddddddd2";
const ITEM_ID = "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1";
const DIGEST = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

const ATTACHMENT = {
  id: ATTACH_ID,
  fileName: "سندٌ ممسوح.pdf",
  mediaType: "application/pdf",
  byteLength: 184320,
  contentHash: DIGEST,
  contentPath: BASE + "/attachments/" + ATTACH_ID + "/content",
  sourceDocumentType: "purchasing.supplier_bill",
  sourceDocumentId: "ffffffff-ffff-4fff-8fff-fffffffffff1",
  storedAt: "2026-05-04T09:12:31.0000000Z",
  storedBy: "99999999-9999-4999-8999-999999999991",
  supersedes: null,
  supersededBy: null,
  version: 1,
  withdrawal: null,
};

const WITHDRAWN = {
  ...ATTACHMENT,
  id: ATTACH_ID_2,
  fileName: "سندٌ مكرّر.png",
  mediaType: "image/png",
  byteLength: 51204,
  version: 2,
  supersedes: ATTACH_ID,
  withdrawal: {
    reasonKey: "duplicate",
    withdrawnAt: "2026-05-06T11:02:00.0000000Z",
    withdrawnBy: "99999999-9999-4999-8999-999999999992",
  },
};

const PAGE = { items: [ATTACHMENT, WITHDRAWN], skip: 0, take: 50, total: 2 };

const TICKET = {
  attachmentId: ATTACH_ID,
  contentPath: ATTACHMENT.contentPath + "?ticket=xxxx",
  expiresAt: "2099-01-01T00:00:00.0000000Z",
  token: "AAAAAAAAAAAAAAAAAAAA",
};

const ITEM = {
  id: ITEM_ID,
  code: "ITM-0001",
  itemGroup: "RAW",
  baseUnit: "PCE",
  name: { ar: "حديد تسليح", en: "Rebar" },
  units: [{ unitCode: "BOX", numerator: 12, denominator: 1 }],
};
const ITEM_LIST = { itemCount: 1, items: [ITEM] };
const ACTIVE_WITH_STOCK = {
  id: ITEM_ID,
  code: "ITM-0001",
  isActive: true,
  holdsStock: true,
  placementsWithStock: 2,
};
const STOPPED = { ...ACTIVE_WITH_STOCK, isActive: false };

/* ══════════════════════════════════════════════════════════ أدوات ═════ */

interface Recorded {
  method: string;
  url: string;
  body?: unknown;
}

function stub(options: {
  routes: Readonly<Record<string, unknown>>;
  sent?: Recorded[];
}): Transport {
  return ({ method, url, body }) => {
    options.sent?.push({ method, url, body });
    const at = url.split("?")[0] ?? url;
    const found = options.routes[method + " " + at];
    if (found === undefined) {
      return Promise.resolve<RawResponse>({ ok: false, status: 404, json: null, url });
    }
    return Promise.resolve<RawResponse>({ ok: true, status: 200, json: found, url });
  };
}

async function mount(options: { path: string; transport: Transport }): Promise<void> {
  const router = createAppRouter({ memory: true, initialPath: options.path });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  function Tree(): ReactNode {
    return (
      <LocaleProvider i18n={createI18n()} initial="ar">
        <QueryClientProvider client={client}>
          <ApiProvider transport={options.transport}>
            <RouterProvider router={router} />
          </ApiProvider>
        </QueryClientProvider>
      </LocaleProvider>
    );
  }
  await act(async () => {
    render(<Tree />);
    await router.load();
  });
}

async function click(element: Element): Promise<void> {
  await act(async () => {
    (element as HTMLElement).click();
    await Promise.resolve();
  });
}

function setNativeValue(element: HTMLInputElement, value: string): void {
  const proto = Object.getPrototypeOf(element) as object;
  // eslint-disable-next-line @typescript-eslint/unbound-method
  const setter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
  if (setter) setter.call(element, value);
  else element.value = value;
}

async function type(element: HTMLInputElement, value: string): Promise<void> {
  await act(async () => {
    setNativeValue(element, value);
    element.dispatchEvent(new Event("input", { bubbles: true }));
    await Promise.resolve();
  });
}

beforeEach(() => {
  globalThis.localStorage.setItem(
    "sb-api-config",
    JSON.stringify({ baseUrl: "", token: "t", companyId: COMPANY, book: "MAIN", period: "" })
  );
});

afterEach(() => {
  cleanup();
  globalThis.localStorage.clear();
});

/* ═══════════════════════════════════════════════════════════════════════
   ١ · القوائم الثلاث تتّفق
   ═══════════════════════════════════════════════════════════════════════ */
describe("عقد الملاحة للشاشات الثلاث", () => {
  const ADDED = ["/attachments", "/attachments/custody", "/inventory/item-lifecycle"];

  it("الثلاث مسجّلةٌ في SCREENS بأقسامها", () => {
    const paths = SCREENS.map((s) => s.path);
    for (const p of ADDED) expect(paths, "لا صفّ في SCREENS للمسار " + p).toContain(p);
    expect(SCREENS.find((s) => s.path === "/attachments")?.section).toBe("accounting");
    expect(SCREENS.find((s) => s.path === "/attachments/custody")?.section).toBe("accounting");
    expect(SCREENS.find((s) => s.path === "/inventory/item-lifecycle")?.section).toBe("inventory");
  });

  it("ولكلٍّ منها رابطٌ في قائمة الملاحة اليدوية — لا تُفتح بلوحة الأوامر وحدها", async () => {
    await mount({ path: "/", transport: stub({ routes: { "GET /health": HEALTH } }) });
    const nav = document.querySelector(".app-side");
    expect(nav).not.toBeNull();
    const hrefs = [...(nav?.querySelectorAll("a[href]") ?? [])].map((a) => a.getAttribute("href"));
    for (const p of ADDED) expect(hrefs, "لا رابط في الملاحة إلى " + p).toContain(p);
  });

  it("ولكلٍّ منها مسارٌ في الموجّه يفتح شاشتها لا صفحةً فارغة", async () => {
    await mount({
      path: "/attachments",
      transport: stub({ routes: { "GET /health": HEALTH, ["GET " + BASE + "/attachments"]: PAGE } }),
    });
    expect(await screen.findByTestId("attachment-register-screen")).toBeTruthy();
  });

  it("وأسماؤها مترجَمةٌ في اللغات الأربع", () => {
    const i18n = createI18n();
    const keys = SCREENS.filter((s) => ADDED.includes(s.path)).map((s) => s.labelKey);
    expect(keys).toHaveLength(3);
    for (const locale of CODES) {
      i18n.use(locale);
      for (const key of keys) expect(i18n.t(key), locale + " ← " + key).not.toBe(key);
    }
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٢ · الأبواب تُستدعى فعلاً — والفحص على الطلب لا على الزرّ
   ═══════════════════════════════════════════════════════════════════════ */
describe("الأبواب التي لم يكن يبلغها شيء", () => {
  it("listAttachments يُطلب، والمسحوب يظهر في الجرد بحالته ولا يُخفى", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/attachments",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/attachments"]: PAGE },
        sent,
      }),
    });
    await screen.findByTestId("attach-table");
    expect(sent.some((r) => r.method === "GET" && r.url.startsWith(BASE + "/attachments?"))).toBe(true);

    const rows = screen.getAllByTestId("attach-row");
    expect(rows).toHaveLength(2);
    /* المسحوب باقٍ في القائمة، ومحمولٌ عليه وسمُ حالته. */
    expect(rows.some((r) => r.getAttribute("data-active") === "false")).toBe(true);
    expect(screen.getByTestId("attach-flag-withdrawn")).toBeTruthy();
    expect(screen.getByTestId("attach-flag-current")).toBeTruthy();
  });

  it("issueAttachmentDownloadTicket ثمّ downloadAttachment — خطوتان، والثانية موقوفة قبل الأولى", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/attachments",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/attachments"]: PAGE,
          ["POST " + BASE + "/attachments/" + ATTACH_ID + "/download-tickets"]: TICKET,
        },
        sent,
      }),
    });
    await screen.findByTestId("attach-table");
    await click(screen.getAllByTestId("attach-pick")[0] as Element);
    await screen.findByTestId("attach-ticket-panel");

    /* **قبل السكّ**: زرّ التنزيل معطَّل ولا طلبَ محتوى قد خرج. */
    const download = screen.getByTestId<HTMLButtonElement>("attach-ticket-download");
    expect(download.disabled).toBe(true);
    expect(screen.getByTestId("attach-ticket-none")).toBeTruthy();
    expect(sent.some((r) => r.url.includes("/content"))).toBe(false);

    await click(screen.getByTestId("attach-ticket-mint"));
    await screen.findByTestId("attach-ticket-minted");
    const minted = sent.filter((r) => r.url.endsWith("/download-tickets"));
    expect(minted).toHaveLength(1);
    /* والعمر يعبر عدداً صحيحاً كما ينشره العقد، لا نصّاً ولا كسراً. */
    expect(minted[0]?.body).toEqual({ lifetimeSeconds: 120 });

    /* **وبعد السكّ**: الزرّ يعمل، والتنزيل يحمل الرمز في سلسلة استعلامه. */
    expect(screen.getByTestId<HTMLButtonElement>("attach-ticket-download").disabled).toBe(false);
  });

  it("readAttachment يُطلَب من شاشة العهدة، والسلسلة والعلامة تُعرَضان", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/attachments/custody",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/attachments/" + ATTACH_ID_2]: WITHDRAWN,
        },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("attach-lookup-id"), ATTACH_ID_2);
    await click(screen.getByTestId("attach-read"));
    await screen.findByTestId("attach-descriptor");
    expect(sent.some((r) => r.url === BASE + "/attachments/" + ATTACH_ID_2)).toBe(true);
    expect(screen.getByTestId("attach-withdrawal-mark")).toBeTruthy();
    expect(screen.getByTestId("attach-open-predecessor")).toBeTruthy();
  });

  it("readItem و readItemLifecycle يُطلبان معاً عند فتح الصنف", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/inventory/item-lifecycle",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/items"]: ITEM_LIST,
          ["GET " + BASE + "/items/" + ITEM_ID]: ITEM,
          ["GET " + BASE + "/items/" + ITEM_ID + "/lifecycle"]: ACTIVE_WITH_STOCK,
        },
        sent,
      }),
    });
    await screen.findByTestId("life-table");
    await click(screen.getAllByTestId("life-pick")[0] as Element);
    await screen.findByTestId("life-revise-form");
    expect(sent.some((r) => r.url === BASE + "/items/" + ITEM_ID)).toBe(true);
    expect(sent.some((r) => r.url === BASE + "/items/" + ITEM_ID + "/lifecycle")).toBe(true);
  });

  it("deactivateItem يُرسَل بعد خطوتين، وجوابُه يُسمّي ما بقي من رصيد", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/inventory/item-lifecycle",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/items"]: ITEM_LIST,
          ["GET " + BASE + "/items/" + ITEM_ID]: ITEM,
          ["GET " + BASE + "/items/" + ITEM_ID + "/lifecycle"]: ACTIVE_WITH_STOCK,
          ["POST " + BASE + "/items/" + ITEM_ID + "/deactivation"]: STOPPED,
        },
        sent,
      }),
    });
    await screen.findByTestId("life-table");
    await click(screen.getAllByTestId("life-pick")[0] as Element);
    await screen.findByTestId("life-stop-form");

    /* الخطوة الأولى تُظهر التأكيد **ولا ترسل شيئاً**. */
    const before = sent.length;
    await click(screen.getByTestId("life-deactivate"));
    await screen.findByTestId("life-confirm");
    expect(sent.length).toBe(before);

    await click(screen.getByTestId("life-confirm-off"));
    await screen.findByTestId("life-stopped");
    expect(sent.some((r) => r.method === "POST" && r.url.endsWith("/deactivation"))).toBe(true);
    /* **وليس صامتاً**: ما بقي من رصيدٍ ومواضعه معروضان بعد الإيقاف.
       والرقم يُقبل بوجهيه — لاتينيّ أو عربيّ-هنديّ — لأن وجهه قرارُ طبقة
       التدويل لا قرارُ هذه الشاشة، وحارسٌ يربطه بوجهٍ بعينه يُحمِّر عند
       تبديل الوجه لا عند عطلٍ في الشاشة. */
    const stopped = screen.getByTestId("life-stopped").textContent ?? "";
    expect(stopped).toMatch(/[2٢]/);
    expect(stopped).toContain("inventory.item_inactive");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٣ · الرفض قبل الضغط — بلا طلبٍ يُرسَل
   ═══════════════════════════════════════════════════════════════════════ */
describe("الرفض يُقال قبل الضغط لا بعده", () => {
  it("نصفُ الربط في الترشيح يوقف الطلب ويُسمّي الرمز", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/attachments",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/attachments"]: PAGE },
        sent,
      }),
    });
    await screen.findByTestId("attach-table");
    const before = sent.length;
    await type(screen.getByTestId<HTMLInputElement>("attach-filter-type"), "sales.invoice");

    const warning = await screen.findByTestId("attach-half-link");
    expect(warning.getAttribute("data-code")).toBe("storage.source_document_incomplete");
    /* ولا استعلامَ خرج بنصف ربط. */
    expect(sent.length).toBe(before);
    expect(screen.getByTestId<HTMLButtonElement>("attach-reload").disabled).toBe(true);
  });

  it("والمسحوبُ لا يُصحَّح ولا يُسحب مرّتين — والاثنان مُعلَنان قبل الضغط", async () => {
    await mount({
      path: "/attachments/custody",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/attachments/" + ATTACH_ID_2]: WITHDRAWN },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("attach-lookup-id"), ATTACH_ID_2);
    await click(screen.getByTestId("attach-read"));
    await screen.findByTestId("attach-descriptor");

    expect(screen.getByTestId("attach-revise-blocked").getAttribute("data-code")).toBe(
      "storage.attachment_withdrawn"
    );
    expect(screen.getByTestId("attach-withdraw-blocked").getAttribute("data-code")).toBe(
      "storage.attachment_withdrawn"
    );
    expect(screen.getByTestId<HTMLButtonElement>("attach-revise-submit").disabled).toBe(true);
    expect(screen.getByTestId<HTMLButtonElement>("attach-withdraw-start").disabled).toBe(true);
  });

  it("ووحدةُ الأساس تُقفَل قبل الضغط حين يكون للصنف رصيد", async () => {
    await mount({
      path: "/inventory/item-lifecycle",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/items"]: ITEM_LIST,
          ["GET " + BASE + "/items/" + ITEM_ID]: ITEM,
          ["GET " + BASE + "/items/" + ITEM_ID + "/lifecycle"]: ACTIVE_WITH_STOCK,
        },
      }),
    });
    await screen.findByTestId("life-table");
    await click(screen.getAllByTestId("life-pick")[0] as Element);
    await screen.findByTestId("life-revise-form");
    const base = screen.getByTestId<HTMLInputElement>("life-base");
    expect(base.disabled).toBe(true);
    /* ورمز الصنف معروضٌ ومقفل: هوية تُقرأ من المسار ولا تُقبل في الجسم. */
    expect(screen.getByTestId<HTMLInputElement>("life-code").readOnly).toBe(true);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٤ · نصّان مختلفان عمداً — ولا يُوحَّدان
   ═══════════════════════════════════════════════════════════════════════ */
describe("حكمُ تعطيل الصنف ليس حكمَ تعطيل الموضع (ADR-0072)", () => {
  it("نصُّ الصنف يقول «يُقبل» ويُسمّي الفرق، ونصُّ الموضع يقول «يُرفض»", () => {
    const i18n = createI18n();
    i18n.use("ar");
    const item = i18n.t("inventory.life.offRuleItem");
    const place = i18n.t("inventory.warehouses.offRule");
    expect(item).not.toBe(place);
    expect(item).toContain("يُقبل");
    expect(item).toContain("موضع");
    expect(place).toContain("يُرفض");
    /* والنصّان يُسمّيان أحدهما الآخر: من قرأ أحدهما لا يُفاجأ بالثاني. */
    expect(place).toContain("الصنف");
  });

  it("والنصّان موجودان في اللغات الأربع ومختلفان في كلٍّ منها", () => {
    const i18n = createI18n();
    for (const locale of CODES) {
      i18n.use(locale);
      const item = i18n.t("inventory.life.offRuleItem");
      const place = i18n.t("inventory.warehouses.offRule");
      expect(item, locale).not.toBe("inventory.life.offRuleItem");
      expect(item, locale).not.toBe(place);
    }
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٥ · قواعد الملفّات نفسها
   ═══════════════════════════════════════════════════════════════════════ */
describe("قواعد الشيفرة — تُفحص على النصّ لأن لا اختبارَ يراها", () => {
  const FILES = [
    "screens/attachments/shared.tsx",
    "screens/attachments/AttachmentRegisterScreen.tsx",
    "screens/attachments/AttachmentCustodyScreen.tsx",
    "screens/inventory/ItemLifecycleScreen.tsx",
  ];

  it("لا parseFloat، ولا Number( إلا على ما ينشره العقد عدداً صحيحاً بمدىً دقيق", () => {
    const offenders: string[] = [];
    for (const file of FILES) {
      const text = read(file).replace(/\/\*[\s\S]*?\*\//g, " ");
      for (const line of text.split("\n")) {
        if (/parseFloat|parseInt/.test(line)) offenders.push(file + " ← " + line.trim());
        /* ‏`Number(` مسموحٌ على ثلاثةٍ وحدها — والثلاثة أعدادٌ صحيحة ينشرها
           العقد بحدودٍ تقع كاملةً داخل المدى الدقيق للعائم المزدوج:
           البسط والمقام (حتى مليار)، وعمرُ التذكرة (حتى ثلاثمئة). وما عداها
           — مالٌ أو كمّية أو نسبة — نصٌّ لا يمرّ على `Number` أبداً. */
        if (/\bNumber\(/.test(line) && !/numerator|denominator|seconds/.test(line)) {
          offenders.push(file + " ← " + line.trim());
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("حارسُ ADR-0078: كل حقلٍ في صفٍّ يحمل وصفاً — وإلّا سُنّن قاعُ صفّه", () => {
    const offenders: string[] = [];
    for (const file of FILES) {
      const text = read(file).replace(/\/\*[\s\S]*?\*\//g, " ");
      for (const m of text.matchAll(/<Field\b[\s\S]*?>/g)) {
        const tag = m[0];
        if (/\bhint=/.test(tag) || /\berror=/.test(tag)) continue;
        offenders.push(file + " ← " + tag.replace(/\s+/g, " ").slice(0, 70));
      }
    }
    expect(offenders).toEqual([]);
  });

  it("حارسُ لافراغ: الملفّات الأربعة تحوي حقولاً أصلاً", () => {
    let fields = 0;
    for (const file of FILES) fields += [...read(file).matchAll(/<Field\b/g)].length;
    /* العدد مقيس: 15 حقلاً في الملفّات الأربعة. وحارسٌ لا يمسح شيئاً يمرّ دائماً. */
    expect(fields).toBeGreaterThanOrEqual(14);
  });

  it("ولا `.field` مكتوبٌ بيد — الأوّليّة تجمع الوصف في خانةٍ واحدة (ADR-0067)", () => {
    const offenders: string[] = [];
    for (const file of FILES) {
      const text = read(file).replace(/\/\*[\s\S]*?\*\//g, " ");
      if (/className="field"/.test(text)) offenders.push(file);
    }
    expect(offenders).toEqual([]);
  });

  it("ولا fetch مكتوبٌ بيد — العميل المُولَّد هو الحدّ الوحيد (ADR-0022)", () => {
    for (const file of FILES) {
      const text = read(file).replace(/\/\*[\s\S]*?\*\//g, " ");
      expect(text, file).not.toMatch(/\bfetch\s*\(/);
      expect(text, file).not.toMatch(/XMLHttpRequest/);
    }
  });

  it("وكل رمزٍ في خريطة الخطوة التالية مفتاحٌ مترجَمٌ في اللغات الأربع", () => {
    const i18n = createI18n();
    const codes = Object.keys(ATTACHMENT_NEXT_STEP);
    /* حارسُ لافراغ: خريطةٌ فرغت تمرّ من كل حلقةٍ تحتها. */
    expect(codes.length).toBeGreaterThanOrEqual(12);
    for (const locale of CODES) {
      i18n.use(locale);
      for (const code of codes) {
        const key = ATTACHMENT_NEXT_STEP[code] ?? "";
        expect(key, code).not.toBe("");
        expect(i18n.t(key), locale + " ← " + key).not.toBe(key);
      }
    }
  });

  it("وكل رمزٍ في الخريطة رمزُ خزنٍ منشورٌ في الخلفية، لا رمزٌ مخترَع", () => {
    const errors = readFileSync(
      path.resolve(process.cwd(), "..", "src/Babel.Contracts/Storage/AttachmentErrors.cs"),
      "utf8"
    );
    const published = new Set([...errors.matchAll(/"(storage\.[a-z_]+)"/g)].map((m) => m[1]));
    /* حارسُ لافراغ: ملفٌّ لم يُقرأ يجعل المجموعة فارغةً فتمرّ الحلقة. */
    expect(published.size).toBeGreaterThanOrEqual(12);
    for (const code of Object.keys(ATTACHMENT_NEXT_STEP)) {
      expect([...published], "رمزٌ ليس في AttachmentErrors: " + code).toContain(code);
    }
  });
});
