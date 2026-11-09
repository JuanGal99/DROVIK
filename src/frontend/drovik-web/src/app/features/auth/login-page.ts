import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { BrandMark } from '../../shared/brand-mark/brand-mark';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule, BrandMark],
  standalone: true,
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss',
})
export class LoginPage {
  readonly auth = inject(AuthService);
  readonly email = signal('');
  readonly password = signal('');
  readonly formError = signal('');
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  submit(): void {
    this.formError.set('');
    this.auth.login(this.email().trim(), this.password()).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/overview';
        void this.router.navigateByUrl(returnUrl.startsWith('/') ? returnUrl : '/overview');
      },
      error: () => this.formError.set(this.auth.error() ?? 'No se pudo iniciar sesión.'),
    });
  }
}
