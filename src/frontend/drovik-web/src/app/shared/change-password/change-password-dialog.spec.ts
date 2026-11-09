import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { ChangePasswordDialog } from './change-password-dialog';

describe('ChangePasswordDialog', () => {
  const auth = { changePassword: vi.fn(), logout: vi.fn(() => of(undefined)), error: signal<string | null>(null), notice: signal<string | null>(null) };

  beforeEach(() => {
    auth.changePassword.mockReset();
    TestBed.configureTestingModule({ imports: [ChangePasswordDialog], providers: [provideRouter([]), { provide: AuthService, useValue: auth }] });
  });

  function filled(next = 'Brand-New-Pass-2026', confirmation = next) {
    const dialog = TestBed.createComponent(ChangePasswordDialog).componentInstance;
    dialog.current.set('Strong-Pass-2026');
    dialog.next.set(next);
    dialog.confirmation.set(confirmation);
    return dialog;
  }

  it('does not call the server when the new passwords differ', () => {
    const dialog = filled('Brand-New-Pass-2026', 'Something-Else-2026');
    dialog.submit();
    expect(dialog.error()).toContain('no coinciden');
    expect(auth.changePassword).not.toHaveBeenCalled();
  });

  it('shows why the server rejected the change', () => {
    auth.changePassword.mockReturnValue(throwError(() => ({ error: { detail: 'La contraseña actual no es correcta.' } })));
    const dialog = filled();
    dialog.submit();
    expect(dialog.error()).toContain('actual');
    expect(dialog.saving()).toBe(false);
  });

  it('sends the user to sign in again after a change', () => {
    auth.changePassword.mockReturnValue(of(undefined));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    filled().submit();
    expect(auth.logout).toHaveBeenCalled();
    expect(auth.notice()).toContain('actualizada');
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
