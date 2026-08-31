/* حدّ طبقة التصميم المُعلَن. ما لا يُصدَّر من هنا ليس جزءاً من العقد الذي
   يرثه من يبني قسماً — ومن استورد من ملفٍّ داخلي فقد بنى على ما قد يتغيّر. */
import "../styles/cinematic.css";
import "../styles/motion.css";
import "../styles/primitives.css";
import "../styles/presence.css";
import "../styles/shell.css";

export { MOTION, MOTION_DWELL_MS, useMoment, revealAt } from "./motion";
export type { MotionName } from "./motion";

export {
  Surface,
  Panel,
  StatCard,
  Field,
  Button,
  StatusBadge,
  RefusalPanel,
  ProgressBar,
  QuantityValue,
  RateValue,
  EmptyState,
  AlertBell,
} from "./primitives";
export type {
  SurfaceProps,
  PanelProps,
  StatCardProps,
  FieldProps,
  ButtonProps,
  RefusalProps,
  DocState,
  Provenance,
} from "./primitives";

export { LedgerTable } from "./LedgerTable";
export type { LedgerRow, LedgerLabels, LedgerTableProps } from "./LedgerTable";

export {
  ConfidenceMeter,
  ProvenanceMark,
  InferredValue,
  StreamingReveal,
  VoiceTrace,
  PresencePanel,
  bandOf,
} from "./presence";
export type { ConfidenceBand, TraceStep } from "./presence";
