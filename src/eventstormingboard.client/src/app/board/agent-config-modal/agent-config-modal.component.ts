import { Component, Inject, OnInit } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AgentConfiguration, AgentConfigurationCreate, AgentConfigurationUpdate, ToolDefinition } from '../../_shared/models/agent-configuration.model';
import { BoardsService } from '../../_shared/services/boards.service';
import { EVENT_STORMING_PHASES } from '../../_shared/models/board.model';
import { v4 as uuid } from 'uuid';
import { AgentInteractionDiagramComponent } from '../agent-interaction-diagram/agent-interaction-diagram.component';

export interface AgentConfigModalData {
  boardId: string;
  agents: AgentConfiguration[];
}

@Component({
  selector: 'app-agent-config-modal',
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatExpansionModule,
    AgentInteractionDiagramComponent
  ],
  templateUrl: './agent-config-modal.component.html',
  styleUrls: ['./agent-config-modal.component.scss']
})
export class AgentConfigModalComponent implements OnInit {
  public agents: AgentConfiguration[] = [];
  public availableTools: ToolDefinition[] = [];
  public phases = EVENT_STORMING_PHASES;
  public saving = false;

  public viewMode: 'config' | 'diagram' = 'config';

  public readonly AGENT_ICONS = [
    'psychology', 'explore', 'account_tree', 'architecture', 'edit_note',
    'auto_fix_high', 'smart_toy', 'hub', 'memory', 'bug_report',
    'lightbulb', 'visibility', 'build', 'code', 'analytics',
    'category', 'schema', 'device_hub', 'science', 'engineering'
  ];

  public readonly AGENT_COLORS = [
    '#3f51b5', '#e65100', '#00897b', '#7b1fa2', '#2e7d32',
    '#ff8f00', '#c62828', '#1565c0', '#4e342e', '#37474f',
    '#ad1457', '#00838f', '#558b2f', '#6a1b9a', '#ef6c00'
  ];

  public readonly MODEL_TYPES = [
    { value: 'gpt-4.1', label: 'GPT 4.1' },
    { value: 'gpt-5.2', label: 'GPT 5.2' }
  ];

  public readonly REASONING_EFFORTS = [
    { value: 'low', label: 'Low' },
    { value: 'medium', label: 'Medium' }
  ];

  constructor(
    public dialogRef: MatDialogRef<AgentConfigModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AgentConfigModalData,
    private boardsService: BoardsService
  ) {}

  ngOnInit(): void {
    this.agents = this.data.agents.map(a => ({ ...a, activePhases: a.activePhases ? [...a.activePhases] : undefined, allowedTools: [...a.allowedTools], canAskAgents: a.canAskAgents ? [...a.canAskAgents] : undefined }));
    this.boardsService.getAvailableTools(this.data.boardId).subscribe({
      next: tools => this.availableTools = tools,
      error: err => console.error('Failed to load available tools:', err)
    });
  }

  public isToolEnabled(agent: AgentConfiguration, toolName: string): boolean {
    return agent.allowedTools.includes(toolName);
  }

  public toggleTool(agent: AgentConfiguration, toolName: string): void {
    const idx = agent.allowedTools.indexOf(toolName);
    if (idx >= 0) {
      agent.allowedTools.splice(idx, 1);
    } else {
      agent.allowedTools.push(toolName);
    }
  }

  public isPhaseEnabled(agent: AgentConfiguration, phaseValue: string): boolean {
    if (!agent.activePhases) return true;
    return agent.activePhases.includes(phaseValue as any);
  }

  public togglePhase(agent: AgentConfiguration, phaseValue: string): void {
    if (!agent.activePhases) {
      agent.activePhases = this.phases.map(p => p.value).filter(v => v !== phaseValue);
      return;
    }
    const idx = agent.activePhases.indexOf(phaseValue as any);
    if (idx >= 0) {
      agent.activePhases.splice(idx, 1);
      if (agent.activePhases.length === this.phases.length) {
        agent.activePhases = undefined;
      }
    } else {
      agent.activePhases.push(phaseValue as any);
      if (agent.activePhases.length === this.phases.length) {
        agent.activePhases = undefined;
      }
    }
  }

  public allPhasesEnabled(agent: AgentConfiguration): boolean {
    return !agent.activePhases;
  }

  public toggleAllPhases(agent: AgentConfiguration): void {
    if (agent.activePhases) {
      agent.activePhases = undefined;
    } else {
      agent.activePhases = [];
    }
  }

  public addAgent(): void {
    const newAgent: AgentConfiguration = {
      id: uuid(),
      name: 'New Agent',
      isFacilitator: false,
      systemPrompt: '',
      icon: 'smart_toy',
      color: '#37474f',
      allowedTools: ['GetBoardState', 'GetRecentEvents'],
      order: this.agents.length,
      modelType: 'gpt-4.1',
      temperature: 0.7
    };
    this.agents.push(newAgent);
  }

  public getOtherAgentNames(agent: AgentConfiguration): string[] {
    return this.agents.filter(a => a.id !== agent.id).map(a => a.name);
  }

  public allAgentsAskable(agent: AgentConfiguration): boolean {
    return !agent.canAskAgents;
  }

  public toggleAllAskableAgents(agent: AgentConfiguration): void {
    if (agent.canAskAgents) {
      agent.canAskAgents = undefined;
    } else {
      agent.canAskAgents = [];
    }
  }

  public isAgentAskable(agent: AgentConfiguration, targetName: string): boolean {
    if (!agent.canAskAgents) return true;
    return agent.canAskAgents.includes(targetName);
  }

  public toggleAskableAgent(agent: AgentConfiguration, targetName: string): void {
    if (!agent.canAskAgents) {
      // Switch from "all" to specific — include all except the toggled one
      agent.canAskAgents = this.getOtherAgentNames(agent).filter(n => n !== targetName);
      return;
    }
    const idx = agent.canAskAgents.indexOf(targetName);
    if (idx >= 0) {
      agent.canAskAgents.splice(idx, 1);
    } else {
      agent.canAskAgents.push(targetName);
      // If all are selected, switch back to undefined (all)
      if (agent.canAskAgents.length >= this.getOtherAgentNames(agent).length) {
        agent.canAskAgents = undefined;
      }
    }
  }

  public removeAgent(index: number): void {
    if (this.agents[index].isFacilitator) return;
    this.agents.splice(index, 1);
  }

  public onModelTypeChange(agent: AgentConfiguration): void {
    if (agent.modelType === 'gpt-5.2') {
      agent.temperature = undefined;
      agent.reasoningEffort = agent.reasoningEffort || 'low';
    } else {
      agent.reasoningEffort = undefined;
      agent.temperature = agent.temperature ?? 0.7;
    }
  }

  public onCancel(): void {
    this.dialogRef.close();
  }

  public onSave(): void {
    this.saving = true;
    this.dialogRef.close(this.agents);
  }
}
