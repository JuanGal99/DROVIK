import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-section-placeholder',
  imports: [RouterLink],
  standalone: true,
  template: `<section class="section-placeholder"><div class="placeholder-icon"><i class="pi pi-wrench" aria-hidden="true"></i></div><p class="placeholder-eyebrow">MÓDULO EN PREPARACIÓN</p><h1>{{ title }}</h1><p class="placeholder-description">Esta sección forma parte de la navegación prevista de DROVIK. Su flujo operativo se desarrollará en una siguiente etapa.</p><a routerLink="/overview">Volver al resumen</a></section>`,
  styles: [`.section-placeholder{max-width:560px;margin:10vh auto;padding:46px 0;border-top:1px solid var(--line-subtle)}.placeholder-icon{display:grid;width:38px;height:38px;place-items:center;border:1px solid var(--line-subtle);color:var(--accent)}.placeholder-eyebrow{margin:28px 0 10px;color:#8596a3;font-size:10px;font-weight:700;letter-spacing:.13em}.section-placeholder h1{margin:0;color:var(--text-primary);font-size:30px;font-weight:620;letter-spacing:-.035em}.placeholder-description{max-width:430px;margin:14px 0 24px;color:var(--text-secondary);font-size:13px;line-height:1.65}.section-placeholder a{color:#71cde2;font-size:12px;text-decoration:none}.section-placeholder a:hover{text-decoration:underline}@media(max-width:720px){.section-placeholder{margin:6vh auto;padding:30px 0}.section-placeholder h1{font-size:26px}}`],
})
export class SectionPlaceholder {
  private readonly route = inject(ActivatedRoute);
  readonly title = this.route.snapshot.data['title'] as string;
}
