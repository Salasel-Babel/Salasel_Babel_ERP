/* ═══════════════════════════════════════════════════════════════════════════
   حدُّ المقطع الحرّ في المتصفّح — **نظير `SlotSpansAreBoundedOrRefused` في الخادم**.
   ───────────────────────────────────────────────────────────────────────────
   والمتصفّح هو المسار **الحيّ**: على عنوانٍ غير مؤمَّن لا يعمل الميكروفون، فالتفريغ
   المكتوب هو كلُّ ما يصل — وهو الذي ابتلع اسمُ العميل فيه شرطَ صاحب المنتج. فإصلاحٌ
   في الخادم وحده انحرافٌ لا يظهر إلا على شاشته (ADR-0030 خامساً).
   ═══════════════════════════════════════════════════════════════════════════ */
import { describe, expect, it } from "vitest";
import { readCommand, NAME_WORD_LIMIT, type VoiceResolution } from "../src/voice/command";
import { VOICE_INTENTS } from "../src/voice/catalogue";

const TODAY = "2026-08-31";
const options = { today: TODAY, statutoryTaxRate: "0.15" };

/** جملةُ صاحب المنتج كما نطقها حرفاً بحرف. */
const OWNER =
  "سجل سند قبض من شركة المسار الامثل فان لم تجدها انشيء لها حسابا ثم سند قبض بقيمة 20000 ريال سعودي بتاريخ اليوم طبعاً";

/** الشرط الذي ابتُلع يومَها في اسم العميل. */
const CLAUSE = "فان لم تجدها انشيء لها حسابا";

function read(transcript: string, entities?: Record<string, readonly string[]>): VoiceResolution {
  const outcome = readCommand(transcript, entities ? { ...options, entities } : options);
  expect(outcome.ok, JSON.stringify(outcome)).toBe(true);
  if (!outcome.ok) throw new Error("unreachable");
  return outcome.resolution;
}

function valueOf(resolution: VoiceResolution, slot: string): string | null {
  return resolution.slots.find((value) => value.name === slot)?.text ?? null;
}

describe("حدُّ المقطع الحرّ — يُقرَّر بسجلٍّ أو يُرفض", () => {
  it("جملة صاحب المنتج ترفض ولا تبتلع الشرط في اسم العميل", () => {
    const resolution = read(OWNER);

    expect(valueOf(resolution, "customer")).toBeNull();
    expect(resolution.missingSlots).toContain("customer");
    expect(resolution.faults).toContain("ai.voice.slot_boundary_not_found");

    /* وما فُهم يبقى مفهوماً: الرفض على شريحةٍ لا يُسقط أخواتها. */
    expect(valueOf(resolution, "amount")).toBe("20000");
    expect(valueOf(resolution, "receivedOn")).toBe(TODAY);
  });

  it("ولا شريحة في جملة صاحب المنتج تحمل كلمة من الشرط", () => {
    const resolution = read(OWNER);
    for (const value of resolution.slots) {
      for (const word of CLAUSE.split(" ")) {
        expect(value.text, value.name).not.toContain(word);
      }
    }
  });

  /* أدواتٌ خليجية **ليست في أي قائمة إيقاف**، ولا يجوز أن تصير فيها. */
  const colloquial: readonly (readonly [string, string])[] = [
    ["سجل سند قبض من شركة المسار الامثل وإذا ما لقيتها سو لها حساب كاش اليوم", "customer"],
    ["قبضت من العميل شركة المسار الامثل بعد ما راجعت الحساب نقدا اليوم", "customer"],
    ["سجل سند قبض من مؤسسة النور لين تشوف حسابها عندك نقد اليوم", "customer"],
    ["سجل سند قبض من مؤسسة النور عشان نقفل الشهر ونرتاح نقد اليوم", "customer"],
    ["استلمت من العميل مؤسسة النور ولا تنسى تسجلها بالدفتر نقد اليوم", "customer"],
    ["سجل سند صرف للمورد مؤسسة الرياض لو ما كان عندك ملف سوّ له ملف نقد اليوم", "supplier"],
    ["كم رصيد الصنف حديد تسليح لو ما لقيته بالمستودع الرئيسي شوف الفرعي", "item"],
    ["اصرف سلفة للموظف احمد الغامدي وإذا ما عنده رصيد خلها على الشهر الجاي بمبلغ 2000 اليوم", "employee"],
  ];

  it.each(colloquial)("العامّية الخليجية ترفض كما ترفض الفصحى: %s", (transcript, slot) => {
    const resolution = read(transcript);
    expect(valueOf(resolution, slot)).toBeNull();
    expect(resolution.faults).toContain("ai.voice.slot_boundary_not_found");
  });

  it("خصومة: اسمان في مقطعٍ واحد يرفضان ولا يُدمجان", () => {
    const resolution = read("سجل سند قبض من شركة المسار الامثل ومؤسسة النور نقد بمبلغ 20000 اليوم");
    expect(valueOf(resolution, "customer")).toBeNull();
    expect(resolution.faults).toContain("ai.voice.slot_boundary_not_found");
  });

  it("خصومة: الفاصلة تحدّ الاسم ولا تُحذف", () => {
    const resolution = read(
      "سجل سند قبض من شركة المسار الامثل، فإن لم تجدها أنشئ لها حسابا. نقد بمبلغ 20000 اليوم"
    );
    expect(valueOf(resolution, "customer")).toBe("شركة المسار الامثل");
    expect(valueOf(resolution, "method")).toBe("نقد");
    expect(resolution.missingSlots).toHaveLength(0);
  });

  it("خصومة: صيغة صرفية لقيمة مغلقة ترفض ولا تلتصق بالاسم", () => {
    const resolution = read("سجل سند قبض من شركة المسار الامثل نقدا بمبلغ 20000 بتاريخ اليوم");
    expect(valueOf(resolution, "customer")).toBeNull();
    expect(resolution.faults).toContain("ai.voice.slot_boundary_not_found");
  });

  it("خصومة: ذيلٌ قصير يمرّ على الأرضية ويُحَدّ بالسجلّ — والثقب مقيسٌ لا مخفيّ", () => {
    const transcript = "سجل سند قبض من مؤسسة النور طبعا نقد بمبلغ 20000 اليوم";

    expect(valueOf(read(transcript), "customer")).toBe("مؤسسة النور طبعا");

    const bounded = read(transcript, { Customer: ["مؤسسة النور"] });
    expect(valueOf(bounded, "customer")).toBe("مؤسسة النور");
    expect(bounded.residue).toEqual([{ slotName: "customer", text: "طبعا" }]);
    expect(bounded.faults).toContain("ai.voice.residue_not_understood");
  });

  it("بالسجلّ يُقرأ اسم صاحب المنتج ويُسمّى الشرط فضلةً", () => {
    const resolution = read(OWNER, { Customer: ["شركة المسار الأمثل"] });
    expect(valueOf(resolution, "customer")).toBe("شركة المسار الامثل");
    expect(resolution.residue).toEqual([{ slotName: "customer", text: CLAUSE }]);
    expect(resolution.faults).toContain("ai.voice.residue_not_understood");
  });

  it("بالسجلّ اسمٌ غير مسجَّل يُرفض ولا يُقارَب بأقرب شبيه", () => {
    const resolution = read("سجل سند قبض من مؤسسة النور نقد بمبلغ 20000 اليوم", {
      Customer: ["مؤسسة النورين"],
    });
    expect(valueOf(resolution, "customer")).toBeNull();
    expect(resolution.faults).toContain("ai.voice.name_not_in_register");
  });

  it("بالسجلّ اسمان متعادلان يُرفضان ولا يُقترع بينهما", () => {
    const resolution = read("سجل سند قبض من مؤسسة النور نقد بمبلغ 20000 اليوم", {
      Customer: ["مؤسسة النور", "مؤسسه النور"],
    });
    expect(valueOf(resolution, "customer")).toBeNull();
    expect(resolution.faults).toContain("ai.voice.slot_boundary_ambiguous");
  });

  it("بالسجلّ الأخصّ يفوز حين يكون أحدهما بادئة الآخر", () => {
    const resolution = read("سجل سند قبض من شركة المسار الأمثل نقد بمبلغ 20000 اليوم", {
      Customer: ["شركة المسار", "شركة المسار الأمثل"],
    });
    expect(valueOf(resolution, "customer")).toBe("شركة المسار الامثل");
    expect(resolution.residue).toHaveLength(0);
  });

  it("النثر الحرّ لا يُحَدّ بعرض الاسم", () => {
    const journal = VOICE_INTENTS.find((intent) => intent.id === "accounting.journal_entry.draft");
    expect(journal?.slots.find((slot) => slot.name === "description")?.entity).toBe("None");

    const resolution = read("سجل قيد يومية بيان اقفال حسابات فرعية متراكمة قديمة بمبلغ 5000 اليوم");
    const text = valueOf(resolution, "description");
    expect(text).not.toBeNull();
    expect((text ?? "").split(" ").length).toBeGreaterThan(NAME_WORD_LIMIT);
  });

  it("كل شريحة نصٍّ في المرآة إمّا موسومة بسجلّ وإمّا نثرٌ مُسمّى", () => {
    const prose = new Set(["description", "reason"]);
    const untagged: string[] = [];
    let counted = 0;

    for (const intent of VOICE_INTENTS) {
      for (const slot of intent.slots) {
        if (slot.kind !== "Text") continue;
        counted++;
        if (slot.entity === "None" && !prose.has(slot.name)) untagged.push(intent.id + "/" + slot.name);
      }
    }

    expect(counted).toBeGreaterThanOrEqual(40);
    expect(untagged).toEqual([]);
  });
});
