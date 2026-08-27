/* ═══════════════════════════════════════════════════════════════════════════
   الرحلة كاملةً: دخول ← اختيار منشأة ← ترحيل قيد ← رؤيته في الميزان
   The whole journey: sign in, choose a company, post an entry, see it
   ───────────────────────────────────────────────────────────────────────────
   وما يُثبَت هنا لا يُثبته اختبار وحدة ولا اختبار خادم:

   ١ · أن المستخدم يصل من صفحة بيضاء إلى ميزان مراجعة **بلا أن يكتب معرّفاً
       بصيغة 8-4-4-4-12** ولا مرّة واحدة.

   ٢ · أن المبلغ الذي يُتلفه Number يغادر المتصفّح **بايتاً ببايت**. والقياس
       على **جسم الطلب الخارج من المتصفّح نفسه** لا على ما يقوله الخادم:
       الخادم يستطيع أن يكون سليماً بينما العميل أفسد القيمة قبل الإرسال،
       وهذا هو الاتجاه غير المُجرَّب.

   ٣ · أن الإرسال الثاني بالمفتاح نفسه **يُرى** حالةً مختلفة عن الأول.

   ٤ · أن الرفض المحاسبي يصل برمزه وتتصرّف عليه الشاشة بلا قراءة نصّ.

   ٥ · أن الشاشتين الجديدتين تمرّان مصفوفة العرض نفسها: أربع لغات × مظهران ×
       ثلاثة عروض، بلا انزلاق أفقي، وبفواصل عشرية مصطفّة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { test, expect, type Page } from "@playwright/test";

const MOCK = "http://127.0.0.1:5099";
const TOKEN = "mock-token";
const EXPIRED = "mock-expired";
const NO_COMPANY = "mock-no-company";
const COMPANY = "11111111-1111-4111-8111-111111111111";
const NOT_SET_UP = "22222222-2222-4222-8222-222222222222";

/** القيمة المقيسة في هذا المستودع: Number يجعلها 1000000000000.4012. */
const LOSSY = "1000000000000.4013";

const LOCALES = [
  { code: "ar", dir: "rtl" },
  { code: "en", dir: "ltr" },
  { code: "ur", dir: "rtl" },
  { code: "hi", dir: "ltr" },
] as const;
const THEMES = ["light", "dark"] as const;
const WIDTHS = [360, 768, 1920] as const;

async function withTheme(page: Page, theme: string): Promise<void> {
  await page.addInitScript((t) => {
    try {
      localStorage.setItem("sb-theme", t);
      localStorage.setItem("sb-palette", "default");
    } catch {
      /* ignore */
    }
  }, theme);
}

function signInUrl(locale = "ar"): string {
  return "/sign-in?" + new URLSearchParams({ lang: locale, baseUrl: MOCK }).toString();
}

function voucherUrl(locale = "ar"): string {
  return (
    "/voucher?" +
    new URLSearchParams({
      lang: locale,
      baseUrl: MOCK,
      token: TOKEN,
      companyId: COMPANY,
      book: "MAIN",
    }).toString()
  );
}

/** يقيس انزلاق جسم الصفحة أفقياً. */
async function horizontalOverflow(page: Page): Promise<number> {
  return page.evaluate(() => {
    const el = document.scrollingElement ?? document.documentElement;
    return el.scrollWidth - el.clientWidth;
  });
}


/**
 * يملأ ما **لا يقوله العقد وتعلّمته الشاشة من الرفض**: الحساب الضابط يحتاج
 * طرفاً، والحساب ذو البُعد الإلزامي يحتاج فرعاً. مقيس على الخادم الحقيقي.
 * @param page الصفحة.
 */
async function fillWhatTheLedgerDemands(page: Page): Promise<void> {
  await page.getByTestId("voucher-qualifier").nth(0).fill("bank");
  await page.getByTestId("voucher-subledger-kind").nth(0).selectOption("Treasury");
  await page.getByTestId("voucher-party").nth(0).fill("BANK-0001");
  await page.getByTestId("voucher-branch").nth(1).fill("BR-01");
}

/* ═══════════════════════════ ١ · الدخول ═══════════════════════════════ */

test.describe("الدخول واختيار المنشأة", () => {
  test("من صفحة بيضاء إلى ميزان مراجعة بلا كتابة معرّف", async ({ page }) => {
    await page.goto(signInUrl());
    await page.waitForSelector('[data-testid="sign-in-screen"]');

    /* لا شركة بعد: الشارة تدعو إلى الاختيار ولا تعرض معرّفاً. */
    await expect(page.getByTestId("company-badge-empty")).toBeVisible();

    await page.getByTestId("sign-in-token").fill(TOKEN);
    await page.getByTestId("sign-in-submit").click();

    await page.waitForSelector('[data-testid="company-picker"]');

    /* حارس اللاخواء: قائمةٌ فارغة لا تُثبت شيئاً. */
    const options = page.locator('[data-testid="company-option"]');
    expect(await options.count(), "عدد المنشآت المعروضة").toBe(2);

    /* المنشأة الجاهزة تُعرض باسمها العربي — وهو السجلّ. */
    const ready = page.locator('[data-testid="company-option"][data-state="Ready"]');
    await expect(ready.getByTestId("company-name-record")).toHaveText("مؤسسة بابل للتجارة");
    await expect(ready.getByTestId("company-id")).toHaveText(COMPANY);

    /* والمنشأة غير المؤسَّسة **تظهر** ولا تُخفى، وزرّها معطّل. */
    const notSetUp = page.locator('[data-testid="company-option"][data-state="NotSetUp"]');
    await expect(notSetUp).toHaveAttribute("data-company", NOT_SET_UP);
    await expect(notSetUp.getByTestId("company-choose")).toBeDisabled();
    await expect(notSetUp.getByTestId("company-not-set-up")).toBeVisible();

    /* الاختيار ينقل إلى الميزان، وشارة المنشأة تحمل الاسم لا المعرّف. */
    await ready.getByTestId("company-choose").click();
    await page.waitForSelector('[data-testid="trial-balance-screen"]');
    await expect(page.getByTestId("company-badge-name")).toHaveText("مؤسسة بابل للتجارة");

    /* ولم يُكتب معرّف واحد بيد في هذه الرحلة كلّها. */
    await expect(page.getByTestId("company-badge")).toHaveAttribute("data-company", COMPANY);
  });

  test("الاعتماد المنقضي يُقرأ برمزه، وتُعرض الخطوة التالية", async ({ page }) => {
    await page.goto(signInUrl());
    await page.getByTestId("sign-in-token").fill(EXPIRED);
    await page.getByTestId("sign-in-submit").click();

    await expect(page.getByTestId("problem-panel")).toHaveAttribute("data-code", "auth.credential_expired");
    await expect(page.getByTestId("sign-in-next-step")).toBeVisible();
    await expect(page.locator('[data-testid="company-picker"]')).toHaveCount(0);
  });

  test("الاعتماد الذي لا يبلغ شركة يُرفض برمزه لا بقائمة فارغة", async ({ page }) => {
    await page.goto(signInUrl());
    await page.getByTestId("sign-in-token").fill(NO_COMPANY);
    await page.getByTestId("sign-in-submit").click();

    await expect(page.getByTestId("problem-panel")).toHaveAttribute(
      "data-code",
      "session.no_reachable_company"
    );
    /* ولا قائمة على الإطلاق: الفراغ يُقرأ «لا بيانات بعد» وهو تشخيص خاطئ. */
    await expect(page.locator('[data-testid="company-picker"]')).toHaveCount(0);
    await expect(page.getByTestId("sign-in-next-step")).toBeVisible();
  });

  test("الاعتماد المرفوض يُرفض برمز مختلف عن المنقضي", async ({ page }) => {
    await page.goto(signInUrl());
    await page.getByTestId("sign-in-token").fill("not-a-real-credential");
    await page.getByTestId("sign-in-submit").click();
    await expect(page.getByTestId("problem-panel")).toHaveAttribute("data-code", "auth.credential_rejected");
  });
});

/* ═══════════════════════ ٢ · أول كتابة ═══════════════════════════════ */

test.describe("القيد اليدوي — أول شاشة تكتب", () => {
  test("المبلغ الذي يُتلفه Number يغادر المتصفّح بايتاً ببايت", async ({ page }) => {
    /* الالتقاط على **جسم الطلب الخارج** — لا على ما يقوله الخادم. */
    const bodies: string[] = [];
    await page.route("**/journal-entries", async (route) => {
      bodies.push(route.request().postData() ?? "");
      await route.continue();
    });

    await page.goto(voucherUrl());
    await page.waitForSelector('[data-testid="voucher-screen"]');

    await page.getByTestId("voucher-date").fill("2026-08-15");
    await page.getByTestId("voucher-memo-ar").fill("قيد يدوي من الشاشة");
    await page.getByTestId("voucher-memo-en").fill("Manual voucher from the screen");
    await fillWhatTheLedgerDemands(page);

    const amounts = page.getByTestId("voucher-amount");
    expect(await amounts.count(), "عدد حقول المبالغ").toBe(2);
    await amounts.nth(0).fill(LOSSY);
    await amounts.nth(1).fill(LOSSY);

    await page.getByTestId("voucher-post").click();
    await page.waitForSelector('[data-testid="voucher-receipt"]');

    expect(bodies.length, "عدد الطلبات الملتقَطة").toBe(1);
    const sent = bodies[0];

    /* بايتاً ببايت، ونصّاً لا رمزاً رقمياً. */
    expect(sent).toContain('"amount":"' + LOSSY + '"');
    expect(sent).not.toContain('"amount":' + LOSSY);
    /* والقيمة التي كان Number سيُنتجها ليست في الطلب أصلاً. */
    expect(String(Number(LOSSY))).toBe("1000000000000.4012");
    expect(sent).not.toContain("1000000000000.4012");

    /* والحدث والمفتاح كما بنتهما الشاشة من العقد لا من نصّ حرّ. */
    expect(sent).toContain('"event":"ledger.manual_voucher.posted"');

    await expect(page.getByTestId("voucher-receipt")).toHaveAttribute("data-already-posted", "false");
    /* رقم القيد Int64String في العقد — أرقام لاتينية بلا بادئة حرفية. */
    await expect(page.getByTestId("receipt-number")).toHaveText(/^[0-9]+$/);
  });

  test("الإرسال الثاني بالمفتاح نفسه لا يُنشئ قيداً ثانياً ويُرى", async ({ page }) => {
    await page.goto(voucherUrl());
    await page.getByTestId("voucher-date").fill("2026-08-16");
    await page.getByTestId("voucher-memo-ar").fill("حصانة التكرار");
    await page.getByTestId("voucher-memo-en").fill("Idempotency");
    await fillWhatTheLedgerDemands(page);
    const amounts = page.getByTestId("voucher-amount");
    await amounts.nth(0).fill("250.7500");
    await amounts.nth(1).fill("250.7500");

    await page.getByTestId("voucher-post").click();
    await page.waitForSelector('[data-testid="voucher-receipt"]');

    const firstNumber = await page.getByTestId("receipt-number").innerText();
    const firstHash = await page.getByTestId("receipt-hash").innerText();
    await expect(page.getByTestId("voucher-receipt")).toHaveAttribute("data-already-posted", "false");

    await page.getByTestId("voucher-submit-again").click();
    await expect(page.getByTestId("voucher-receipt")).toHaveAttribute("data-already-posted", "true");

    /* الإيصال **ذاته**: رقم القيد وبصمته لم يتغيّرا. */
    await expect(page.getByTestId("receipt-number")).toHaveText(firstNumber);
    await expect(page.getByTestId("receipt-hash")).toHaveText(firstHash);
  });

  test("القيد غير المتوازن يُرفض برمزه، وتتصرّف الشاشة على الرمز", async ({ page }) => {
    await page.goto(voucherUrl());
    await page.getByTestId("voucher-date").fill("2026-08-17");
    await page.getByTestId("voucher-memo-ar").fill("غير متوازن");
    await page.getByTestId("voucher-memo-en").fill("Unbalanced");
    await fillWhatTheLedgerDemands(page);
    const amounts = page.getByTestId("voucher-amount");
    await amounts.nth(0).fill("100.0000");
    await amounts.nth(1).fill("90.0000");

    await page.getByTestId("voucher-post").click();

    await expect(page.getByTestId("problem-panel")).toHaveAttribute("data-code", "ledger.posting.unbalanced");
    await expect(page.getByTestId("voucher-unbalanced")).toBeVisible();
    await expect(page.locator('[data-testid="voucher-receipt"]')).toHaveCount(0);
  });

  test("مبلغ يخالف النحو المنشور يُوقف الإرسال قبل مغادرة المتصفّح", async ({ page }) => {
    let attempts = 0;
    await page.route("**/journal-entries", async (route) => {
      attempts += 1;
      await route.continue();
    });

    await page.goto(voucherUrl());
    await page.getByTestId("voucher-memo-ar").fill("نحو");
    await page.getByTestId("voucher-memo-en").fill("Grammar");
    const amounts = page.getByTestId("voucher-amount");

    /* خمس خانات عشرية، وصيغة أسّية، وفاصلة آلاف — كلها خارج النحو المنشور. */
    for (const bad of ["1.00000", "1e3", "1,000.00"]) {
      await amounts.nth(0).fill(bad);
      await amounts.nth(1).fill("1.0000");
      await expect(amounts.nth(0)).toHaveAttribute("aria-invalid", "true");
      await expect(page.getByTestId("voucher-post")).toBeDisabled();
    }

    expect(attempts, "طلبات غادرت المتصفّح بمبلغ مخالف").toBe(0);
  });

  test("ما لا يقوله العقد يصل رمزاً قابلاً للتصرّف: طرفٌ ناقص وبُعدٌ ناقص", async ({ page }) => {
    await page.goto(voucherUrl());
    await page.getByTestId("voucher-date").fill("2026-08-18");
    await page.getByTestId("voucher-memo-ar").fill("بلا طرف ولا فرع");
    await page.getByTestId("voucher-memo-en").fill("No party, no branch");
    const amounts = page.getByTestId("voucher-amount");
    await amounts.nth(0).fill("500.0000");
    await amounts.nth(1).fill("500.0000");

    /* ١ · بلا طرف: الحساب الضابط يرفض، والشاشة تقول الخطوة التالية. */
    await page.getByTestId("voucher-post").click();
    await expect(page.getByTestId("problem-panel")).toHaveAttribute(
      "data-code",
      "ledger.posting.missing_subledger"
    );
    await expect(page.getByTestId("voucher-needs-party")).toBeVisible();

    /* ٢ · بعد إضافة الطرف: يبقى البُعد الإلزامي ناقصاً، وبرمز آخر. */
    await page.getByTestId("voucher-subledger-kind").nth(0).selectOption("Treasury");
    await page.getByTestId("voucher-party").nth(0).fill("BANK-0001");
    await page.getByTestId("voucher-post").click();
    await expect(page.getByTestId("problem-panel")).toHaveAttribute(
      "data-code",
      "ledger.posting.guard.GR-COA-002"
    );
    await expect(page.getByTestId("voucher-needs-dimension")).toBeVisible();

    /* ٣ · وبعد الفرع: يُقبل — والمفتاح نفسه لم يتغيّر ولم يُرحَّل شيء قبل ذلك. */
    await page.getByTestId("voucher-branch").nth(1).fill("BR-01");
    await page.getByTestId("voucher-post").click();
    await page.waitForSelector('[data-testid="voucher-receipt"]');
    await expect(page.getByTestId("voucher-receipt")).toHaveAttribute("data-already-posted", "false");
  });

  test("شاشة الكتابة بلا منشأة تدلّ على الاختيار ولا تطلب معرّفاً", async ({ page }) => {
    await page.goto("/voucher?" + new URLSearchParams({ lang: "ar", baseUrl: MOCK }).toString());
    await expect(page.getByTestId("voucher-needs-company")).toBeVisible();
    await expect(page.getByTestId("voucher-go-sign-in")).toBeVisible();
    await expect(page.locator('[data-testid="voucher-screen"]')).toHaveCount(0);
  });

  test("مركز التكلفة يُقرأ من تأسيس المنشأة، والموقوف لا يُعرض", async ({ page }) => {
    await page.goto(voucherUrl());
    await page.waitForSelector('[data-testid="voucher-cost-center"]');
    const options = page.getByTestId("voucher-cost-center").first().locator("option");
    const values = await options.evaluateAll((els) => els.map((e) => (e as HTMLOptionElement).value));

    /* الافتراضي (حقلٌ محذوف) ومركزان عاملان — ولا أثر للموقوف. */
    expect(values).toEqual(["", "cc.main", "cc.branch"]);
  });

  test("الأدوار والجوانب مقروءة من العقد لا مكتوبة في الشاشة", async ({ page }) => {
    await page.goto(voucherUrl());
    const roles = await page
      .getByTestId("voucher-role")
      .first()
      .locator("option")
      .evaluateAll((els) => els.map((e) => (e as HTMLOptionElement).value));

    /* أربعة عشر دوراً كما ينشرها العقد — والعدد يتغيّر بتغيّره لا بتحرير شاشة. */
    expect(roles.length).toBe(14);
    expect(roles).toContain("Settlement");
    expect(roles).toContain("NetAmount");

    const sides = await page
      .getByTestId("voucher-side")
      .first()
      .locator("option")
      .evaluateAll((els) => els.map((e) => (e as HTMLOptionElement).value));
    expect(sides).toEqual(["Debit", "Credit"]);
  });
});

/* ═════════════════ ٣ · مصفوفة العرض للشاشتين الجديدتين ═══════════════ */

test.describe("مصفوفة اللغات والمظاهر والعروض — الدخول والقيد", () => {
  for (const locale of LOCALES) {
    for (const theme of THEMES) {
      for (const width of WIDTHS) {
        test(`الدخول · ${locale.code} · ${theme} · ${width}px`, async ({ page }) => {
          await page.setViewportSize({ width, height: 900 });
          await withTheme(page, theme);
          await page.goto(signInUrl(locale.code));
          await page.getByTestId("sign-in-token").fill(TOKEN);
          await page.getByTestId("sign-in-submit").click();
          await page.waitForSelector('[data-testid="company-picker"]');

          await expect(page.locator("html")).toHaveAttribute("dir", locale.dir);
          await expect(page.locator("html")).toHaveAttribute("lang", locale.code);
          await expect(page.locator("html")).toHaveAttribute("data-theme", theme);

          const options = page.locator('[data-testid="company-option"]');
          expect(await options.count(), "منشآت مرسومة").toBe(2);

          expect(await horizontalOverflow(page), "انزلاق أفقي").toBeLessThanOrEqual(1);

          /* السجلّ العربي معروض في اللغات الأربع — لا يُستبدل بترجمته. */
          await expect(
            page.locator('[data-testid="company-option"][data-state="Ready"]')
              .getByTestId("company-name-record")
          ).toHaveText("مؤسسة بابل للتجارة");

          await page.screenshot({
            path: `test-results/entry/signin-${locale.code}-${theme}-${width}.png`,
          });
        });

        test(`القيد · ${locale.code} · ${theme} · ${width}px`, async ({ page }) => {
          await page.setViewportSize({ width, height: 900 });
          await withTheme(page, theme);
          await page.goto(voucherUrl(locale.code));
          await page.waitForSelector('[data-testid="voucher-screen"]');

          await expect(page.locator("html")).toHaveAttribute("dir", locale.dir);
          await expect(page.locator("html")).toHaveAttribute("lang", locale.code);
          await expect(page.locator("html")).toHaveAttribute("data-theme", theme);

          const amounts = page.getByTestId("voucher-amount");
          expect(await amounts.count(), "حقول مالية مرسومة").toBe(2);
          await amounts.nth(0).fill(LOSSY);
          await amounts.nth(1).fill("999.5000");

          expect(await horizontalOverflow(page), "انزلاق أفقي").toBeLessThanOrEqual(1);

          /* الخانة المالية معزولة ومحاذاة إلى النهاية بأرقام جدولية — في اللغات الأربع. */
          const styles = await amounts.evaluateAll((els) =>
            els.map((el) => {
              const s = getComputedStyle(el);
              return {
                direction: s.direction,
                unicodeBidi: s.unicodeBidi,
                textAlign: s.textAlign,
                variant: s.fontVariantNumeric,
              };
            })
          );
          expect(styles.length).toBe(2);
          for (const s of styles) {
            expect(s.direction).toBe("ltr");
            expect(s.unicodeBidi).toBe("isolate");
            expect(s.textAlign).toBe("end");
            expect(s.variant).toContain("tabular-nums");
          }

          /* والقيمة كما كُتبت — لا تُنسَّق ولا تُقرَّب في حقل الإدخال. */
          await expect(amounts.nth(0)).toHaveValue(LOSSY);

          /* الحافّتان النهائيتان للحقلين المالِيَّين واحدة، فالفاصلة تحت أختها. */
          const edges = await amounts.evaluateAll((els) =>
            els.map((el) => Math.round(el.getBoundingClientRect().right))
          );
          expect(new Set(edges).size, "حواف الحقول المالية").toBe(1);

          await page.screenshot({
            path: `test-results/entry/voucher-${locale.code}-${theme}-${width}.png`,
          });
        });
      }
    }
  }
});
