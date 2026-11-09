import { Component, computed, input } from '@angular/core';

export interface NetworkPoint { id: string; name: string; latitude: number; longitude: number; }
export interface NetworkRoute { id: string; originId: string; destinationId: string; tone: 'moving' | 'delayed' | 'waiting'; }

const WIDTH = 760;
const HEIGHT = 420;
const PADDING = { x: 40, top: 34, bottom: 34 };
const LABEL_WIDTH = 170;

/**
 * Schematic of the delivery network. Positions come from real coordinates but the axes are stretched to fit,
 * and lines join origin and destination directly: it shows the shape of the work, not a road or a vehicle.
 */
@Component({
  selector: 'app-network-map',
  standalone: true,
  template: `
    <svg [attr.viewBox]="viewBox" role="img" [attr.aria-label]="summary()">
      @for (route of drawnRoutes(); track route.id) { <path [attr.d]="route.path" [class]="'route ' + route.tone" /> }
      @for (point of drawnPoints(); track point.id) {
        <g [attr.transform]="'translate(' + point.x + ' ' + point.y + ')'" [class.busy]="point.busy">
          <rect class="node" x="-6" y="-6" width="12" height="12" />
          <text [attr.x]="point.labelLeft ? -14 : 14" y="4" [attr.text-anchor]="point.labelLeft ? 'end' : 'start'">{{ point.name }}</text>
        </g>
      }
    </svg>`,
  styles: [`
    :host { display: block; }
    svg { display: block; width: 100%; height: auto; }
    .route { fill: none; stroke-width: 2; }
    .route.moving { stroke: var(--accent); }
    .route.delayed { stroke: var(--warning); }
    .route.waiting { stroke: var(--text-muted); stroke-dasharray: 3 6; }
    .node { fill: var(--surface-app); stroke: var(--text-secondary); stroke-width: 2; }
    .busy .node { fill: var(--accent); stroke: var(--accent); }
    text { fill: var(--text-primary); font-size: 14px; font-weight: 500; paint-order: stroke; stroke: var(--surface-panel); stroke-width: 5px; stroke-linejoin: round; }
  `],
})
export class NetworkMap {
  readonly points = input.required<NetworkPoint[]>();
  readonly routes = input<NetworkRoute[]>([]);
  readonly viewBox = `0 0 ${WIDTH} ${HEIGHT}`;

  private readonly positioned = computed(() => {
    const points = this.points();
    const lats = points.map((point) => point.latitude);
    const lngs = points.map((point) => point.longitude);
    const [minLat, maxLat, minLng, maxLng] = [Math.min(...lats), Math.max(...lats), Math.min(...lngs), Math.max(...lngs)];
    const usableWidth = WIDTH - PADDING.x * 2;
    const usableHeight = HEIGHT - PADDING.top - PADDING.bottom;
    return new Map(points.map((point) => [point.id, {
      ...point,
      x: Math.round(PADDING.x + ((point.longitude - minLng) / (maxLng - minLng || 1)) * usableWidth),
      y: Math.round(PADDING.top + ((maxLat - point.latitude) / (maxLat - minLat || 1)) * usableHeight),
    }]));
  });

  readonly drawnPoints = computed(() => {
    const all = [...this.positioned().values()];
    const busy = new Set(this.routes().flatMap((route) => [route.originId, route.destinationId]));
    return all.map((point) => ({
      ...point,
      busy: busy.has(point.id),
      // A label goes left when it would leave the canvas or run into the next point on its row.
      labelLeft: point.x + LABEL_WIDTH > WIDTH || all.some((other) => other.id !== point.id && other.x > point.x && other.x - point.x < LABEL_WIDTH && Math.abs(other.y - point.y) < 26),
    }));
  });

  readonly drawnRoutes = computed(() => {
    const positions = this.positioned();
    return this.routes().flatMap((route) => {
      const from = positions.get(route.originId);
      const to = positions.get(route.destinationId);
      if (!from || !to) return [];
      // A slight bow keeps opposite trips between the same two points from overlapping.
      const bow = 0.14;
      const controlX = (from.x + to.x) / 2 - (to.y - from.y) * bow;
      const controlY = (from.y + to.y) / 2 + (to.x - from.x) * bow;
      return [{ id: route.id, tone: route.tone, path: `M${from.x} ${from.y} Q${Math.round(controlX)} ${Math.round(controlY)} ${to.x} ${to.y}` }];
    });
  });

  readonly summary = computed(() => `Esquema de la red: ${this.points().length} puntos y ${this.routes().length} envíos en curso.`);
}
