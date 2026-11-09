import { Component } from '@angular/core';

/** Two depots joined by a route: the unit of work this product moves around. */
@Component({
  selector: 'app-brand-mark',
  standalone: true,
  template: `<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="M6 18V11a3 3 0 0 1 3-3h9" fill="none" stroke="currentColor" stroke-width="2.25"/><rect x="2.5" y="16" width="7" height="6" fill="currentColor"/><rect x="15" y="2" width="7" height="9" fill="none" stroke="currentColor" stroke-width="2.25"/></svg>`,
  styles: [`:host { display: inline-block; width: 24px; height: 24px; color: var(--accent); } svg { display: block; width: 100%; height: 100%; }`],
})
export class BrandMark {}
