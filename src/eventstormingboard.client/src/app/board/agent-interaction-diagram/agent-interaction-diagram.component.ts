import { AfterViewInit, Component, DestroyRef, ElementRef, effect, inject, input, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AgentConfiguration } from '../../_shared/models/agent-configuration.model';

export interface AgentNode {
  agent: AgentConfiguration;
  x: number;
  y: number;
  radius: number;
}

export interface AgentEdge {
  from: string;
  to: string;
  type: 'delegate' | 'review' | 'ask';
  label: string;
}

export interface BundledEdge {
  from: string;
  to: string;
  types: ('delegate' | 'review' | 'ask')[];
  curveDirection: number;
}

const MODIFICATION_TOOLS = ['CreateNote', 'CreateNotes', 'EditNoteText', 'MoveNotes', 'DeleteNotes', 'CreateConnection', 'CreateConnections'];

@Component({
  selector: 'app-agent-interaction-diagram',
  imports: [CommonModule, MatIconModule, MatTooltipModule],
  templateUrl: './agent-interaction-diagram.component.html',
  styleUrls: ['./agent-interaction-diagram.component.scss']
})
export class AgentInteractionDiagramComponent implements AfterViewInit {
  private destroyRef = inject(DestroyRef);
  readonly agents = input<AgentConfiguration[]>([]);
  readonly containerRef = viewChild.required<ElementRef<HTMLDivElement>>('diagramContainer');

  nodes: AgentNode[] = [];
  edges: AgentEdge[] = [];
  bundledEdges: BundledEdge[] = [];
  svgWidth = 700;
  svgHeight = 500;
  centerX = 350;
  centerY = 250;
  orbitRadius = 185;
  private nodeRadius = 36;
  private baseContainerSize = 500;

  private destroyed = false;

  constructor() {
    effect(() => {
      const agents = this.agents();
      this.buildGraph(agents);
    });
  }

  ngAfterViewInit(): void {
    this.destroyRef.onDestroy(() => this.destroyed = true);
    setTimeout(() => {
      if (this.destroyed) return;
      this.recalcSize();
    });
  }

  private recalcSize(): void {
    if (this.containerRef()) {
      const rect = this.containerRef().nativeElement.getBoundingClientRect();
      if (rect.width > 100 && rect.height > 100) {
        this.baseContainerSize = Math.min(rect.width, rect.height);
        this.svgWidth = Math.min(rect.width, 900);
        this.svgHeight = Math.min(rect.height, 600);
        this.centerX = this.svgWidth / 2;
        this.centerY = this.svgHeight / 2;
        this.buildGraph(this.agents());
      }
    }
  }

  private buildGraph(agents: AgentConfiguration[]): void {
    if (!agents || agents.length === 0) return;

    this.computeSizes(agents);
    this.layoutNodes(agents);
    this.deriveEdges(agents);
    this.bundleEdges();
  }

  /** Scale node and orbit sizes based on agent count so the diagram stays readable */
  private computeSizes(agents: AgentConfiguration[]): void {
    const outerCount = agents.filter(a => !a.isFacilitator).length;

    if (outerCount <= 1) {
      this.nodeRadius = 40;
      this.orbitRadius = this.baseContainerSize * 0.22;
    } else if (outerCount <= 4) {
      this.nodeRadius = 38;
      this.orbitRadius = this.baseContainerSize * 0.30;
    } else if (outerCount <= 7) {
      this.nodeRadius = 34;
      this.orbitRadius = this.baseContainerSize * 0.35;
    } else {
      // 8+ outer agents: shrink nodes, expand orbit
      this.nodeRadius = Math.max(24, 36 - (outerCount - 7) * 2);
      this.orbitRadius = this.baseContainerSize * Math.min(0.42, 0.35 + (outerCount - 7) * 0.02);
    }
  }

  private layoutNodes(agents: AgentConfiguration[]): void {
    const facilitator = agents.find(a => a.isFacilitator);
    const others = agents.filter(a => !a.isFacilitator);

    this.nodes = [];

    if (facilitator) {
      this.nodes.push({
        agent: facilitator,
        x: this.centerX,
        y: this.centerY,
        radius: this.nodeRadius + 8
      });
    }

    const count = others.length;
    const startAngle = -Math.PI / 2;
    others.forEach((agent, i) => {
      const angle = startAngle + (2 * Math.PI * i) / count;
      this.nodes.push({
        agent,
        x: this.centerX + this.orbitRadius * Math.cos(angle),
        y: this.centerY + this.orbitRadius * Math.sin(angle),
        radius: this.nodeRadius
      });
    });
  }

  private deriveEdges(agents: AgentConfiguration[]): void {
    this.edges = [];
    const agentNames = new Set(agents.map(a => a.name));
    const agentsWithModTools = new Set(
      agents.filter(a => a.allowedTools.some(t => MODIFICATION_TOOLS.includes(t))).map(a => a.name)
    );

    for (const agent of agents) {
      // Delegate edges
      if (agent.allowedTools.includes('DelegateToAgent')) {
        for (const target of agents) {
          if (target.name === agent.name) continue;
          if (agentsWithModTools.has(target.name)) {
            this.edges.push({ from: agent.name, to: target.name, type: 'delegate', label: 'Delegate' });
          }
        }
      }

      // Review edges
      if (agent.allowedTools.includes('RequestBoardReview')) {
        for (const target of agents) {
          if (target.name === agent.name) continue;
          if (!target.isFacilitator) {
            this.edges.push({ from: agent.name, to: target.name, type: 'review', label: 'Review' });
          }
        }
      }

      // Ask edges
      if (agent.allowedTools.includes('AskAgentQuestion')) {
        const askTargets = agent.canAskAgents
          ? agent.canAskAgents.filter(n => agentNames.has(n))
          : agents.filter(a => a.name !== agent.name).map(a => a.name);

        for (const targetName of askTargets) {
          this.edges.push({ from: agent.name, to: targetName, type: 'ask', label: 'Ask' });
        }
      }
    }
  }

  private bundleEdges(): void {
    const map = new Map<string, BundledEdge>();
    for (const edge of this.edges) {
      const key = `${edge.from}\u2192${edge.to}`;
      if (map.has(key)) {
        const bundle = map.get(key)!;
        if (!bundle.types.includes(edge.type)) {
          bundle.types.push(edge.type);
        }
      } else {
        map.set(key, {
          from: edge.from,
          to: edge.to,
          types: [edge.type],
          curveDirection: 0
        });
      }
    }

    const processed = new Set<string>();
    for (const [key, bundle] of map) {
      if (processed.has(key)) continue;
      const reverseKey = `${bundle.to}\u2192${bundle.from}`;
      if (map.has(reverseKey) && !processed.has(reverseKey)) {
        bundle.curveDirection = 1;
        map.get(reverseKey)!.curveDirection = -1;
        processed.add(key);
        processed.add(reverseKey);
      }
    }

    this.bundledEdges = Array.from(map.values());
  }

  getNode(name: string): AgentNode | undefined {
    return this.nodes.find(n => n.agent.name === name);
  }

  edgePath(edge: AgentEdge): string {
    const from = this.getNode(edge.from);
    const to = this.getNode(edge.to);
    if (!from || !to) return '';

    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (dist === 0) return '';

    // Offset start/end by node radius
    const nx = dx / dist;
    const ny = dy / dist;
    const x1 = from.x + nx * from.radius;
    const y1 = from.y + ny * from.radius;
    const x2 = to.x - nx * to.radius;
    const y2 = to.y - ny * to.radius;

    // Curve offset based on edge type to prevent overlap
    const typeOffsets: Record<string, number> = { delegate: -0.12, review: 0, ask: 0.12 };
    const curveStrength = (typeOffsets[edge.type] ?? 0) * dist;

    // Perpendicular offset for the control point
    const px = -ny * curveStrength;
    const py = nx * curveStrength;
    const cx = (x1 + x2) / 2 + px;
    const cy = (y1 + y2) / 2 + py;

    return `M ${x1} ${y1} Q ${cx} ${cy} ${x2} ${y2}`;
  }

  edgeDash(type: string): string | null {
    switch (type) {
      case 'delegate': return null;
      case 'review': return '8 6';
      case 'ask': return '4 6';
      default: return null;
    }
  }

  edgeColor(type: string): string {
    switch (type) {
      case 'delegate': return '#00e6ff';
      case 'review': return '#ff9900';
      case 'ask': return '#cc00ff';
      default: return '#999';
    }
  }

  edgeLabelPos(edge: AgentEdge): { x: number; y: number } {
    const from = this.getNode(edge.from);
    const to = this.getNode(edge.to);
    if (!from || !to) return { x: 0, y: 0 };

    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const dist = Math.sqrt(dx * dx + dy * dy) || 1;
    const nx = dx / dist;
    const ny = dy / dist;

    const typeOffsets: Record<string, number> = { delegate: -0.12, review: 0, ask: 0.12 };
    const curveStrength = (typeOffsets[edge.type] ?? 0) * dist;
    const px = -ny * curveStrength * 0.5;
    const py = nx * curveStrength * 0.5;

    return {
      x: (from.x + to.x) / 2 + px,
      y: (from.y + to.y) / 2 + py
    };
  }

  /** Unique filter ID for each node's glow */
  glowId(node: AgentNode): string {
    return `glow-${node.agent.id.replace(/[^a-zA-Z0-9]/g, '')}`;
  }

  /** Unique gradient ID for each node */
  gradientId(node: AgentNode): string {
    return `grad-${node.agent.id.replace(/[^a-zA-Z0-9]/g, '')}`;
  }

  /** Convert hex color to rgba for gradient stops */
  hexToRgba(hex: string, alpha: number): string {
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }

  /** Lighter version of a hex color for gradient */
  lighten(hex: string, amount: number): string {
    const r = Math.min(255, parseInt(hex.slice(1, 3), 16) + amount);
    const g = Math.min(255, parseInt(hex.slice(3, 5), 16) + amount);
    const b = Math.min(255, parseInt(hex.slice(5, 7), 16) + amount);
    return `rgb(${r}, ${g}, ${b})`;
  }

  darken(hex: string, amount: number): string {
    const r = Math.max(0, parseInt(hex.slice(1, 3), 16) - amount);
    const g = Math.max(0, parseInt(hex.slice(3, 5), 16) - amount);
    const b = Math.max(0, parseInt(hex.slice(5, 7), 16) - amount);
    return `rgb(${r}, ${g}, ${b})`;
  }

  trackByEdge(_index: number, edge: AgentEdge): string {
    return `${edge.from}-${edge.to}-${edge.type}`;
  }

  trackByNode(_index: number, node: AgentNode): string {
    return node.agent.id;
  }

  trackByBundledEdge(_index: number, edge: BundledEdge): string {
    return `${edge.from}-${edge.to}`;
  }

  bundledTypeEdgePath(edge: BundledEdge, typeIndex: number, totalTypes: number): string {
    const from = this.getNode(edge.from);
    const to = this.getNode(edge.to);
    if (!from || !to) return '';

    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (dist === 0) return '';

    const nx = dx / dist;
    const ny = dy / dist;
    
    // Spread lines sideways so they don't overlap when there are multiple types
    const spread = (typeIndex - (totalTypes - 1) / 2) * 35; // 35px spread apart at curve center
    
    const curveAmount = edge.curveDirection === 0 
      ? spread  // If straight line, use spread to bow them out
      : (edge.curveDirection * 0.2 * dist) + spread; // If already bowed, add spread

    const px = -ny * curveAmount;
    const py = nx * curveAmount;
    
    // Slightly offset endpoints to prevent arrowheads from clashing visually
    const endShift = (typeIndex - (totalTypes - 1) / 2) * 8;
    const x1 = from.x + nx * from.radius - ny * endShift;
    const y1 = from.y + ny * from.radius + nx * endShift;
    const x2 = to.x - nx * to.radius - ny * endShift;
    const y2 = to.y - ny * to.radius + nx * endShift;

    const cx = (x1 + x2) / 2 + px;
    const cy = (y1 + y2) / 2 + py;

    return `M ${x1} ${y1} Q ${cx} ${cy} ${x2} ${y2}`;
  }

  bundledEdgePrimaryColor(edge: BundledEdge): string {
    if (edge.types.length === 1) {
      return this.edgeColor(edge.types[0]);
    }
    return '#00ffcc'; // Brighter neon color for multiple links
  }

  bundledEdgeLabel(edge: BundledEdge): string {
    if (edge.types.length === 1) {
      const labels: Record<string, string> = {
        delegate: 'Delegate',
        review: 'Review',
        ask: 'Ask'
      };
      return labels[edge.types[0]] ?? edge.types[0];
    }
    const shortLabels: Record<string, string> = {
      delegate: 'Delegate',
      review: 'Review',
      ask: 'Ask'
    };
    return edge.types.map(t => shortLabels[t]).join(', ');
  }
}
