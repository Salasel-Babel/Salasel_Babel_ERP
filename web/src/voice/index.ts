/* حدّ المكوّن المُعلَن. ما لا يُصدَّر من هنا ليس جزءاً من الواجهة المستقرّة. */
export { VoiceCapture, INVOICE_FIELDS } from "./VoiceCapture";
export type { VoiceCaptureProps, VoiceField } from "./VoiceCapture";
export { readInvoiceIntent, matchEvent, FIELD, SPOKEN_EVENT_CODES, SPOKEN_EVENT_RULES } from "./intent";
export type { SpokenIntent, SpokenValue, Provenance } from "./intent";
export { readArabicNumber, canReadArabicNumber, normaliseToken } from "./arabic-number";
export type { NumberReading, NumberFault } from "./arabic-number";
export { speechSupport, listen, translateError } from "./speech";
export type { SpeechUnavailable, SpeechSession, SpeechChunk } from "./speech";
