import { EventStormingPhase } from './board.model';

export interface AgentConfiguration {
  id: string;
  name: string;
  isFacilitator: boolean;
  systemPrompt: string;
  icon: string;
  color: string;
  activePhases?: EventStormingPhase[];
  allowedTools: string[];
  canAskAgents?: string[];
  order: number;
}

export interface AgentConfigurationCreate {
  name: string;
  systemPrompt: string;
  icon: string;
  color: string;
  activePhases?: EventStormingPhase[];
  allowedTools: string[];
  canAskAgents?: string[];
  order: number;
}

export interface AgentConfigurationUpdate {
  name: string;
  systemPrompt: string;
  icon: string;
  color: string;
  activePhases?: EventStormingPhase[];
  allowedTools: string[];
  canAskAgents?: string[];
  order: number;
}

export interface ToolDefinition {
  name: string;
  description: string;
}
