/* منقول آلياً من design/i18n/locales/en.js — لا تُحرِّره بيدك.
   Ported from design/i18n/locales/en.js — do not edit by hand.
   أعِد النقل: node scripts/port-locales.mjs   ·   مفاتيح · keys: 650
   المصدر design/ للقراءة فقط. / design/ is read-only. */
import type { LocaleMeta, MessageTree } from "../types";

export const meta: LocaleMeta = {
  "lang": "en",
  "dir": "ltr",
  "native": "English",
  "english": "English",
  "fallback": "ar",
  "source": false,
  "translation": "human",
  "pluralLocale": "en",
  "numbers": {
    "group": ",",
    "decimal": ".",
    "groupSizes": [
      3
    ],
    "digits": "latn",
    "minus": "-",
    "percentSuffix": "%",
    "currency": "SAR",
    "currencyCode": "SAR"
  },
  "dates": {
    "shortPattern": "{year}-{month}-{day}",
    "longPattern": "{weekday}, {day} {month} {year}",
    "eraGregorian": "",
    "eraHijri": "AH",
    "emptyDash": "—",
    "months": [
      "January",
      "February",
      "March",
      "April",
      "May",
      "June",
      "July",
      "August",
      "September",
      "October",
      "November",
      "December"
    ],
    "weekdays": [
      "Sunday",
      "Monday",
      "Tuesday",
      "Wednesday",
      "Thursday",
      "Friday",
      "Saturday"
    ],
    "hijriMonths": [
      "Muharram",
      "Safar",
      "Rabi' I",
      "Rabi' II",
      "Jumada I",
      "Jumada II",
      "Rajab",
      "Sha'ban",
      "Ramadan",
      "Shawwal",
      "Dhu al-Qi'dah",
      "Dhu al-Hijjah"
    ]
  },
  "font": {
    "href": "https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600;700&display=swap",
    "ui": "'IBM Plex Sans','Segoe UI',system-ui,sans-serif",
    "display": "'IBM Plex Sans','Segoe UI',system-ui,sans-serif"
  },
  "cssStrings": [
    "draftStamp",
    "draftSuffix"
  ]
};

export const messages: MessageTree = {
  "css": {
    "draftStamp": "DRAFT — NOT POSTED",
    "draftSuffix": " (draft)"
  },
  "app": {
    "name": "Salasel Babel",
    "fullName": "Salasel Babel intelligent ERP",
    "org": "Salasel Babel Trading Co.",
    "orgEn": "Salasel Babel Trading Co.",
    "skipToContent": "Skip to content",
    "designSystem": "Design system — Salasel Babel",
    "galleryVersion": "Component gallery · version 1.0",
    "userInitials": "MA",
    "userName": "Muzaffar Al-Omari",
    "noscript": "This page loads its text from a locale file via JavaScript. Enable it to view the page.",
    "theme": {
      "toggle": "Toggle theme",
      "light": "Light theme",
      "dark": "Dark theme",
      "palette": "Colour palette",
      "paletteDefault": "Default (AA)",
      "paletteAccessible": "High contrast"
    },
    "locale": {
      "label": "Language",
      "aria": "Choose interface language",
      "digitShape": "Digit shape (display only)"
    },
    "a11y": {
      "openNav": "Open navigation",
      "help": "Help",
      "notifications": "Notifications",
      "close": "Close",
      "rowOptions": "Line options",
      "crumbs": "Breadcrumb",
      "mainNav": "Main navigation",
      "gallerySections": "Gallery sections"
    },
    "nav": {
      "home": "Home",
      "dashboard": "Dashboard",
      "sales": "Sales",
      "inventory": "Inventory",
      "hr": "Human resources",
      "gl": "General ledger",
      "settings": "Settings",
      "designGallery": "Design system gallery",
      "chart": "Chart of accounts",
      "journals": "Journal entries",
      "ledger": "General ledger",
      "trialBalance": "Trial balance",
      "reports": "Financial reports",
      "budget": "Budget"
    }
  },
  "common": {
    "action": {
      "save": "Save",
      "saveDraft": "Save as draft",
      "cancel": "Cancel",
      "close": "Close",
      "back": "Back",
      "print": "Print",
      "export": "Export",
      "refresh": "Refresh report",
      "retry": "Try again",
      "deleteFinal": "Delete permanently",
      "confirmDelete": "Yes, delete",
      "duplicate": "Duplicate line",
      "deleteLine": "Delete line",
      "duplicateEntry": "Duplicate entry",
      "printVoucher": "Print voucher",
      "deleteDraft": "Delete draft",
      "addLine": "Add line",
      "search": "Search",
      "clearSearch": "Clear search",
      "post": "Post entry",
      "reverse": "Reverse entry",
      "recalculate": "Recalculate",
      "previewEntry": "Preview entry (modal)",
      "confirmDialog": "Confirm delete (small modal)",
      "accountDrawer": "Account details (drawer)",
      "actionsMenu": "Actions menu",
      "openLedger": "Open ledger",
      "copyError": "Copy error text",
      "reviewUnbalanced": "Review unbalanced lines",
      "addAdjusting": "Add adjusting line",
      "ignore": "Dismiss",
      "newEntry": "New entry",
      "import": "Import",
      "printTrialBalance": "Print trial balance",
      "runAudit": "Run the audit on this page"
    },
    "label": {
      "all": "All",
      "none": "None",
      "select": "— select —",
      "selectAccount": "— select account —",
      "dash": "—",
      "perPage": "Per page",
      "status": "Status",
      "date": "Date",
      "type": "Type",
      "classification": "Classification",
      "parentAccount": "Parent account",
      "currentBalance": "Current balance",
      "lastMovement": "Last movement",
      "format": "Format",
      "level": "Detail level",
      "from": "From",
      "to": "To",
      "required": "Required field",
      "badNumber": "Invalid value",
      "codeName": "{code} — {name}"
    },
    "pager": {
      "prev": "Previous page",
      "next": "Next page",
      "showingEntries": {
        "=0": "No entries to show",
        "one": "Showing {range} of 1 entry",
        "other": "Showing {range} of {count} entries"
      },
      "showingAccounts": {
        "=0": "No accounts to show",
        "one": "Showing {range} of 1 account",
        "other": "Showing {range} of {count} accounts"
      }
    },
    "count": {
      "selected": {
        "=0": "Nothing selected",
        "one": "1 item selected",
        "other": "{count} items selected"
      },
      "validationErrors": {
        "=0": "No errors — the form is ready to save",
        "one": "Could not save — 1 field needs correcting",
        "other": "Could not save — {count} fields need correcting"
      },
      "lines": {
        "=0": "No lines",
        "one": "1 line",
        "other": "{count} lines"
      },
      "accounts": {
        "=0": "No accounts",
        "one": "1 account",
        "other": "{count} accounts"
      },
      "entries": {
        "=0": "No entries",
        "one": "1 entry",
        "other": "{count} entries"
      },
      "similarEntries": {
        "=0": "This description does not resemble any previous entry.",
        "one": "This description resembles 1 previous entry posted to “IT services expense”.",
        "other": "This description resembles {count} previous entries posted to “IT services expense”."
      }
    },
    "state": {
      "emptyTitle": "No entries in this period",
      "emptyBody": "Nothing has been posted to period 05/2026 yet. Start a new entry or import from a file.",
      "errorTitle": "Could not load the trial balance",
      "errorBody": "The server did not respond within the timeout. No data was changed.",
      "noAuditLog": "No audit log yet",
      "noAuditLogBody": "The audit log appears after the first posting.",
      "noAccountMatch": "No account matches the search",
      "noAccountMatchBody": "Try the full account number, or clear the search filter.",
      "pickerEmpty": "No account matches the search."
    },
    "toast": {
      "lineDuplicated": "Line duplicated",
      "draftSaved": "Draft saved",
      "cannotDeleteLast": "The last line cannot be deleted",
      "serverRejected": "The server rejected the request",
      "sortedBy": "Sorted by: {column}",
      "zeroShown": "Zero-balance accounts shown",
      "zeroHidden": "Zero-balance accounts hidden",
      "prototypeRecalc": "Prototype — recalculation is not wired up",
      "prototypeExport": "Prototype — export is not wired up",
      "localeChanged": "Interface language is now: {locale}"
    },
    "guard": {
      "invisible": "This text contains invisible control characters ({list}) — the server will reject it because they change the entry hash. Delete them and retype by hand.",
      "unknownChar": "control character"
    },
    "charName": {
      "2066": "LTR isolate",
      "2067": "RTL isolate",
      "2068": "first-strong isolate",
      "2069": "pop isolate",
      "200B": "zero-width space",
      "200C": "zero-width non-joiner",
      "200D": "zero-width joiner",
      "200E": "left-to-right mark",
      "200F": "right-to-left mark",
      "061C": "Arabic letter mark",
      "202A": "LTR embedding",
      "202B": "RTL embedding",
      "202C": "pop directional formatting",
      "202D": "LTR override",
      "202E": "RTL override",
      "FEFF": "byte order mark"
    }
  },
  "acct": {
    "debit": "Debit",
    "credit": "Credit",
    "debitCur": "Debit ({currency})",
    "creditCur": "Credit ({currency})",
    "debitTotal": "Total debit ({currency})",
    "creditTotal": "Total credit ({currency})",
    "taxTotal": "Total VAT ({currency})",
    "difference": "Difference ({currency})",
    "total": "Total",
    "grandTotal": "Grand total",
    "balanced": "Balanced",
    "unbalanced": "Out of balance",
    "entryState": "Entry state",
    "entryBalanced": "Entry is balanced",
    "entryUnbalanced": "Entry is out of balance",
    "tbState": "Trial balance state",
    "tbBalanced": "Trial balance is balanced",
    "natureDebit": "Debit nature",
    "natureCredit": "Credit nature",
    "openingBalance": "Opening balance",
    "periodMovement": "Period movement",
    "closingBalance": "Closing balance",
    "debitBalances": "Total debit balances ({currency})",
    "creditBalances": "Total credit balances ({currency})",
    "accountCount": "Accounts",
    "amountInWords": "Amount in words:",
    "entryVoucher": "Journal voucher",
    "journalVoucher": "Journal Voucher",
    "status": {
      "draft": "Draft",
      "posted": "Posted",
      "reversed": "Reversed",
      "pending": "Awaiting approval",
      "rejected": "Rejected",
      "archived": "Archived",
      "active": "Active",
      "postable": "Postable",
      "openPeriod": "Open period",
      "postedOnly": "Posted entries only"
    },
    "tax": {
      "type": "VAT type",
      "rate": "VAT rate",
      "amount": "VAT amount ({currency})",
      "input": "Input VAT",
      "output": "Output VAT",
      "zero": "Zero-rated",
      "exempt": "Exempt",
      "exclusive": "Amount excludes VAT",
      "inclusive": "Amount includes VAT",
      "inputAt": "Input VAT 15%",
      "none": "No VAT on this entry"
    },
    "calendar": {
      "gregorian": "Gregorian",
      "hijri": "Hijri (Umm al-Qura)"
    },
    "class": {
      "assets": "Assets",
      "liabilities": "Liabilities",
      "equity": "Equity",
      "revenue": "Revenue",
      "expenses": "Expenses"
    },
    "totalOf": "Total {class}",
    "columns": {
      "entryNo": "Entry no.",
      "date": "Date",
      "memo": "Description",
      "account": "Account",
      "costCentre": "Cost centre",
      "action": "Action",
      "seq": "#"
    }
  },
  "field": {
    "reference": {
      "label": "Reference",
      "value": "Invoice 0587-2026"
    },
    "memo": {
      "label": "Description",
      "ph": "Type the line description…",
      "hint": "Appears in the ledger and on the account statement.",
      "linePh": "Line description"
    },
    "entryNo": {
      "label": "Entry no.",
      "hint": "(system number — gapless counter)"
    },
    "branch": {
      "label": "Branch",
      "main": "Head office — Riyadh",
      "jeddah": "Jeddah branch",
      "dammam": "Dammam branch",
      "all": "All branches"
    },
    "period": {
      "label": "Fiscal period",
      "open": "05/2026 — open",
      "value": "05/2026"
    },
    "costCentre": {
      "label": "Cost centre",
      "error": "A cost centre is required for expense accounts.",
      "it": "IT department",
      "finance": "Finance department",
      "projects": "Projects department"
    },
    "description": {
      "label": "Entry description",
      "value": "IT services expense for period 05/2026 including 15% VAT",
      "hint": "Try pasting text from a browser or an office document — the field warns you if it carries invisible control characters."
    },
    "currency": {
      "label": "Currency",
      "ok": "Books currency",
      "sar": "Saudi riyal (SAR)"
    },
    "entryDate": {
      "label": "Entry date"
    },
    "dueDate": {
      "label": "Due date"
    },
    "calendarPref": {
      "label": "Calendar preference",
      "hint": "A per-user display preference. It never touches storage."
    },
    "account": {
      "label": "Account",
      "ph": "Search by account number or name…",
      "hint": "Only detail accounts can be posted to; summary accounts are not listed here."
    },
    "options": {
      "label": "Other options",
      "autoVat": "Create the VAT line automatically",
      "showAccountNos": "Show account numbers next to names",
      "rememberCostCentre": "Remember the cost centre for this entry type",
      "autoPost": "Auto-post (requires the “accounts manager” permission)"
    },
    "searchEntries": {
      "label": "Search",
      "ph": "Search by entry number or description…"
    },
    "searchAccounts": {
      "label": "Search accounts",
      "ph": "Search by account number or name…"
    },
    "notes": {
      "label": "Internal notes",
      "ph": "Notes that do not appear in external reports…"
    },
    "exchangeRate": {
      "label": "Exchange rate"
    },
    "createdBy": {
      "label": "Created by"
    },
    "defaultProject": {
      "label": "Default project",
      "value": "Babel Towers project"
    },
    "debitAmount": {
      "label": "Debit amount",
      "error": "The amount must be greater than zero."
    },
    "accountNo": {
      "label": "Account number",
      "ok": "Detail account — postable."
    },
    "vendorName": {
      "label": "Vendor name",
      "value": "Tech Company",
      "hint": "The invisible-character guard is active on this field — paste text from a browser to try it."
    },
    "detailLevel": {
      "label": "Detail level",
      "detail": "Detail accounts",
      "groups": "Groups",
      "classes": "Top-level classes"
    },
    "negativeDebit": {
      "label": "Debit — negative value"
    },
    "rejectedCredit": {
      "label": "Credit — rejected"
    }
  },
  "screen": {
    "journal": {
      "accounts": {
        "a6120101": "IT services expense",
        "a6110101": "Salaries and wages",
        "a2010101": "Vendor / Tech Company",
        "a1020101": "Al Ahli Bank — current account",
        "a4101001": "Sales revenue",
        "a121201": "VAT — input"
      },
      "memoTech": "IT services expense",
      "memoVat": "Value added tax",
      "memoVendor": "Tech Company",
      "vendorInvoice": "Tech Company — invoice 0587",
      "entryType": "Expense entry",
      "chips": {
        "tech": "IT services expense",
        "vat": "VAT — input",
        "vendor": "Vendor / Tech Company"
      },
      "preview": {
        "title": "Preview the entry before posting"
      },
      "confirm": {
        "title": "Delete this draft permanently?",
        "body": "Draft {code} and its four lines will be deleted. This cannot be undone. <strong>Posted entries are never deleted</strong> — they are reversed by a matching entry."
      },
      "drawer": {
        "title": "6120101 — IT services expense",
        "recent": "Last 3 movements",
        "classification": "Operating expenses",
        "parent": "61201 — IT expenses"
      },
      "alert": {
        "periodOpenTitle": "Fiscal period 05/2026 is open",
        "periodOpenBody": "Posting is allowed until {date}. After that the period closes and corrections require a reversing entry.",
        "postedTitle": "Entry {code} posted",
        "postedBody": "Appended to the hash chain at sequence {seq}.",
        "unbalancedTitle": "Entry is out of balance",
        "unbalancedBody": "Difference {amount} {currency}. It cannot be posted until debit equals credit.",
        "aiTitle": "Assistant suggestion",
        "aiNote": "These are suggestions only; review them before posting.",
        "vatNote": "The system will create the VAT line automatically from the VAT type and rate settings.",
        "dbTitle": "The database rejected the posting — nothing was saved",
        "dbBody": "Check constraint {constraint} failed. The transaction rolled back completely and no counter number was consumed. Correct the lines and try again.",
        "dbAttempt": "Attempted at:",
        "dbRef": "Reference id:",
        "dbUser": "User:",
        "errList1": "Cost centre — required for expense accounts.",
        "errList2": "Entry description — contains invisible control characters.",
        "errList3": "Line 4 — an account is selected with no debit or credit amount.",
        "errList1Tail": " — required for expense accounts.",
        "errList2Tail": " — contains invisible control characters."
      },
      "tabs": {
        "details": "Entry details",
        "extra": "Additional information",
        "notes": "Notes",
        "audit": "Audit log",
        "detailsBody": "First panel. Move between tabs with the arrow keys — their direction follows the active language."
      },
      "rows": {
        "r1": {
          "memo": "IT services expense — period 05/2026"
        },
        "r2": {
          "memo": "Collection from customer Al Nour Company"
        },
        "r3": {
          "memo": "Inventory adjustment — main warehouse"
        },
        "r4": {
          "memo": "Payroll for 04/2026"
        },
        "r5": {
          "memo": "Reversal of entry JV-2026-00118"
        },
        "r6": {
          "memo": "Material purchases — Babel Towers project"
        },
        "r7": {
          "memo": "Fiscal year 2025 closing entry"
        }
      }
    },
    "trialBalance": {
      "title": "Trial balance",
      "docTitle": "Trial balance — Salasel Babel",
      "description": "Trial balance — Salasel Babel ERP",
      "caption": "Trial balance for the period {from} to {to}",
      "sub": "Period {from} to {to} · Branch: {branch} · Currency: {currency} · Last updated {time}",
      "filters": "Report filters",
      "showZero": "Show zero-balance accounts",
      "showAlt": "Show names in a second language",
      "altLocale": "Second-column language",
      "showSubtotals": "Show subtotals per class",
      "viewAll": "All",
      "viewDebit": "Debit only",
      "viewCredit": "Credit only",
      "note": "This trial balance covers posted entries only",
      "noteBody": "Drafts and entries awaiting approval are excluded. Balance here is a structural property, not a computed result: an unbalanced entry cannot be posted at all.",
      "accountsSummary": "across {classes} · {idle}",
      "idleAccounts": {
        "=0": "no dormant accounts",
        "one": "1 dormant account",
        "other": "{count} dormant accounts"
      },
      "classCount": {
        "=0": "no classes",
        "one": "1 class",
        "other": "{count} classes"
      },
      "grandTotalOf": {
        "=0": "Grand total — no accounts",
        "one": "Grand total — 1 account",
        "other": "Grand total — {count} accounts"
      },
      "footnote": "Amounts are in Saudi riyals shown to two decimal places. Contra balances (such as accumulated depreciation) appear in the column matching their nature. Accounts with no movement and no balance are hidden unless requested.",
      "lastPosted": "Last posting: {code} · {stamp}",
      "export": {
        "title": "Export the trial balance",
        "xlsx": "Excel workbook (xlsx)",
        "pdf": "PDF file",
        "csv": "CSV file, UTF-8 with byte order mark",
        "incSubtotals": "Include subtotals",
        "incZero": "Include zero-balance accounts",
        "incAlt": "Include second-language names",
        "note": "CSV files are exported as UTF-8 with a byte order mark; without it Arabic Excel opens them with mangled characters. Numbers are exported with a dot decimal separator and no thousands separators so that Excel does not reinterpret them by machine locale."
      },
      "accounts": {
        "121201": "VAT — input",
        "221301": "VAT — output",
        "1010101": "Main cash",
        "1010102": "Jeddah branch cash",
        "1020101": "Al Ahli Bank — current account",
        "1301001": "Customer / Al Nour Company",
        "1301002": "Customer / Al Rowad Est.",
        "1401001": "Goods inventory — main warehouse",
        "1501001": "Fixed assets — cost",
        "1502001": "Accumulated depreciation",
        "2010101": "Vendor / Tech Company",
        "2010102": "Vendor / Imdad Est.",
        "2201001": "Accrued payroll",
        "2204001": "End-of-service provision",
        "3010101": "Share capital",
        "3020101": "Retained earnings",
        "4101001": "Sales revenue",
        "4201001": "Rental income",
        "6110101": "Salaries and wages",
        "6120101": "IT services expense",
        "6120102": "Telecom and internet expense",
        "6130101": "Rent expense",
        "6140101": "Depreciation expense",
        "6150101": "Maintenance expense"
      }
    },
    "voucher": {
      "docTitle": "Journal voucher — print",
      "description": "Printable journal voucher — document template in Salasel Babel",
      "backToGallery": "Back to the gallery",
      "printIt": "Print the voucher",
      "draftWithStamp": "Draft (stamp visible)",
      "printHint": "Print to PDF to preview the print sheet: repeating table header, amount in words, signature slots and the entry hash.",
      "address": "Riyadh — King Fahd Road, building 1240",
      "cr": "Commercial registration:",
      "vatNo": "VAT number:",
      "phone": "Phone:",
      "email": "Email:",
      "qrAria": "QR code placeholder",
      "qrCaption": "QR code",
      "qrCaption2": "generated on approval",
      "hijriDate": "Hijri date",
      "preparedBy": "Prepared by — {name}",
      "reviewedBy": "Reviewed by — responsible accountant",
      "approvedBy": "Approved by — finance manager",
      "printedAt": "Printed at: {stamp} · page 1 of 1",
      "wordsSuffix": " only",
      "noWords": "Amount-in-words is not available in this language — the figure is shown numerically only."
    }
  },
  "gallery": {
    "docTitle": "Design system gallery — Salasel Babel",
    "description": "Design system gallery for Salasel Babel ERP — every component in every state, in both themes and every language",
    "heroTitle": "Salasel Babel design system",
    "heroBody": "Everything on this page is extracted from the <strong>approved journal-entry screen</strong>; nothing was redesigned. The point is that every screen built next — by any person or any agent — looks as if it came from the same hand. Switch theme and language from the top bar to see the whole system in every combination.",
    "badge": {
      "approved": "Approved visual reference",
      "rtl": "RTL-native, not mirrored",
      "noframework": "No framework · no build step",
      "i18n": "Four languages · keys, not strings"
    },
    "toc": {
      "colors": "Colours",
      "type": "Type",
      "space": "Spacing",
      "buttons": "Buttons",
      "fields": "Fields",
      "amounts": "Amounts",
      "line": "Entry line",
      "tables": "Tables",
      "states": "States",
      "alerts": "Alerts",
      "overlays": "Overlays",
      "empty": "Empty and loading",
      "print": "Print",
      "i18n": "i18n",
      "audit": "Audit",
      "trialBalance": "Trial balance screen ↗",
      "rules": "Rules guide ↗"
    },
    "sec": {
      "color": "Colours",
      "type": "Type and the typographic ladder",
      "space": "Spacing, radii, shadows and motion",
      "buttons": "Buttons",
      "fields": "Fields and forms",
      "amounts": "Amounts — cell, input and debit/credit pair",
      "line": "Journal line editor",
      "table": "Data table — sticky header, sortable columns, aligned totals row",
      "pills": "Status pills",
      "alerts": "Alerts and live validation",
      "overlays": "Modals, drawers and menus",
      "nav": "Tabs and breadcrumbs",
      "states": "Empty, loading and error states",
      "stats": "Statistics and balance state",
      "toasts": "Transient notifications",
      "print": "Printing",
      "i18n": "Internationalisation — four languages, two directions, six plural categories",
      "audit": "Audit — with no build step"
    },
    "lede": {
      "color": "Every value below is read live from the theme file when the page opens, so the gallery cannot drift from the tokens. The “light” and “dark” lines show each token in both themes at once.",
      "colorSem": "Never write a raw colour in a component, and never use a core token for the “posted” state: use the semantic token. If the meaning of the state changes later, it changes in one place instead of a hundred.",
      "type": "The typeface follows the active language, and each language carries its own fallback stack in its own file. Every number in the interface is tabular-nums without exception.",
      "buttons": "Order in the action bar: primary action first (at the start of the line), then secondary actions. A destructive action never sits next to the primary one.",
      "fields": "Every label is a key, not a string. The companion second label shows **another language the user chooses** — not English pinned in place.",
      "amounts": "This is the most important component in the product. An amount is decimal, not float; it arrives from the server as a string at a fixed scale and the interface only adds thousands separators — using separators declared in the locale file, never from Intl.",
      "line": "Carried over from the approved screen: debit and credit headers are coloured, the cell edits in place, and the row menu holds duplicate and delete.",
      "table": "The totals row lives in a tfoot of the same table, so it inherits the column widths and cannot drift from them however the content changes or however long a translation runs.",
      "pills": "A state is read from its colour and its text together — never colour alone, because 8% of men cannot distinguish red from green and monochrome printing drops colour entirely.",
      "print": "Arabic printing breaks routinely: the table header does not repeat, coloured backgrounds are dropped so the debit/credit distinction is lost, numbers scatter inside the paragraph, and the web font does not load in preview.",
      "i18n": "Direction is a property of the language, not of the product. Plurals go through Intl.PluralRules, never a test against one. And locale formatting is display-only, enforced by type rather than by comment.",
      "audit": "Open design/audit.html for the full audit page, or run the audit right here against this page."
    },
    "demo": {
      "coreLayer": "Core layer",
      "semLayer": "Semantic layer — this is what components use",
      "sizeLadder": "Size ladder",
      "weights": "Weights",
      "weightsNote": "600 is the weight of interactive elements (button, tab, label); 700 is for headings and total values only.",
      "wRegular": "Regular 400",
      "wMedium": "Medium 500",
      "wSemibold": "Semibold 600",
      "wBold": "Bold 700",
      "sampleLine": "A balanced journal entry",
      "isolation": "Numbers: bidirectional isolation",
      "isolationBad": "Without isolation (wrong)",
      "isolationGood": "With isolation (right)",
      "isolationRow1": "Balance -1,250.00 riyals",
      "isolationRow2": "Invoice INV-2026-0587 is on hold",
      "isolationRow1Good": "Balance <span class=\"amt amt--neg\" dir=\"ltr\">-1,250.00</span> SAR",
      "isolationRow2Good": "Invoice <span class=\"ltr\">INV-2026-0587</span> is pending",
      "isolationNote": "The minus sign is a neutral character in the bidi algorithm, so inside an RTL paragraph it jumps to the wrong end and the number reads as positive. See rules guide §3.",
      "spaceLadder": "Spacing ladder — the name is the value in pixels",
      "radii": "Radii",
      "shadows": "Shadows",
      "motion": "Motion",
      "motionNote": "Every transition is cancelled under prefers-reduced-motion: reduce — applied once for everything.",
      "btnTypes": "Variants",
      "btnStates": "States — hover, and press Tab to see the focus ring",
      "btnDefault": "Default",
      "btnHover": "Hover",
      "btnFocus": "Focus — press Tab",
      "btnDisabled": "Disabled (entry out of balance)",
      "btnLoading": "Posting…",
      "btnDisabledShort": "Disabled",
      "btnLoadingShort": "Loading",
      "btnDeleteDisabled": "Delete disabled",
      "btnBlock": "Full width",
      "btnSmall": "Small",
      "btnLoadingNote": "The loading state keeps the button width fixed so the action bar does not jump under the user's finger.",
      "iconAndSeg": "Icon buttons and segmented controls",
      "fieldBasics": "Basic states",
      "dateField": "Date — always stored Gregorian, displayed in the language's calendar",
      "dateNote": "Storage is Gregorian, always, without exception. Hijri is a display conversion computed at render time and never written back to the database.",
      "picker": "Account picker and searchable list",
      "amtStates": "Amount cell states",
      "amtCase": "Case",
      "amtShown": "Rendered",
      "amtToken": "Semantic token",
      "amtNote": "Note",
      "amtDebit": "Debit amount",
      "amtCredit": "Credit amount",
      "amtNeutral": "Neutral amount",
      "amtNegative": "Negative amount",
      "amtZero": "Zero",
      "amtMuted": "Muted (reference)",
      "amtTotal": "Total",
      "amtWithCur": "With currency",
      "amtLarge": "Large",
      "amtAcctCode": "Account number",
      "amtNoteW600": "weight 600",
      "amtNoteOutside": "outside the two sides of the entry",
      "amtNoteSign": "sign left of the number — isolation is mandatory",
      "amtNoteDash": "shows a dash, not a zero",
      "amtNotePrev": "prior balance",
      "amtNoteTotalRow": "totals row",
      "amtNoteCur": "the currency symbol never separates from the number",
      "amtNoteStat": "stat card",
      "amtNoteIso": "isolated and tabular",
      "amtNoteW700": "weight 700",
      "scaleNote": "The scale is fixed: always two decimals on display, never trimmed. Mixing two representations of the same amount is exactly trap-18.",
      "dcPair": "Amount input and debit/credit pair",
      "dcNote": "The field stays inside the active language's form, but its content is a Latin number: direction:ltr with text-align toward the end of the line places the number as in the approved screen and keeps the minus sign in place.",
      "lineNote": "The fourth line shows the rejected-cell state: an account chosen with no amount. The table scrolls horizontally inside its own container — the page body never does.",
      "stickyNote": "A sticky header needs a bounded container height. Click a column header to try sorting — the ordering uses the active language's collator, not one pinned to Arabic.",
      "docStates": "Accounting document states",
      "pillNote": "No new colour: every pill points at an existing semantic token. “Archived” carries a dashed border so it reads without colour.",
      "pageAlerts": "Page-level alerts",
      "dbReject": "Database-level rejection — full content width",
      "dbNote": "Three rules for a rejection message: (1) it says what happened to the data. (2) it carries the server text verbatim, isolated dir=\"ltr\". (3) it carries a reference id. And it is never hidden or collapsed.",
      "errSummary": "Error summary above the form, and in-field validation",
      "notes": "Notes inside cards",
      "openLayers": "Open the layered components",
      "layersNote": "Every modal traps focus, closes on Esc or on the scrim, and returns focus to the button that opened it. The drawer slides in from the start of the line — right in RTL, left in LTR — with no language-conditional code.",
      "crumbs": "Breadcrumb — the separator stays between items in both directions",
      "tableSkeleton": "Table skeleton — same column count and alignment, so the page does not jump",
      "cardSkeleton": "Card skeleton.",
      "statNote": "Being out of balance is a warning, not a red error — that is the approved screen's decision: an entry being typed is out of balance by nature, and red is reserved for actual rejection.",
      "toastFn": "stacks, never overlaps",
      "toastNeutral": "Neutral",
      "toastSuccess": "Success",
      "toastWarning": "Warning",
      "toastDanger": "Danger",
      "toastNote": "A toast is for reversible actions and non-critical information only. A posting result or a database rejection is never reported by a notice that disappears in two seconds.",
      "printLinks": "Open the printable documents",
      "printNote": "Open the voucher and print it to see the print sheet work: letterhead, repeating table header, amount in words, signature slots, entry hash in the footer, and the slanted “draft” stamp. The stamp and suffix strings come from the locale file through CSS custom properties, because content: cannot read an attribute.",
      "openVoucher": "Open the printable journal voucher",
      "openTrialBalance": "Trial balance screen",
      "openRules": "Arabic rules guide"
    },
    "i18n": {
      "dirTitle": "Direction is a property of the language",
      "dirBody": "There is not one test anywhere in this system for “is the language Arabic?”. Direction, typeface, separators, month names and plural categories are all fields in the locale file. Switching language sets lang and dir on the root element; everything else is logical properties.",
      "pluralTitle": "Plurals — six categories in Arabic, two in English, Urdu and Hindi",
      "pluralBody": "Change the count and watch the selected category in each language. count === 1 ? a : b is wrong in Arabic at 2, at 3–10 and at 11–99, and wrong in Hindi at zero, because Hindi puts zero in the “one” category.",
      "pluralCount": "Count",
      "pluralCategory": "Selected category",
      "fmtTitle": "Formatting is display-only — enforced by type",
      "fmtBody": "SB.fmt.amount() does not return a string; it returns a Display object. Any implicit coercion to string throws a TypeError, so a formatted value cannot reach a field or a hash by accident. The only way out to the screen is d.into(el); the only submittable value is d.machine, which is always ASCII.",
      "fmtTry": "Try misusing it",
      "fmtDisplay": "Display (into)",
      "fmtMachine": "Machine (machine)",
      "fmtCoerce": "Implicit coercion",
      "hazardTitle": "Why we do not use Intl for numbers and dates — measured live in your browser",
      "hazardBody": "The table below is computed right now in your browser. Note the invisible control characters Intl injects.",
      "hazardLocale": "Locale",
      "hazardNumber": "Number",
      "hazardDate": "Date",
      "hazardMarks": "Invisible characters",
      "hazardNone": "none",
      "digitTitle": "Digit shape — a display preference, not a value",
      "digitBody": "Digit shape is a field in the locale file. Change it and watch: the rendered text changes and the machine value stays ASCII exactly as it was — and only the machine value is ever submitted or hashed."
    },
    "audit": {
      "run": "Run the audit",
      "clean": "No findings — clean",
      "missingKeys": "Missing keys",
      "orphanKeys": "Orphaned keys",
      "pluralGaps": "Missing plural categories",
      "paramGaps": "Mismatched placeholders",
      "hardcoded": "Strings hard-coded in markup",
      "convention": "Key-convention violations",
      "keyCount": "Key count",
      "categories": "Plural categories",
      "cliNote": "File-level checks (raw colours outside the theme file, strings in HTML) need to read files, which is blocked under file://. Run: node design/audit.js"
    },
    "role": {
      "pageBg": "Page background",
      "surface": "Card surface",
      "surfaceRaised": "Raised surface",
      "surfaceSunken": "Sunken surface · table head",
      "border": "Default border",
      "borderStrong": "Field border",
      "text": "Primary text",
      "textMuted": "Secondary text",
      "textSubtle": "Subtle text",
      "brand": "Brand",
      "brandHover": "Brand on hover",
      "brandSoft": "Brand background",
      "debitSoft": "Debit background",
      "creditSoft": "Credit background",
      "success": "Success",
      "successSoft": "Success background",
      "successLine": "Success border",
      "warning": "Warning",
      "warningSoft": "Warning background",
      "warningLine": "Warning border",
      "ai": "AI assistant",
      "aiSoft": "Assistant background",
      "aiLine": "Assistant border",
      "danger": "Danger · required field",
      "primaryAction": "Primary action",
      "focusRing": "Focus ring",
      "info": "Information",
      "amount": "Normal amount",
      "amountZero": "Zero amount",
      "amountNegative": "Negative amount",
      "required": "Required field",
      "hint": "Hint",
      "colHead": "Column heading",
      "fieldLabel": "Field label",
      "tableCell": "Table cell",
      "buttonTab": "Button and tab",
      "uiText": "Interface text",
      "cardTitle": "Card title",
      "longText": "Long text",
      "dialogTitle": "Dialog title",
      "sectionTitle": "Section title",
      "stateValue": "State value",
      "pageTitle": "Page title",
      "statValue": "Stat value",
      "field": "Field",
      "button": "Button",
      "iconButton": "Icon button",
      "card": "Card",
      "dialog": "Dialog",
      "menu": "Menu",
      "lift": "Slight lift",
      "pill": "Pill"
    },
    "nav": {
      "tokens": "Tokens",
      "components": "Components"
    },
    "swatch": {
      "light": "Light",
      "dark": "Dark"
    },
    "footer": "Salasel Babel design system — extracted from the approved journal-entry screen. Tokens, theme, components and locale files live under design/. The figures and accounts on this page are illustrative."
  },
  "audit": {
    "docTitle": "Design system audit — Salasel Babel",
    "title": "Internationalisation and theme audit",
    "lede": "An audit page with no build step. It opens straight from disk. It checks key coverage, plural categories, placeholder parity and the key-naming convention, and measures the Intl hazards in your browser.",
    "sec": {
      "keys": "Key coverage",
      "plurals": "Plural categories",
      "params": "Placeholders",
      "convention": "Key naming convention",
      "intl": "Intl hazards — measured live",
      "files": "File-level audit (command line)",
      "dom": "Strings hard-coded in markup"
    },
    "filesBody": "The browser cannot read sibling files under file:// (CORS policy). Run the following from the repository root to audit raw colours outside the theme file and strings hard-coded in HTML:",
    "ok": "pass",
    "fail": "finding",
    "total": "Total"
  }
};
