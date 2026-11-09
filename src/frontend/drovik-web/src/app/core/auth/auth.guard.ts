import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.initialize().pipe(map(() => auth.user()?.roles.includes('Administrator') ? true : router.createUrlTree(['/overview'])));
};

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.initialize().pipe(
    map(() => auth.isAuthenticated ? true : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })),
  );
};
