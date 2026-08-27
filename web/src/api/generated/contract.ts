/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     8a33528a07e07e6b03c5ee5d6412ccbe27809ed11ed5c811be93ba3132068135
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
  sourceSha256: "8a33528a07e07e6b03c5ee5d6412ccbe27809ed11ed5c811be93ba3132068135",
  operationCount: 16,
  schemaCount: 36,
  operations: [{"id":"addCostCenter","method":"POST","path":"/api/v1/companies/{companyId}/cost-centers"},{"id":"admitDocument","method":"POST","path":"/api/v1/companies/{companyId}/document-shapes/{documentType}/admissions"},{"id":"health","method":"GET","path":"/health"},{"id":"initialiseCompanySetup","method":"PUT","path":"/api/v1/companies/{companyId}/setup"},{"id":"postJournalEntry","method":"POST","path":"/api/v1/companies/{companyId}/journal-entries"},{"id":"readCapabilityProfile","method":"GET","path":"/api/v1/companies/{companyId}/capability-profile"},{"id":"readCompanySetup","method":"GET","path":"/api/v1/companies/{companyId}/setup"},{"id":"readDocumentShape","method":"GET","path":"/api/v1/companies/{companyId}/document-shapes/{documentType}"},{"id":"readJournalEntry","method":"GET","path":"/api/v1/companies/{companyId}/journal-entries/{entryId}"},{"id":"readSession","method":"GET","path":"/api/v1/session"},{"id":"readTrialBalance","method":"GET","path":"/api/v1/companies/{companyId}/trial-balance"},{"id":"renameCostCenter","method":"PUT","path":"/api/v1/companies/{companyId}/cost-centers/{costCenterCode}"},{"id":"reverseJournalEntry","method":"POST","path":"/api/v1/companies/{companyId}/journal-entries/{entryId}/reversal"},{"id":"suspendCostCenter","method":"POST","path":"/api/v1/companies/{companyId}/cost-centers/{costCenterCode}/suspension"},{"id":"verifyLedgerChain","method":"GET","path":"/api/v1/companies/{companyId}/ledger-chain/verification"},{"id":"writeCapabilityProfile","method":"PUT","path":"/api/v1/companies/{companyId}/capability-profile"}],
} as const;
