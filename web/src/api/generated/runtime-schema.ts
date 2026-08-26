/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     90d076ad3cc6c558ce905171467482b90038f42e9a77b8c4fe5a9aa8eaa99366
   المولّد · generator: web/scripts/generate-client.mjs

   لإعادة التوليد:  npm run gen
   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)

   واصفات وقت التشغيل: لكل مخطّط، أي حقوله مال وأيها أعداد طويلة وأيها مخطّط آخر.
   فاكّ التشفير يمشي عليها فيلفّ المال — ولا موضع مال واحد مكتوب بيد.
   ═══════════════════════════════════════════════════════════════════════ */

export type FieldKind = "plain" | "money" | "brand" | "ref" | "array";

export interface FieldShape {
  /** النوع · kind */ k: FieldKind;
  /** اسم المخطّط عند k==="ref" · referenced schema */ r?: string;
  /** اسم الصيغة المحتجزة عند k==="brand" · branded format */ b?: string;
  /** شكل العنصر عند k==="array" · item shape */ i?: FieldShape;
  /** يقبل null · nullable */ n?: boolean;
}

export interface SchemaShape {
  /** الحقول الإلزامية · required properties */ required: readonly string[];
  /** شكل كل حقل معروف · shape of each known property */ fields: Readonly<Record<string, FieldShape>>;
}

export const SCHEMAS: Readonly<Record<string, SchemaShape>> = {
  AdmitDocumentRequest: { required: ["fields"], fields: {"fields":{"i":{"k":"plain"},"k":"array"}} },
  ApiError: { required: ["code","field","messageAr","messageEn"], fields: {"code":{"k":"plain"},"field":{"k":"plain","n":true},"messageAr":{"k":"plain"},"messageEn":{"k":"plain"}} },
  CapabilityProfile: { required: ["documents"], fields: {"documents":{"i":{"k":"ref","r":"DocumentShape"},"k":"array"}} },
  CapabilitySwitch: { required: ["capability","enabled"], fields: {"capability":{"k":"plain"},"enabled":{"k":"plain"}} },
  ChainVerification: { required: ["checked","detail","firstDivergentSequence","ok","reasonAr","verdict"], fields: {"checked":{"k":"plain"},"detail":{"k":"plain","n":true},"firstDivergentSequence":{"b":"Int64String","k":"brand","n":true},"ok":{"k":"plain"},"reasonAr":{"k":"plain"},"verdict":{"k":"plain"}} },
  ClosedPeriodAuthorisation: { required: ["authorisedBy","permissionCode","reason"], fields: {"authorisedBy":{"k":"plain"},"permissionCode":{"k":"plain"},"reason":{"k":"ref","r":"LocalizedText"}} },
  CompanySetup: { required: ["costCenters","decimalPlaces","defaultCostCenter","nameAr","nameTranslations"], fields: {"costCenters":{"i":{"k":"ref","r":"CostCenter"},"k":"array"},"decimalPlaces":{"k":"plain"},"defaultCostCenter":{"k":"plain"},"nameAr":{"k":"plain"},"nameTranslations":{"i":{"k":"ref","r":"NameValue"},"k":"array"}} },
  CostCenter: { required: ["code","isDefault","nameAr","nameTranslations","state","suspensionReason"], fields: {"code":{"k":"plain"},"isDefault":{"k":"plain"},"nameAr":{"k":"plain"},"nameTranslations":{"i":{"k":"ref","r":"NameValue"},"k":"array"},"state":{"k":"plain"},"suspensionReason":{"k":"plain"}} },
  CostCenterNameRequest: { required: ["nameAr"], fields: {"nameAr":{"k":"plain"},"nameTranslations":{"i":{"k":"ref","r":"NameValue"},"k":"array"}} },
  DocumentAdmission: { required: ["admitted","documentType","fields"], fields: {"admitted":{"k":"plain"},"documentType":{"k":"plain"},"fields":{"i":{"k":"plain"},"k":"array"}} },
  DocumentProfile: { required: ["capabilities","documentType"], fields: {"capabilities":{"i":{"k":"ref","r":"CapabilitySwitch"},"k":"array"},"defaults":{"i":{"k":"ref","r":"NameValue"},"k":"array"},"documentType":{"k":"plain"}} },
  DocumentShape: { required: ["availableCapabilities","defaults","documentType","enabledCapabilities","fields","module","nameAr","nameKey"], fields: {"availableCapabilities":{"i":{"k":"plain"},"k":"array"},"defaults":{"i":{"k":"ref","r":"NameValue"},"k":"array"},"documentType":{"k":"plain"},"enabledCapabilities":{"i":{"k":"plain"},"k":"array"},"fields":{"i":{"k":"plain"},"k":"array"},"module":{"k":"plain"},"nameAr":{"k":"plain"},"nameKey":{"k":"plain"}} },
  HealthResponse: { required: ["apiVersion","calendar","culture","status"], fields: {"apiVersion":{"k":"plain"},"calendar":{"k":"plain"},"culture":{"k":"plain"},"status":{"k":"plain"}} },
  InitialiseCompanySetupRequest: { required: ["companyNameAr","costCenters","decimalPlaces"], fields: {"companyNameAr":{"k":"plain"},"companyNameTranslations":{"i":{"k":"ref","r":"NameValue"},"k":"array"},"costCenters":{"k":"plain"},"decimalPlaces":{"k":"plain"},"firstCostCenterNameAr":{"k":"plain","n":true},"firstCostCenterTranslations":{"i":{"k":"ref","r":"NameValue"},"k":"array"}} },
  JournalEntry: { required: ["book","chainSequence","currency","entryDate","entryHash","entryId","entryNumber","lines","memoAr","memoEn","periodCode","reversesEntryId","status"], fields: {"book":{"k":"plain"},"chainSequence":{"b":"Int64String","k":"brand"},"currency":{"k":"plain"},"entryDate":{"k":"plain"},"entryHash":{"k":"plain"},"entryId":{"k":"plain"},"entryNumber":{"b":"Int64String","k":"brand"},"lines":{"i":{"k":"ref","r":"JournalLine"},"k":"array"},"memoAr":{"k":"plain"},"memoEn":{"k":"plain"},"periodCode":{"k":"plain"},"reversesEntryId":{"k":"plain","n":true},"status":{"k":"plain"}} },
  JournalLine: { required: ["credit","currency","debit","descriptionAr","descriptionEn","lineNo","qualifier","role"], fields: {"credit":{"k":"money"},"currency":{"k":"plain"},"debit":{"k":"money"},"descriptionAr":{"k":"plain"},"descriptionEn":{"k":"plain"},"lineNo":{"k":"plain"},"qualifier":{"k":"plain"},"role":{"k":"plain"}} },
  LocalizedText: { required: ["ar","en"], fields: {"ar":{"k":"plain"},"en":{"k":"plain"}} },
  NameValue: { required: ["name","value"], fields: {"name":{"k":"plain"},"value":{"k":"plain"}} },
  NamedAmount: { required: ["name","value"], fields: {"name":{"k":"plain"},"value":{"k":"money"}} },
  PostJournalEntryRequest: { required: ["documentDate","event","idempotencyKey","narration","source","trigger"], fields: {"amounts":{"i":{"k":"ref","r":"NamedAmount"},"k":"array"},"book":{"k":"plain"},"closedPeriodAuthorisation":{"k":"ref","r":"ClosedPeriodAuthorisation"},"currency":{"k":"plain"},"dimensions":{"i":{"k":"ref","r":"NameValue"},"k":"array"},"documentDate":{"k":"plain"},"event":{"k":"plain"},"exchangeRate":{"b":"ExchangeRate","k":"brand"},"facts":{"i":{"k":"ref","r":"NameValue"},"k":"array"},"generation":{"k":"plain"},"idempotencyKey":{"k":"plain"},"lines":{"i":{"k":"ref","r":"PostingLine"},"k":"array"},"narration":{"k":"ref","r":"LocalizedText"},"source":{"k":"ref","r":"SourceDocument"},"trigger":{"k":"plain"}} },
  PostingLine: { required: ["amount","role","side"], fields: {"amount":{"k":"money"},"dimensions":{"i":{"k":"ref","r":"NameValue"},"k":"array"},"narration":{"k":"ref","r":"LocalizedText"},"qualifier":{"k":"plain"},"role":{"k":"plain"},"scope":{"k":"ref","r":"Scope"},"side":{"k":"plain"},"subledger":{"k":"ref","r":"Subledger"}} },
  PostingReceipt: { required: ["alreadyPosted","chainSequence","entryHash","entryId","entryNumber","generation","lineCount","periodCode"], fields: {"alreadyPosted":{"k":"plain"},"chainSequence":{"b":"Int64String","k":"brand"},"entryHash":{"k":"plain"},"entryId":{"k":"plain"},"entryNumber":{"b":"Int64String","k":"brand"},"generation":{"k":"plain"},"lineCount":{"k":"plain"},"periodCode":{"k":"plain"}} },
  Problem: { required: ["code","detail","detailAr","errors","instance","status","title","titleAr","traceId","type"], fields: {"code":{"k":"plain"},"detail":{"k":"plain"},"detailAr":{"k":"plain"},"errors":{"i":{"k":"ref","r":"ApiError"},"k":"array"},"instance":{"k":"plain"},"status":{"k":"plain"},"title":{"k":"plain"},"titleAr":{"k":"plain"},"traceId":{"k":"plain"},"type":{"k":"plain"}} },
  PutCapabilityProfileRequest: { required: ["documents"], fields: {"documents":{"i":{"k":"ref","r":"DocumentProfile"},"k":"array"},"withdrawalReason":{"k":"plain","n":true}} },
  ReverseJournalEntryRequest: { required: ["reason"], fields: {"closedPeriodAuthorisation":{"k":"ref","r":"ClosedPeriodAuthorisation"},"reason":{"k":"ref","r":"LocalizedText"},"reversalDate":{"k":"plain"}} },
  Scope: { required: [], fields: {"branchId":{"k":"plain","n":true},"costCenterId":{"k":"plain"},"projectId":{"k":"plain","n":true}} },
  SourceDocument: { required: ["documentId","documentType","module"], fields: {"documentId":{"k":"plain"},"documentType":{"k":"plain"},"module":{"k":"plain"}} },
  Subledger: { required: ["kind","partyId"], fields: {"kind":{"k":"plain"},"partyId":{"k":"plain"}} },
  SuspendCostCenterRequest: { required: ["reason"], fields: {"reason":{"k":"plain"}} },
  TrialBalance: { required: ["balanced","book","periodCode","rowCount","rows","totalCredit","totalDebit"], fields: {"balanced":{"k":"plain"},"book":{"k":"plain"},"periodCode":{"k":"plain","n":true},"rowCount":{"k":"plain"},"rows":{"i":{"k":"ref","r":"TrialBalanceRow"},"k":"array"},"totalCredit":{"k":"money"},"totalDebit":{"k":"money"}} },
  TrialBalanceRow: { required: ["accountCode","credit","debit","nameAr","nameTranslations"], fields: {"accountCode":{"k":"plain"},"credit":{"k":"money"},"debit":{"k":"money"},"nameAr":{"k":"plain"},"nameTranslations":{"i":{"k":"ref","r":"NameValue"},"k":"array"}} },
};

/** أسماء المخطّطات كما وردت في العقد. / Schema names as published. */
export const SCHEMA_NAMES = ["AdmitDocumentRequest","ApiError","CapabilityProfile","CapabilitySwitch","ChainVerification","ClosedPeriodAuthorisation","CompanySetup","CostCenter","CostCenterNameRequest","DocumentAdmission","DocumentProfile","DocumentShape","ExchangeRate","HealthResponse","InitialiseCompanySetupRequest","Int64String","JournalEntry","JournalLine","LocalizedText","Money","NameValue","NamedAmount","PostJournalEntryRequest","PostingLine","PostingReceipt","Problem","PutCapabilityProfileRequest","ReverseJournalEntryRequest","Scope","SourceDocument","Subledger","SuspendCostCenterRequest","TrialBalance","TrialBalanceRow"] as const;
