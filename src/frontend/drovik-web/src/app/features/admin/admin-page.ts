import { DialogFocus } from '../../shared/dialog-focus/dialog-focus';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminDataService, ManagedUser, NewUser, UserRole } from '../../core/data/admin-data.service';
import { DEMO_PRESETS, DemoPresetId, DemoSeedService } from '../../core/data/demo-seed.service';
import { AuthService } from '../../core/auth/auth.service';

const ROLE_LABELS: Record<UserRole, string> = { Administrator: 'Administrador', Operator: 'Operador' };
type ApiError = { error?: { detail?: string; title?: string } };

@Component({
  selector: 'app-admin-page',
  imports: [FormsModule, RouterLink, DialogFocus],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.scss',
  standalone: true,
  host: { '(document:keydown.escape)': 'editorOpen.set(false); resetTarget.set(null)' },
})
export class AdminPage {
  readonly data = inject(AdminDataService);
  readonly seed = inject(DemoSeedService);
  private readonly auth = inject(AuthService);
  readonly presets = DEMO_PRESETS;
  readonly presetId = signal<DemoPresetId>('complete');
  readonly preset = computed(() => this.presets.find((item) => item.id === this.presetId())!);
  readonly confirming = signal(false);
  readonly editorOpen = signal(false);
  readonly formError = signal('');
  readonly rowError = signal('');
  readonly rowNotice = signal('');
  readonly resetTarget = signal<ManagedUser | null>(null);
  readonly newPassword = signal('');
  readonly draft = signal<NewUser>({ email: '', password: '', role: 'Operator' });

  constructor() { this.data.load(); }

  isSelf(user: ManagedUser): boolean { return user.id === this.auth.user()?.id; }
  roleLabels(user: ManagedUser): string { return user.roles.map((role) => ROLE_LABELS[role]).join(', '); }
  setDraft<K extends keyof NewUser>(key: K, value: NewUser[K]): void { this.draft.update((draft) => ({ ...draft, [key]: value })); }

  openCreate(): void {
    this.draft.set({ email: '', password: '', role: 'Operator' });
    this.formError.set('');
    this.editorOpen.set(true);
  }

  create(): void {
    this.data.create(this.draft()).subscribe({
      next: () => this.editorOpen.set(false),
      error: (error: ApiError) => this.formError.set(error.error?.detail ?? 'No se pudo crear el usuario.'),
    });
  }

  toggle(user: ManagedUser): void {
    this.rowError.set('');
    this.data.setActive(user.id, !user.isActive).subscribe({
      error: (error: ApiError) => this.rowError.set(error.error?.detail ?? 'No se pudo cambiar el estado del usuario.'),
    });
  }

  changeRole(user: ManagedUser, role: UserRole): void {
    this.rowError.set('');
    this.rowNotice.set('');
    this.data.setRole(user.id, role).subscribe({
      next: () => this.rowNotice.set(`${user.email} ahora es ${ROLE_LABELS[role].toLocaleLowerCase('es')}.`),
      error: (error: ApiError) => {
        this.rowError.set(error.error?.detail ?? 'No se pudo cambiar el rol.');
        this.data.load();
      },
    });
  }

  openReset(user: ManagedUser): void {
    this.newPassword.set('');
    this.formError.set('');
    this.resetTarget.set(user);
  }

  resetPassword(): void {
    const user = this.resetTarget();
    if (!user) return;
    this.data.resetPassword(user.id, this.newPassword()).subscribe({
      next: () => {
        this.resetTarget.set(null);
        this.rowError.set('');
        this.rowNotice.set(`Contraseña restablecida para ${user.email}. Sus sesiones abiertas se cerraron.`);
      },
      error: (error: ApiError) => this.formError.set(error.error?.detail ?? 'No se pudo restablecer la contraseña.'),
    });
  }

  restart(): void {
    this.confirming.set(false);
    void this.seed.restart(this.preset());
  }
}
