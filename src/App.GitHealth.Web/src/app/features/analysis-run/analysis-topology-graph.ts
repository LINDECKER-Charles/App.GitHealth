import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AnalysisReferenceProgress } from '../../core/api/api.models';
import { buildGraph } from '../../core/analysis/analysis-graph';

const viewportWidth = 312;
const nodeRadius = 3.4;
const cursorRadius = 6.5;

/**
 * The shape the run is uncovering: a trunk for the baseline, a fork per reference placed,
 * drawn as they land. A window on the references being read, not a map of the repository.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-analysis-topology-graph',
  styles: `
    :host {
      display: block;
      min-height: 0;
      overflow: hidden;
      padding: 12px 14px 6px;
    }

    .graph-head {
      display: flex;
      align-items: center;
      gap: 8px;
      font: var(--type-label);
      letter-spacing: var(--tracking-label);
      text-transform: uppercase;
      color: var(--text-muted);
    }

    .graph-count {
      margin-left: auto;
      font: var(--type-code-sm);
      text-transform: none;
      letter-spacing: 0;
      font-variant-numeric: tabular-nums;
    }

    .graph-canvas {
      position: relative;
      margin-top: 8px;
    }

    svg {
      display: block;
      overflow: visible;
    }

    .graph-trunk {
      stroke: var(--status-info-solid);
      stroke-width: 1.6;
      stroke-linecap: round;
      stroke-dasharray: 400;
      stroke-dashoffset: 400;
      animation: gh-draw 900ms var(--ease-out) forwards;
    }

    .graph-fork {
      fill: none;
      stroke: var(--border-strong);
      stroke-width: 1.4;
      stroke-linecap: round;
      stroke-dasharray: 240;
      stroke-dashoffset: 240;
      animation: gh-draw 520ms var(--ease-out) forwards;
    }

    .graph-node {
      stroke-width: 1.5;
      animation: gh-fade 200ms var(--ease-out) both;
    }

    .graph-cursor {
      fill: none;
      stroke: var(--status-info-solid);
      stroke-width: 1.2;
      animation: gh-pulse 900ms linear infinite;
    }

    .graph-baseline {
      font: 11px/1 var(--font-mono);
      fill: var(--text-secondary);
    }

    .graph-label {
      position: absolute;
      font: 11px/1 var(--font-mono);
      color: var(--text-secondary);
      white-space: nowrap;
      animation: gh-fade 240ms var(--ease-out) both;
    }
  `,
  template: `
    <div class="graph-head">
      <span i18n="@@analysisRun.graph.title">Topology read</span>
      <span class="graph-count">{{ placedLabel() }}</span>
    </div>
    <div class="graph-canvas" [style.width.px]="width" [style.height.px]="graph().height">
      <svg
        [attr.viewBox]="'0 0 ' + width + ' ' + graph().height"
        [attr.width]="width"
        [attr.height]="graph().height"
        aria-hidden="true"
      >
        <line class="graph-trunk" x1="24" y1="6" x2="24" [attr.y2]="graph().trunkEnd"></line>
        @for (node of graph().nodes; track node.id) {
          <path class="graph-fork" [attr.d]="node.path"></path>
          <circle
            class="graph-node"
            [attr.cx]="node.x"
            [attr.cy]="node.y"
            [attr.r]="nodeRadius"
            [style.fill]="node.isHollow ? 'var(--surface)' : toneColor(node.tone)"
            [style.stroke]="toneColor(node.tone)"
          ></circle>
        }
        @if (graph().cursorX !== null) {
          <circle
            class="graph-cursor"
            [attr.cx]="graph().cursorX"
            [attr.cy]="graph().cursorY"
            [attr.r]="cursorRadius"
          ></circle>
        }
        <text class="graph-baseline" x="31" [attr.y]="graph().trunkLabelY">{{ baseline() }}</text>
      </svg>
      @for (node of graph().nodes; track node.id) {
        <span class="graph-label" [style.left.px]="node.labelX" [style.top.px]="node.y - 6">{{
          node.label
        }}</span>
      }
    </div>
  `,
})
export class AnalysisTopologyGraph {
  readonly references = input.required<readonly AnalysisReferenceProgress[]>();
  readonly baseline = input.required<string>();

  protected readonly width = viewportWidth;
  protected readonly nodeRadius = nodeRadius;
  protected readonly cursorRadius = cursorRadius;

  protected readonly graph = computed(() => buildGraph(this.references()));

  protected readonly placedLabel = computed(() => {
    const graph = this.graph();
    return $localize`:@@analysisRun.graph.placed:${graph.placed}:placed: / ${graph.total}:total: placed`;
  });

  protected toneColor(tone: string): string {
    return `var(--status-${tone}-solid)`;
  }
}
