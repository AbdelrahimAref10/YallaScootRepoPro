import { Injectable, inject } from '@angular/core';
import { PreloadingStrategy, Route } from '@angular/router';
import { Observable, EMPTY, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

/**
 * Downloads lazy pages in the background, but only the ones the signed-in user may open
 * (routes carry `data.permission`). An admin never fetches merchant pages and vice versa,
 * and nothing is fetched on the login screen. The router re-runs this after every navigation,
 * so pages are picked up right after sign-in.
 */
@Injectable({ providedIn: 'root' })
export class PermissionPreloadingStrategy implements PreloadingStrategy {
  private readonly auth = inject(AuthService);

  preload(route: Route, load: () => Observable<unknown>): Observable<unknown> {
    const permission = route.data?.['permission'] as string | undefined;
    if (!permission || !this.auth.isAuthenticated() || !this.auth.hasPermission(permission)) {
      return EMPTY;
    }
    // Let the current page finish rendering before using the network.
    return timer(1500).pipe(switchMap(() => load()));
  }
}
