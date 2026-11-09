import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { BrandMark } from '../../shared/brand-mark/brand-mark';
import { NetworkMap, NetworkPoint, NetworkRoute } from '../../shared/network-map/network-map';

// The sign-in screen has no session yet, so it draws the shape of the network without its names.
const POINTS: [number, number][] = [[4.6486, -74.0628], [4.7209, -74.0301], [4.7056, -74.112], [4.628, -74.153], [4.5981, -74.0758], [4.741, -74.084], [4.637, -74.079], [4.576, -74.133]];

@Component({
  selector: 'app-login-page',
  imports: [FormsModule, BrandMark, NetworkMap],
  standalone: true,
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss',
})
export class LoginPage {
  readonly auth = inject(AuthService);
  readonly network: NetworkPoint[] = POINTS.map(([latitude, longitude], index) => ({ id: `${index}`, name: '', latitude, longitude }));
  readonly routes: NetworkRoute[] = [['2', '0', 'moving'], ['3', '6', 'moving'], ['5', '1', 'waiting'], ['7', '4', 'moving'], ['0', '4', 'waiting']]
    .map(([originId, destinationId, tone], index) => ({ id: `${index}`, originId, destinationId, tone: tone as NetworkRoute['tone'] }));
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
