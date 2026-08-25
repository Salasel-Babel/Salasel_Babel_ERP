/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     e2ba8112a2421c49813d9475f8849856d96e8af4f94d9cc0d071498442a6e2ca
   المولّد · generator: web/scripts/generate-client.mjs

   لإعادة التوليد:  npm run gen
   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)

   هوية العقد الذي وُلِّد منه هذا العميل.
   ═══════════════════════════════════════════════════════════════════════ */

export const CONTRACT = {
  title: "سلاسل بابل — سطح دفتر الأستاذ / Salasel Babel — Ledger API",
  version: "v1",
  openapi: "3.1.0",
  /** بصمة بايتات contracts/openapi/v1.json وقت التوليد. */
  sourceSha256: "e2ba8112a2421c49813d9475f8849856d96e8af4f94d9cc0d071498442a6e2ca",
  operationCount: 6,
  schemaCount: 22,
  operations: [{"id":"health","method":"GET","path":"/health"},{"id":"postJournalEntry","method":"POST","path":"/api/v1/companies/{companyId}/journal-entries"},{"id":"readJournalEntry","method":"GET","path":"/api/v1/companies/{companyId}/journal-entries/{entryId}"},{"id":"readTrialBalance","method":"GET","path":"/api/v1/companies/{companyId}/trial-balance"},{"id":"reverseJournalEntry","method":"POST","path":"/api/v1/companies/{companyId}/journal-entries/{entryId}/reversal"},{"id":"verifyLedgerChain","method":"GET","path":"/api/v1/companies/{companyId}/ledger-chain/verification"}],
} as const;
