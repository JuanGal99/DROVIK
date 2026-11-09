import { afterNextRender, Component, effect, ElementRef, input, OnDestroy, signal, viewChild } from '@angular/core';
import * as L from 'leaflet';
import { Customer } from '../../core/data/customers-data.service';
import { LiveVehicle } from '../../core/data/live-operations.service';

interface VehicleLayer { marker: L.Marker; line: L.Polyline; route?: LiveVehicle['route']; }

@Component({
  selector: 'app-live-map',
  standalone: true,
  template: `<div #map class="live-map"></div>@if (tilesFailed()) { <p class="tiles-notice">No se pudo cargar el mapa de fondo. Los puntos y vehículos siguen actualizándose.</p> }`,
  styles: [`
    :host { position: relative; display: block; height: 100%; }
    .live-map { height: 100%; background: var(--surface-app); }
    .tiles-notice { position: absolute; right: 10px; bottom: 26px; left: 10px; z-index: 500; margin: 0; padding: 8px 10px; background: var(--surface-overlay); color: var(--text-primary); font-size: var(--type-small); }
  `],
})
export class LiveMap implements OnDestroy {
  readonly points = input<Customer[]>([]);
  readonly vehicles = input<LiveVehicle[]>([]);
  readonly tilesFailed = signal(false);

  private readonly container = viewChild.required<ElementRef<HTMLDivElement>>('map');
  private readonly map = signal<L.Map | null>(null);
  private readonly pointLayer = L.layerGroup();
  private readonly vehicleLayers = new Map<string, VehicleLayer>();
  private fitted = false;
  private resizeObserver?: ResizeObserver;

  constructor() {
    afterNextRender(() => {
      const element = this.container().nativeElement;
      const map = L.map(element, { zoomControl: true }).setView([4.65, -74.09], 12);
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap' })
        .on('tileerror', () => this.tilesFailed.set(true))
        .addTo(map);
      this.pointLayer.addTo(map);
      this.resizeObserver = new ResizeObserver(() => map.invalidateSize());
      this.resizeObserver.observe(element);
      this.map.set(map);
    });
    effect(() => this.drawPoints(this.map(), this.points()));
    effect(() => this.drawVehicles(this.map(), this.vehicles()));
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.map()?.remove();
  }

  private drawPoints(map: L.Map | null, points: Customer[]): void {
    if (!map) return;
    this.pointLayer.clearLayers();
    for (const point of points) {
      L.marker([point.latitude, point.longitude], { icon: L.divIcon({ className: 'map-point', iconSize: [14, 14] }), keyboard: false })
        .bindTooltip(escapeHtml(point.name), { permanent: true, direction: 'right', offset: [8, 0], className: 'map-label' })
        .addTo(this.pointLayer);
    }
    if (points.length && !this.fitted) {
      map.fitBounds(L.latLngBounds(points.map((point) => [point.latitude, point.longitude])), { padding: [40, 40] });
      this.fitted = true;
    }
  }

  private drawVehicles(map: L.Map | null, vehicles: LiveVehicle[]): void {
    if (!map) return;
    const current = new Set(vehicles.map((vehicle) => vehicle.shipmentId));
    for (const [id, layer] of this.vehicleLayers) {
      if (current.has(id)) continue;
      layer.marker.remove();
      layer.line.remove();
      this.vehicleLayers.delete(id);
    }
    for (const vehicle of vehicles) {
      const arrived = vehicle.status === 'Arrived';
      const color = arrived ? '#6bcb9a' : vehicle.isBrokenDown ? '#ee8d7b' : vehicle.isDelayed ? '#e7b163' : '#6cb1ec';
      const layer = this.vehicleLayers.get(vehicle.shipmentId) ?? this.addVehicle(map, vehicle);
      layer.marker.setLatLng([vehicle.latitude, vehicle.longitude]);
      if (vehicle.route && vehicle.route !== layer.route) {
        layer.line.setLatLngs(vehicle.route);
        layer.route = vehicle.route;
      }
      layer.marker.getElement()?.classList.toggle('is-broken', vehicle.isBrokenDown && !arrived);
      layer.line.setStyle({ color, opacity: arrived ? 0.25 : 0.75 });
      layer.marker.getElement()?.classList.toggle('is-delayed', vehicle.isDelayed && !arrived);
      layer.marker.getElement()?.classList.toggle('is-arrived', arrived);
    }
  }

  private addVehicle(map: L.Map, vehicle: LiveVehicle): VehicleLayer {
    const layer: VehicleLayer = {
      route: vehicle.route,
      line: L.polyline(vehicle.route ?? [], { weight: 3 }).addTo(map),
      marker: L.marker([vehicle.latitude, vehicle.longitude], {
        icon: L.divIcon({ className: 'map-vehicle', html: '<i class="pi pi-truck"></i>', iconSize: [28, 28] }),
        zIndexOffset: 500,
      }).bindTooltip(escapeHtml(vehicle.trackingCode), { direction: 'top', offset: [0, -14] }).addTo(map),
    };
    this.vehicleLayers.set(vehicle.shipmentId, layer);
    return layer;
  }
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (character) => `&#${character.charCodeAt(0)};`);
}
