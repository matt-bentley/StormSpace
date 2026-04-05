import { Component, Input, OnChanges, SimpleChanges, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
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
export class AgentInteractionDiagramComponent implements OnChanges, AfterViewInit {
  @Input() agents: AgentConfiguration[] = [];
  @ViewChild('diagramContainer') containerRef!: ElementRef<HTMLDivElement>;

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

  ngAfterViewInit(): void {
    this.recalcSize();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['agents']) {
      this.buildGraph();
    }
  }

  private recalcSize(): void {
    if (this.containerRef) {
      const rect = this.containerRef.nativeElement.getBoundingClientRect();
      if (rect.width > 100 && rect.height > 100) {
        this.baseContainerSize = Math.min(rect.width, rect.height);
        this.svgWidth = Math.min(rect.width, 900);
        this.svgHeight = Math.min(rect.height, 600);
        this.centerX = this.svgWidth / 2;
        this.centerY = this.svgHeight / 2;
        this.buildGraph();
      }
    }
  }

  private buildGraph(): void {
    if (!this.agents || this.agents.length === 0) return;

    this.computeSizes();
    this.layoutNodes();
    this.deriveEdges();
    this.bundleEdges();
  }

  /** Scale node and orbit sizes based on agent count so the diagram stays readable */
  private computeSizes(): void {
    const outerCount = this.agents.filter(a => !a.isFacilitator).length;

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

  private layoutNodes(): void {
    const facilitator = this.agents.find(a => a.isFacilitator);
    const others = this.agents.filter(a => !a.isFacilitator);

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

  private deriveEdges(): void {
    this.edges = [];
    const agentNames = new Set(this.agents.map(a => a.name));
    const agentsWithModTools = new Set(
      this.agents.filter(a => a.allowedTools.some(t => MODIFICATION_TOOLS.includes(t))).map(a => a.name)
    );

    for (const agent of this.agents) {
      // Delegate edges
      if (agent.allowedTools.includes('DelegateToAgent')) {
        for (const target of this.agents) {
          if (target.name === agent.name) continue;
          if (agentsWithModTools.has(target.name)) {
            this.edges.push({ from: agent.name, to: target.name, type: 'delegate', label: 'Delegate' });
          }
        }
      }

      // Review edges
      if (agent.allowedTools.includes('RequestBoardReview')) {
        for (const target of this.agents) {
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
          : this.agents.filter(a => a.name !== agent.name).map(a => a.name);

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

  edgeDash(type: string): string {
    switch (type) {
      case 'delegate': return 'none';
      case 'review': return '8 4';
      case 'ask': return '3 3';
      default: return 'none';
    }
  }

  edgeColor(type: string): string {
    switch (type) {
      case 'delegate': return '#1976d2';
      case 'review': return '#e65100';
      case 'ask': return '#7b1fa2';
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

  trackByEdge(_index: number, edge: AgentEdge): string {
    return `${edge.from}-${edge.to}-${edge.type}`;
  }

  trackByNode(_index: number, node: AgentNode): string {
    return node.agent.id;
  }

  trackByBundledEdge(_index: number, edge: BundledEdge): string {
    return `${edge.from}-${edge.to}`;
  }

  bundledEdgePath(edge: BundledEdge): string {
    const from = this.getNode(edge.from);
    const to = this.getNode(edge.to);
    if (!from || !to) return '';

    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (dist === 0) return '';

    const nx = dx / dist;
    const ny = dy / dist;
    const x1 = from.x + nx * from.radius;
    const y1 = from.y + ny * from.radius;
    const x2 = to.x - nx * to.radius;
    const y2 = to.y - ny * to.radius;

    const curveAmount = edge.curveDirection * 0.2 * dist;
    const px = -ny * curveAmount;
    const py = nx * curveAmount;
    const cx = (x1 + x2) / 2 + px;
    const cy = (y1 + y2) / 2 + py;

    return `M ${x1} ${y1} Q ${cx} ${cy} ${x2} ${y2}`;
  }

  bundledEdgeLabelPos(edge: BundledEdge): { x: number; y: number } {
    const from = this.getNode(edge.from);
    const to = this.getNode(edge.to);
    if (!from || !to) return { x: 0, y: 0 };

    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const dist = Math.sqrt(dx * dx + dy * dy) || 1;
    const nx = dx / dist;
    const ny = dy / dist;
    const curveAmount = edge.curveDirection * 0.2 * dist;
    const px = -ny * curveAmount * 0.6;
    const py = nx * curveAmount * 0.6;

    // Position at 60% along the path (closer to target) to reduce label clustering near the center
    return {
      x: from.x + dx * 0.6 + px,
      y: from.y + dy * 0.6 + py
    };
  }

  bundledEdgePrimaryColor(edge: BundledEdge): string {
    if (edge.types.length === 1) {
      return this.edgeColor(edge.types[0]);
    }
    return '#546e7a';
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
      delegate: 'Del',
      review: 'Rev',
      ask: 'Ask'
    };
    return edge.types.map(t => shortLabels[t]).join(' \u00b7 ');
  }

  bundledEdgeLabelWidth(edge: BundledEdge): number {
    const label = this.bundledEdgeLabel(edge);
    return Math.max(44, label.length * 5.5 + 16);
  }

  bundledEdgeDash(edge: BundledEdge): string {
    if (edge.types.length === 1) {
      return this.edgeDash(edge.types[0]);
    }
    return 'none';
  }

  /** Hide edge labels when the edge is too short to fit one legibly */
  showBundledEdgeLabel(edge: BundledEdge): boolean {
    const from = this.getNode(edge.from);
    const to = this.getNode(edge.to);
    if (!from || !to) return false;
    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    return dist > (from.radius + to.radius + 60);
  }
}
