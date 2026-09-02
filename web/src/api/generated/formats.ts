/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     0229f4a0df345dae90832ad0ae10d4959e31e839c0fe1c495ab9a8fe5d48af7a
   المولّد · generator: web/scripts/generate-client.mjs

   لإعادة التوليد:  npm run gen
   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)

   الأنماط المنشورة، منقولةً حرفياً من العقد. المدقّقات تستعملها ولا تُعيد كتابتها.
   ═══════════════════════════════════════════════════════════════════════ */

export const PARAM_API_V1_COMPANIES_COMPANYID_COST_CENTERS_COSTCENTERCODE_SUSPENSION_costCenterCode = "^[a-z0-9._]{1,32}$";
export const PARAM_API_V1_COMPANIES_COMPANYID_COST_CENTERS_COSTCENTERCODE_SUSPENSION_costCenterCode_RE = new RegExp("^[a-z0-9._]{1,32}$");
export const PARAM_API_V1_COMPANIES_COMPANYID_COST_CENTERS_COSTCENTERCODE_costCenterCode = "^[a-z0-9._]{1,32}$";
export const PARAM_API_V1_COMPANIES_COMPANYID_COST_CENTERS_COSTCENTERCODE_costCenterCode_RE = new RegExp("^[a-z0-9._]{1,32}$");
export const PARAM_downloadAttachment_ticket = "^[A-Za-z0-9_-]{16,512}$";
export const PARAM_downloadAttachment_ticket_RE = new RegExp("^[A-Za-z0-9_-]{16,512}$");
export const PARAM_listAttachments_skip = "^[0-9]{1,7}$";
export const PARAM_listAttachments_skip_RE = new RegExp("^[0-9]{1,7}$");
export const PARAM_listAttachments_sourceDocumentType = "^[a-z0-9._]{1,64}$";
export const PARAM_listAttachments_sourceDocumentType_RE = new RegExp("^[a-z0-9._]{1,64}$");
export const PARAM_listAttachments_take = "^[0-9]{1,7}$";
export const PARAM_listAttachments_take_RE = new RegExp("^[0-9]{1,7}$");
export const PARAM_readInventoryValuation_asOf = "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$";
export const PARAM_readInventoryValuation_asOf_RE = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
export const PARAM_readPayablesAging_asOf = "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$";
export const PARAM_readPayablesAging_asOf_RE = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
export const PARAM_readReceivablesAging_asOf = "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$";
export const PARAM_readReceivablesAging_asOf_RE = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
export const PARAM_readRetentionRegister_asOf = "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$";
export const PARAM_readRetentionRegister_asOf_RE = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
export const PARAM_readSubcontractorStatement_asOf = "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$";
export const PARAM_readSubcontractorStatement_asOf_RE = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
export const PARAM_readTenantArrearsAging_asOf = "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$";
export const PARAM_readTenantArrearsAging_asOf_RE = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
export const PARAM_readTrialBalance_period = "^[0-9]{4}-(0[1-9]|1[0-2])$";
export const PARAM_readTrialBalance_period_RE = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])$");
export const PARAM_reconcileEmployeeSubledger_asOf = "^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$";
export const PARAM_reconcileEmployeeSubledger_asOf_RE = new RegExp("^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$");
export const PARAM_verifyLedgerChain_fiscalYear = "^[0-9]{4}$";
export const PARAM_verifyLedgerChain_fiscalYear_RE = new RegExp("^[0-9]{4}$");
export const SCHEMA_ExchangeRate = "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,8})?$";
export const SCHEMA_ExchangeRate_RE = new RegExp("^-?(0|[1-9][0-9]*)(\\.[0-9]{1,8})?$");
export const SCHEMA_Int64String = "^-?(0|[1-9][0-9]*)$";
export const SCHEMA_Int64String_RE = new RegExp("^-?(0|[1-9][0-9]*)$");
export const SCHEMA_Magnitude = "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,6})?$";
export const SCHEMA_Magnitude_RE = new RegExp("^-?(0|[1-9][0-9]*)(\\.[0-9]{1,6})?$");
export const SCHEMA_Money = "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,4})?$";
export const SCHEMA_Money_RE = new RegExp("^-?(0|[1-9][0-9]*)(\\.[0-9]{1,4})?$");
export const SCHEMA_Quantity = "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,4})?$";
export const SCHEMA_Quantity_RE = new RegExp("^-?(0|[1-9][0-9]*)(\\.[0-9]{1,4})?$");
export const SCHEMA_Rate = "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,8})?$";
export const SCHEMA_Rate_RE = new RegExp("^-?(0|[1-9][0-9]*)(\\.[0-9]{1,8})?$");
export const SCHEMA_TaxRate = "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,8})?$";
export const SCHEMA_TaxRate_RE = new RegExp("^-?(0|[1-9][0-9]*)(\\.[0-9]{1,8})?$");
export const SCHEMA_UnitCost = "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,6})?$";
export const SCHEMA_UnitCost_RE = new RegExp("^-?(0|[1-9][0-9]*)(\\.[0-9]{1,6})?$");
