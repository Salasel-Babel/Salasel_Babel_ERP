/* حدّ المكوّن المُعلَن. ما لا يُصدَّر من هنا ليس جزءاً من الواجهة المستقرّة.
   The declared component boundary. What is not exported here is not stable API. */
export { AgentQuestionSheet } from "./QuestionSheet";
export type { AgentQuestionSheetProps } from "./QuestionSheet";

export {
  AGENT_ANSWER_KEYS,
  AGENT_ENTITY_KINDS,
  AGENT_TOKEN_GROUP_LENGTH,
  AGENT_TOKEN_GROUP_SEPARATOR,
  AGENT_TOKEN_LENGTH,
  agentSheetFaults,
  answerOf,
  isAgentEntityKind,
  isCreateOption,
} from "./sheet";
export type {
  AgentAnswer,
  AgentCreateDraft,
  AgentEntityKind,
  AgentQuestionOption,
  AgentQuestionSheet as AgentQuestionSheetData,
  AgentSheetFault,
} from "./sheet";

export {
  AGENT_CREATE_OPERATIONS,
  AGENT_PERMITTED_VERBS,
  agentCreateFaults,
  planAgentCreateSheet,
} from "./create-fields";
export type {
  AgentCreateField,
  AgentCreateFieldKind,
  AgentCreateFault,
  AgentCreatePlan,
  AgentCreateRefusal,
  CreateOperationRef,
} from "./create-fields";
