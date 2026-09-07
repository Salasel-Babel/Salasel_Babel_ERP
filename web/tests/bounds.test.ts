/* حدودُ النصّ المكتوب تُقرأ من العقد لا تُكتب في الشاشة (ADR-0091). */
import { describe, expect, it } from "vitest";
import { maxLengthOf, minLengthOf } from "../src/api/bounds";
import { SCHEMAS } from "../src/api/generated/runtime-schema";

describe("حدودُ العقد", () => {
  it("أدنى طول السبب يأتي من العقد للشاشتين، وهو الرقم نفسه في الموضعين", () => {
    const suspend = minLengthOf("SuspendCostCenterRequest", "reason");
    const withdraw = minLengthOf("PutCapabilityProfileRequest", "withdrawalReason");
    expect(suspend).toBeGreaterThan(0);
    expect(suspend).toBe(withdraw);
    expect(maxLengthOf("SuspendCostCenterRequest", "reason")).toBeGreaterThan(suspend);
  });

  it("حقلٌ بلا حدٍّ مُعلَن يُرفض باسمه ولا يُخترَع له رقم", () => {
    expect(() => minLengthOf("CompanySetup", "nameAr")).toThrowError(/minLength/);
    expect(() => minLengthOf("NoSuchSchema", "x")).toThrowError(/minLength/);
    /* والمولّد يحمل الحدّين معاً حيث يعلنهما العقد. */
    expect(SCHEMAS.SuspendCostCenterRequest?.fields.reason?.mx).toBeDefined();
  });
});
