/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     bcd3d62ac30871329286007435f873c35108a6a9be4a114fa18280708835e2a1
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
  sourceSha256: "bcd3d62ac30871329286007435f873c35108a6a9be4a114fa18280708835e2a1",
  operationCount: 17,
  schemaCount: 38,
  operations: [{"id":"addCostCenter","method":"POST","path":"/api/v1/companies/{companyId}/cost-centers"},{"id":"admitDocument","method":"POST","path":"/api/v1/companies/{companyId}/document-shapes/{documentType}/admissions"},{"id":"health","method":"GET","path":"/health"},{"id":"initialiseCompanySetup","method":"PUT","path":"/api/v1/companies/{companyId}/setup"},{"id":"postJournalEntry","method":"POST","path":"/api/v1/companies/{companyId}/journal-entries"},{"id":"readCapabilityProfile","method":"GET","path":"/api/v1/companies/{companyId}/capability-profile"},{"id":"readChartOfAccounts","method":"GET","path":"/api/v1/companies/{companyId}/chart-of-accounts"},{"id":"readCompanySetup","method":"GET","path":"/api/v1/companies/{companyId}/setup"},{"id":"readDocumentShape","method":"GET","path":"/api/v1/companies/{companyId}/document-shapes/{documentType}"},{"id":"readJournalEntry","method":"GET","path":"/api/v1/companies/{companyId}/journal-entries/{entryId}"},{"id":"readSession","method":"GET","path":"/api/v1/session"},{"id":"readTrialBalance","method":"GET","path":"/api/v1/companies/{companyId}/trial-balance"},{"id":"renameCostCenter","method":"PUT","path":"/api/v1/companies/{companyId}/cost-centers/{costCenterCode}"},{"id":"reverseJournalEntry","method":"POST","path":"/api/v1/companies/{companyId}/journal-entries/{entryId}/reversal"},{"id":"suspendCostCenter","method":"POST","path":"/api/v1/companies/{companyId}/cost-centers/{costCenterCode}/suspension"},{"id":"verifyLedgerChain","method":"GET","path":"/api/v1/companies/{companyId}/ledger-chain/verification"},{"id":"writeCapabilityProfile","method":"PUT","path":"/api/v1/companies/{companyId}/capability-profile"}],
} as const;
