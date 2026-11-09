import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-section-placeholder',
  imports: [RouterLink],
  standalone: true,
  template: `<section class="placeholder"><h1>{{ title }}</h1><p>Esta sección todavía no está disponible.</p><a routerLink="/overview">Volver al resumen</a></section>`,
  styles: [`
    .placeholder { max-width: 520px; padding: 8vh 0; }
    h1 { margin: 0; font-family: var(--font-display); font-size: var(--type-title); font-weight: 600; }
    p { margin: 6px 0 18px; color: var(--text-secondary); }
    a { color: var(--accent); }
  `],
})
export class SectionPlaceholder {
  private readonly route = inject(ActivatedRoute);
  readonly title = this.route.snapshot.data['title'] as string;
}
