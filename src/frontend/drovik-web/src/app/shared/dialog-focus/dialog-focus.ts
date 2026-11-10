import { AfterViewInit, DOCUMENT, Directive, ElementRef, OnDestroy, inject } from '@angular/core';

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex="0"]';

/** Keeps the keyboard inside an open dialog and gives focus back to whatever opened it. */
@Directive({
  selector: '[role="dialog"]',
  host: { tabindex: '-1', '(keydown.tab)': 'keepInside($event, false)', '(keydown.shift.tab)': 'keepInside($event, true)' },
})
export class DialogFocus implements AfterViewInit, OnDestroy {
  private readonly dialog = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly document = inject(DOCUMENT);
  private readonly opener = this.document.activeElement as HTMLElement | null;

  // The dialog itself takes focus so a screen reader announces its title before any field.
  ngAfterViewInit(): void { this.dialog.focus({ preventScroll: true }); }

  ngOnDestroy(): void { if (this.opener?.isConnected) this.opener.focus({ preventScroll: true }); }

  keepInside(event: Event, backwards: boolean): void {
    const items = Array.from(this.dialog.querySelectorAll<HTMLElement>(FOCUSABLE));
    if (!items.length) { event.preventDefault(); return; }
    const active = this.document.activeElement;
    const edge = backwards ? items[0] : items[items.length - 1];
    if (active !== edge && !(backwards && active === this.dialog)) return;
    event.preventDefault();
    (backwards ? items[items.length - 1] : items[0]).focus();
  }
}
