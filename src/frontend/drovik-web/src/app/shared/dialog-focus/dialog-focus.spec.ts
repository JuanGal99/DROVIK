import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DialogFocus } from './dialog-focus';

@Component({
  imports: [DialogFocus],
  template: `
    <button id="opener" type="button">Abrir</button>
    @if (open()) {
      <section role="dialog" aria-label="Prueba"><button id="first" type="button">Uno</button><button id="off" type="button" disabled>Dos</button><button id="last" type="button">Tres</button></section>
    }`,
})
class Host { readonly open = signal(false); }

describe('DialogFocus', () => {
  function opened() {
    const fixture = TestBed.createComponent(Host);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLElement>('#opener')!.focus();
    fixture.componentInstance.open.set(true);
    fixture.detectChanges();
    return { fixture, element, dialog: element.querySelector<HTMLElement>('[role="dialog"]')! };
  }
  const press = (target: HTMLElement, shiftKey = false) => {
    const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey, bubbles: true, cancelable: true });
    target.dispatchEvent(event);
    return event;
  };

  afterEach(() => { document.body.innerHTML = ''; });

  it('moves focus into the dialog when it opens', () => {
    const { dialog } = opened();
    expect(document.activeElement).toBe(dialog);
  });

  it('goes from the last control back to the first, skipping disabled ones', () => {
    const { element } = opened();
    const last = element.querySelector<HTMLElement>('#last')!;
    last.focus();
    expect(press(last).defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(element.querySelector('#first'));
  });

  it('goes backwards from the first control to the last', () => {
    const { element } = opened();
    const first = element.querySelector<HTMLElement>('#first')!;
    first.focus();
    press(first, true);
    expect(document.activeElement).toBe(element.querySelector('#last'));
  });

  it('leaves normal tabbing alone in the middle of the dialog', () => {
    const { element, dialog } = opened();
    expect(press(dialog).defaultPrevented).toBe(false);
    expect(document.activeElement).not.toBe(element.querySelector('#opener'));
  });

  it('returns focus to what opened it', () => {
    const { fixture, element } = opened();
    fixture.componentInstance.open.set(false);
    fixture.detectChanges();
    expect(document.activeElement).toBe(element.querySelector('#opener'));
  });
});
