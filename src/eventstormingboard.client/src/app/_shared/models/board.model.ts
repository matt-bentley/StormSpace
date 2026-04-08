import { AgentConfiguration } from './agent-configuration.model';

export type EventStormingPhase =
  | 'setContext'
  | 'identifyEvents'
  | 'addCommandsAndPolicies'
  | 'defineAggregates'
  | 'breakItDown';

export const EVENT_STORMING_PHASES: { value: EventStormingPhase; label: string }[] = [
  { value: 'setContext', label: 'Set the Context' },
  { value: 'identifyEvents', label: 'Identify Events' },
  { value: 'addCommandsAndPolicies', label: 'Add Commands & Policies' },
  { value: 'defineAggregates', label: 'Define Aggregates' },
  { value: 'breakItDown', label: 'Break It Down' }
];

export interface BoardDto {
  id: string;
  name: string;
  domain?: string;
  sessionScope?: string;
  phase?: EventStormingPhase;
  autonomousEnabled: boolean;
  notes: NoteDto[];
  connections: ConnectionDto[];
  boundedContexts: BoundedContextDto[];
  agentConfigurations: AgentConfiguration[];
}

export interface BoardSummaryDto {
  id: string;
  name: string;
}

export interface BoardCreateDto {
  name: string;
  domain?: string;
  sessionScope?: string;
  phase?: EventStormingPhase;
  autonomousEnabled?: boolean;
}

export interface NoteDto {
  id: string;
  text: string;
  x: number;
  y: number;
  width: number;
  height: number;
  color: string;
  type: string;
}

export interface ConnectionDto {
  fromNoteId: string;
  toNoteId: string;
}

export interface BoundedContextDto {
  id: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  color?: string;
}
