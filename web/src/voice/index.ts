/* حدّ المكوّن المُعلَن. ما لا يُصدَّر من هنا ليس جزءاً من الواجهة المستقرّة. */
export { VoiceCapture, INVOICE_FIELDS } from "./VoiceCapture";
export type { VoiceCaptureProps, VoiceField } from "./VoiceCapture";
export { readInvoiceIntent, matchEvent, FIELD, SPOKEN_EVENT_RULES } from "./intent";
export type { SpokenIntent, SpokenValue, Provenance } from "./intent";
export { readArabicNumber, canReadArabicNumber, normaliseToken } from "./arabic-number";
export type { NumberReading, NumberFault } from "./arabic-number";
export { speechSupport, listen, translateError } from "./speech";
export type { SpeechUnavailable, SpeechSession, SpeechChunk } from "./speech";

/* ── الأقسام الخمسة: السجلّ، والقارئ، والبوابة، واللوحة ─────────────────── */
export { VOICE_INTENTS, VOICE_SECTIONS, SPOKEN_EVENT_CODES, intentsOf, intentById } from "./catalogue";
export type {
  VoiceIntent,
  VoiceIntentKind,
  VoiceIntentStatus,
  VoiceLedgerEffect,
  VoiceSection,
  VoiceSlot,
  VoiceSlotKind,
} from "./catalogue";
export {
  authorise,
  confirmationToken,
  disclosureFault,
  isSpokenCancellation,
  isSpokenConfirmation,
  maskPersonal,
  matchIntent,
  readCommand,
  readbackArabic,
  readbackEnglish,
  unitCodeOf,
  CONFIRM_CALL_AR,
  CONFIRM_CALL_EN,
  CONFIRM_WORDS_AR,
  CANCEL_WORDS_AR,
  TRANSCRIPT_LIMIT,
} from "./command";
export type {
  SpokenSlotValue,
  VoiceAuthorisation,
  VoiceCaller,
  VoiceDispatch,
  VoiceReading,
  VoiceReadingOptions,
  VoiceResolution,
} from "./command";
export { canSpeak, hush, speak } from "./speak";
export type { SpeakOutcome, SpeakRefusal } from "./speak";
export { VoiceConsole } from "./VoiceConsole";
export type { VoiceConsoleProps } from "./VoiceConsole";
