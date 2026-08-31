#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
# لقطة بيانات العرض — تُقرأ من قاعدة الشركة التجريبية المبذورة كما هي.
#
# لا يُخترع في هذا الملف رقم واحد: كل ما يخرج منه ناتج `select` على
# babel_demo_ledger و babel_demo_sales و babel_demo_purchasing بعد تشغيل
# `deploy/up.sh`. والمخرَج ملفّ JSON واحد تقرؤه صفحة العرض.
#
#   demo/showcase/export-snapshot.sh > web/src/demo/data/snapshot.json
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

host="${PGHOST:-127.0.0.1}"
port="${PGPORT:-5432}"
user="${PGUSER:-postgres}"
company="${BABEL_DEMO_COMPANY_ID:-d3305e1e-0000-4000-8000-000000000001}"

q() { psql -X -q -A -t -h "$host" -p "$port" -U "$user" -d "$1" -v ON_ERROR_STOP=1 -c "$2"; }

ledger_json=$(q babel_demo_ledger "
with lines as (
  select l.entry_id,
         jsonb_agg(jsonb_build_object(
           'lineNo', l.line_no,
           'accountCode', l.account_code,
           'accountName', a.name_ar,
           'roleCode', l.role_code,
           'debit', l.debit_company::text,
           'credit', l.credit_company::text,
           'costCenter', l.cost_center_id,
           'branch', l.branch_id,
           'party', l.subledger_party_id,
           'descriptionAr', l.description_ar
         ) order by l.line_no) as lines
    from ledger.journal_line l
    join ledger.account a on a.company_id = l.company_id and a.account_code = l.account_code
   where l.company_id = '$company'
   group by l.entry_id
)
select coalesce(jsonb_agg(jsonb_build_object(
         'entryNo', e.entry_no,
         'entryId', e.entry_id,
         'entryDate', to_char(e.entry_date, 'YYYY-MM-DD'),
         'periodCode', e.period_code,
         'postedAt', to_char(e.posted_at at time zone 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SSZ'),
         'status', e.status,
         'memoAr', e.memo_ar,
         'sourceModule', e.source_module,
         'sourceDocType', e.source_doc_type,
         'sourceDocId', e.source_doc_id,
         'eventCode', e.event_code,
         'triggerCode', e.posting_trigger_code,
         'currency', e.currency,
         'chainSeq', c.chain_seq,
         'entryHash', encode(c.entry_hash, 'hex'),
         'prevHash', encode(c.prev_hash, 'hex'),
         'canonVersion', c.canon_version,
         'lines', x.lines
       ) order by e.entry_no), '[]'::jsonb)
  from ledger.journal_entry e
  join lines x on x.entry_id = e.entry_id
  left join ledger.chain_link c on c.entry_id = e.entry_id
 where e.company_id = '$company'")

accounts_json=$(q babel_demo_ledger "
select coalesce(jsonb_agg(jsonb_build_object(
         'accountCode', a.account_code,
         'nameAr', a.name_ar,
         'translations', (select jsonb_object_agg(t.language_tag, t.name)
                            from ledger.name_translation t
                           where t.company_id = a.company_id and t.entity_kind = 'account'
                             and t.entity_key = a.account_code),
         'nature', a.natural_side,
         'accountType', a.account_type,
         'statementSection', a.statement_section
       ) order by a.account_code), '[]'::jsonb)
  from ledger.account a
 where a.company_id = '$company'
   and exists (select 1 from ledger.journal_line l
                where l.company_id = a.company_id and l.account_code = a.account_code)")

invoices_json=$(q babel_demo_sales "
select coalesce(jsonb_agg(jsonb_build_object(
         'docId', i.\"Id\",
         'number', i.\"Number\",
         'partyCode', c.\"Code\",
         'partyNameAr', c.\"NameAr\",
         'issuedOn', to_char(i.\"IssuedOn\", 'YYYY-MM-DD'),
         'dueOn', to_char(i.\"DueOn\", 'YYYY-MM-DD'),
         'state', i.\"State\",
         'currency', i.\"CurrencyCode\",
         'netTotal', i.\"NetTotal\"::text,
         'taxTotal', i.\"TaxTotal\"::text,
         'grossTotal', i.\"GrossTotal\"::text,
         'lines', (select coalesce(jsonb_agg(jsonb_build_object(
                            'lineNo', l.\"LineNo\",
                            'descriptionAr', l.\"DescriptionAr\",
                            'quantity', l.\"Quantity\"::text,
                            'unitPrice', l.\"UnitPrice\"::text,
                            'taxRate', l.\"TaxRate\"::text,
                            'lineNet', l.\"LineNet\"::text,
                            'lineTax', l.\"LineTax\"::text) order by l.\"LineNo\"), '[]'::jsonb)
                     from sales.sales_line l
                    where l.\"OwnerType\" = 'INVOICE' and l.\"OwnerId\" = i.\"Id\")
       ) order by i.\"Number\"), '[]'::jsonb)
  from sales.sales_invoice i
  join sales.customer c on c.\"Id\" = i.\"CustomerId\"
 where i.\"TenantId\" = '$company'")

bills_json=$(q babel_demo_purchasing "
select coalesce(jsonb_agg(jsonb_build_object(
         'docId', b.\"Id\",
         'number', b.\"Number\",
         'partyCode', s.\"Code\",
         'partyNameAr', s.\"NameAr\",
         'partyVat', s.\"VatNumber\",
         'issuedOn', to_char(b.\"IssuedOn\", 'YYYY-MM-DD'),
         'dueOn', to_char(b.\"DueOn\", 'YYYY-MM-DD'),
         'state', b.\"State\",
         'expenseCategory', b.\"ExpenseCategory\",
         'currency', b.\"CurrencyCode\",
         'netTotal', b.\"NetTotal\"::text,
         'taxTotal', b.\"TaxTotal\"::text,
         'grossTotal', b.\"GrossTotal\"::text,
         'lines', (select coalesce(jsonb_agg(jsonb_build_object(
                            'lineNo', l.\"LineNo\",
                            'descriptionAr', l.\"DescriptionAr\",
                            'quantity', l.\"Quantity\"::text,
                            'unitPrice', l.\"UnitPrice\"::text,
                            'taxRate', l.\"TaxRate\"::text,
                            'lineNet', l.\"LineNet\"::text,
                            'lineTax', l.\"LineTax\"::text) order by l.\"LineNo\"), '[]'::jsonb)
                     from purchasing.purchase_line l
                    where l.\"OwnerType\" = 'Bill' and l.\"OwnerId\" = b.\"Id\")
       ) order by b.\"Number\"), '[]'::jsonb)
  from purchasing.supplier_bill b
  join purchasing.supplier s on s.\"Id\" = b.\"SupplierId\"
 where b.\"TenantId\" = '$company'")

totals_json=$(q babel_demo_ledger "
select jsonb_build_object(
         'entryCount', (select count(*) from ledger.journal_entry where company_id = '$company'),
         'lineCount',  (select count(*) from ledger.journal_line  where company_id = '$company'),
         'totalDebit', (select coalesce(sum(debit_company),0)::text from ledger.journal_line where company_id = '$company'),
         'totalCredit',(select coalesce(sum(credit_company),0)::text from ledger.journal_line where company_id = '$company'),
         'accountCount',(select count(distinct account_code) from ledger.journal_line where company_id = '$company'),
         'chartSize', (select count(*) from ledger.account where company_id = '$company'),
         'roleCount', (select count(*) from ledger.posting_role),
         'mapRows',   (select count(*) from ledger.role_account_map))")

printf '{\n  "companyId": %s,\n  "generatedFrom": "psql · babel_demo_ledger + babel_demo_sales + babel_demo_purchasing",\n  "totals": %s,\n  "accounts": %s,\n  "entries": %s,\n  "salesInvoices": %s,\n  "supplierBills": %s\n}\n' \
  "\"$company\"" "$totals_json" "$accounts_json" "$ledger_json" "$invoices_json" "$bills_json"
