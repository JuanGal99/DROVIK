import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-section-placeholder',
  imports: [RouterLink],
  standalone: true,
  template: `<section class="placeholder"><h1>Operación en vivo</h1><p>Aquí verás el mapa de la red y controlarás las simulaciones. Todavía no está disponible.</p><a routerLink="/overview">Volver al resumen</a></section>`,
  styles: [`
    .placeholder { max-width: 520px; padding: 8vh 0; }
    h1 { margin: 0; font-family: var(--font-display); font-size: var(--type-title); font-weight: 600; }
    p { margin: 6px 0 18px; color: var(--text-secondary); line-height: 1.5; }
    a { color: var(--accent); }
  `],
})
export class SectionPlaceholder {}
