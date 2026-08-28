/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     63c3b477e2e6dbcf9ca20df58b2cb06a6f649c6754d096b4a261c9544948c1f6
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
  sourceSha256: "63c3b477e2e6dbcf9ca20df58b2cb06a6f649c6754d096b4a261c9544948c1f6",
  operationCount: 33,
  schemaCount: 52,
  operations: [{"id":"addCostCenter","method":"POST","path":"/api/v1/companies/{companyId}/cost-centers"},{"id":"addCustomer","method":"POST","path":"/api/v1/companies/{companyId}/customers"},{"id":"addSupplier","method":"POST","path":"/api/v1/companies/{companyId}/suppliers"},{"id":"admitDocument","method":"POST","path":"/api/v1/companies/{companyId}/document-shapes/{documentType}/admissions"},{"id":"draftCreditNote","method":"POST","path":"/api/v1/companies/{companyId}/credit-notes"},{"id":"draftExpenseBill","method":"POST","path":"/api/v1/companies/{companyId}/supplier-bills"},{"id":"draftSalesInvoice","method":"POST","path":"/api/v1/companies/{companyId}/sales-invoices"},{"id":"health","method":"GET","path":"/health"},{"id":"initialiseCompanySetup","method":"PUT","path":"/api/v1/companies/{companyId}/setup"},{"id":"postCreditNote","method":"POST","path":"/api/v1/companies/{companyId}/credit-notes/{creditNoteId}/posting"},{"id":"postJournalEntry","method":"POST","path":"/api/v1/companies/{companyId}/journal-entries"},{"id":"postSalesInvoice","method":"POST","path":"/api/v1/companies/{companyId}/sales-invoices/{invoiceId}/posting"},{"id":"postSupplierBill","method":"POST","path":"/api/v1/companies/{companyId}/supplier-bills/{billId}/posting"},{"id":"readCapabilityProfile","method":"GET","path":"/api/v1/companies/{companyId}/capability-profile"},{"id":"readChartOfAccounts","method":"GET","path":"/api/v1/companies/{companyId}/chart-of-accounts"},{"id":"readCompanySetup","method":"GET","path":"/api/v1/companies/{companyId}/setup"},{"id":"readCustomer","method":"GET","path":"/api/v1/companies/{companyId}/customers/{customerId}"},{"id":"readDocsPage","method":"GET","path":"/docs"},{"id":"readDocumentShape","method":"GET","path":"/api/v1/companies/{companyId}/document-shapes/{documentType}"},{"id":"readJournalEntry","method":"GET","path":"/api/v1/companies/{companyId}/journal-entries/{entryId}"},{"id":"readPayablesAging","method":"GET","path":"/api/v1/companies/{companyId}/payables-aging"},{"id":"readPublishedContract","method":"GET","path":"/openapi/v1.json"},{"id":"readReceivablesAging","method":"GET","path":"/api/v1/companies/{companyId}/receivables-aging"},{"id":"readSalesInvoice","method":"GET","path":"/api/v1/companies/{companyId}/sales-invoices/{invoiceId}"},{"id":"readSession","method":"GET","path":"/api/v1/session"},{"id":"readSupplier","method":"GET","path":"/api/v1/companies/{companyId}/suppliers/{supplierId}"},{"id":"readSupplierBill","method":"GET","path":"/api/v1/companies/{companyId}/supplier-bills/{billId}"},{"id":"readTrialBalance","method":"GET","path":"/api/v1/companies/{companyId}/trial-balance"},{"id":"renameCostCenter","method":"PUT","path":"/api/v1/companies/{companyId}/cost-centers/{costCenterCode}"},{"id":"reverseJournalEntry","method":"POST","path":"/api/v1/companies/{companyId}/journal-entries/{entryId}/reversal"},{"id":"suspendCostCenter","method":"POST","path":"/api/v1/companies/{companyId}/cost-centers/{costCenterCode}/suspension"},{"id":"verifyLedgerChain","method":"GET","path":"/api/v1/companies/{companyId}/ledger-chain/verification"},{"id":"writeCapabilityProfile","method":"PUT","path":"/api/v1/companies/{companyId}/capability-profile"}],
} as const;
