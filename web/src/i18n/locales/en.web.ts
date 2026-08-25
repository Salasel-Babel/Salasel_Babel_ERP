/* Web-app keys — English. Translation quality: human.
   Every key here exists in all four locales; scripts/audit.mjs enforces it. */
import type { MessageTree } from "../types";

export const messages: MessageTree = {
  app: {
    web: {
      docTitle: "Salasel Babel — Trial Balance",
      tagline: "Arabic-native accounting · a front end isolated from the back end by a published contract",
      skipToTable: "Skip to the table",
    },
    health: {
      label: "Service health",
      ok: "Connected",
      down: "No response",
      checking: "Checking",
      culture: "Server culture",
      calendar: "Server calendar",
      apiVersion: "Surface version",
      hijriWarning: "The server runs on the Umm al-Qura calendar — any implicit date formatting there writes Hijri.",
    },
    nav: { contract: "Published contract" },
  },
  common: {
    state: {
      loading: "Loading",
      loadingBody: "Reading the trial balance from the server. Nothing in the data changes.",
    },
    action: {
      clearFilters: "Clear filters",
      keyboardHelp: "Keyboard shortcuts",
    },
    problem: {
      title: "The request did not complete",
      code: "Code",
      trace: "Trace id",
      field: "Field",
      status: "Status",
      noContract: "The server did not answer in the published problem format — no code and no Arabic message.",
      network: "The server could not be reached. Nothing in the data changed.",
      count: {
        "=0": "No errors",
        one: "One error",
        other: "{count} errors",
      },
    },
    keys: {
      title: "Keyboard shortcuts",
      hint: "Press ? for shortcuts",
      search: "Jump to search",
      rowNext: "Next row",
      rowPrev: "Previous row",
      rowFirst: "First row",
      rowLast: "Last row",
      pageNext: "Ten rows forward",
      pagePrev: "Ten rows back",
      viewCycle: "Cycle view: all · debit · credit",
      reload: "Re-read the trial balance",
      help: "Show this list",
      dismiss: "Close, or clear the search",
    },
  },
  field: {
    book: { label: "Book", hint: "The book within the company, e.g. MAIN" },
    company: { label: "Company", hint: "The company id — scope is matched against the credential" },
    periodCode: {
      label: "Period code",
      hint: "Gregorian yyyy-MM, or leave empty for all periods",
      bad: "The period code does not match the published yyyy-MM format",
      all: "All periods",
    },
    token: { label: "Credential", hint: "Identity comes from the credential alone — no tenant header" },
  },
  screen: {
    trialBalance: {
      sourceNote: "Rows come from the immutable journal lines, not from a balance table.",
      totalsNote: "Both totals are computed by sum() over numeric in the same query — never in the browser.",
      sortedBy: "Sorted by {column}",
      matching: {
        "=0": "No matching account",
        one: "One matching account",
        other: "{count} matching accounts",
      },
    },
    contract: {
      title: "Published contract",
      sub: "Everything the front end needs. It reads no back-end code.",
      version: "Contract version",
      digest: "Contract digest",
      note: "The types and the client are generated from this file. A change to it breaks the build loudly instead of drifting silently.",
      moneyNote: "Money is text on the wire, and its type here is an object that throws on any conversion to a number.",
      operations: {
        "=0": "No operations",
        one: "One operation",
        other: "{count} operations",
      },
      schemas: {
        "=0": "No schemas",
        one: "One schema",
        other: "{count} schemas",
      },
    },
  },
};
