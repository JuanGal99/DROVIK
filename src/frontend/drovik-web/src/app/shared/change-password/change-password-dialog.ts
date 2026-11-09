import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-change-password-dialog',
  imports: [FormsModule],
  host: { '(document:keydown.escape)': 'closed.emit()' },
  styles: ':host, .dialog-root { display: contents; }',
  template: `
    <div class="ops-page dialog-root">
      <div class="modal-backdrop" (click)="closed.emit()"></div>
      <section class="ops-modal" role="dialog" aria-modal="true" aria-labelledby="password-title">
        <header><h2 id="password-title">Cambiar contraseña</h2><button class="close-button" type="button" aria-label="Cerrar formulario" (click)="closed.emit()"><i class="pi pi-times"></i></button></header>
        <form #passwordForm="ngForm" (ngSubmit)="submit()">
          <label>Contraseña actual<input name="current" type="password" required autocomplete="current-password" [ngModel]="current()" (ngModelChange)="current.set($event)"></label>
          <label>Nueva contraseña<input name="next" type="password" required minlength="12" autocomplete="new-password" [ngModel]="next()" (ngModelChange)="next.set($event)"><small>Al menos 12 caracteres, con mayúscula, minúscula, número y símbolo.</small></label>
          <label>Repite la nueva contraseña<input name="confirmation" type="password" required autocomplete="new-password" [ngModel]="confirmation()" (ngModelChange)="confirmation.set($event)"></label>
          <small>Al guardar se cerrarán todas tus sesiones y tendrás que entrar de nuevo.</small>
          @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
          <footer><button class="secondary-action" type="button" (click)="closed.emit()">Cancelar</button><button class="primary-action" type="submit" [disabled]="passwordForm.invalid || saving()">Cambiar contraseña</button></footer>
        </form>
      </section>
    </div>`,
})
export class ChangePasswordDialog {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly closed = output<void>();
  readonly current = signal('');
  readonly next = signal('');
  readonly confirmation = signal('');
  readonly error = signal('');
  readonly saving = signal(false);

  submit(): void {
    if (this.next() !== this.confirmation()) { this.error.set('Las contraseñas nuevas no coinciden.'); return; }
    this.error.set('');
    this.saving.set(true);
    this.auth.changePassword(this.current(), this.next()).subscribe({
      next: () => this.signInAgain(),
      error: (error: { error?: { detail?: string } }) => {
        this.saving.set(false);
        this.error.set(error.error?.detail ?? 'No se pudo cambiar la contraseña.');
      },
    });
  }

  private signInAgain(): void {
    const toLogin = () => {
      this.auth.error.set(null);
      this.auth.notice.set('Contraseña actualizada. Entra con la nueva.');
      void this.router.navigateByUrl('/login');
    };
    this.auth.logout().subscribe({ next: toLogin, error: toLogin });
  }
}
