import { Injectable, Injector } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../services/auth.service';
import { LocaleService } from '../services/locale.service';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  constructor(private injector: Injector) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        // Check if error is 401 Unauthorized
        if (error.status === 401) {
          // Don't try to refresh token for auth endpoints
          const isAuthEndpoint = req.url.toLowerCase().includes('/api/auth/');

          if (isAuthEndpoint) {
            return throwError(() => error);
          }

          return this.handle401Error(req, next);
        }

        // Forbidden: the sub-role lost this permission; tell the user and resync menus / guards.
        if (error.status === 403) {
          this.injector.get(ToastrService).error(this.injector.get(LocaleService).translate('common.noPermission'));
          this.injector.get(AuthService).refreshAccess(true);
        }

        return throwError(() => error);
      })
    );
  }

  private handle401Error(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    // Use Injector to lazily get AuthService to avoid circular dependency
    const authService = this.injector.get(AuthService);
    const refreshToken = authService.getRefreshToken();

    if (refreshToken) {
      return authService.refreshToken().pipe(
        switchMap((newToken: string) => {
          // Clone the request with the new token
          const clonedRequest = request.clone({
            setHeaders: {
              Authorization: `Bearer ${newToken}`
            }
          });
          return next.handle(clonedRequest);
        }),
        catchError((err) => {
          // If refresh fails, logout will be handled by AuthService
          return throwError(() => err);
        })
      );
    } else {
      authService.logout();
      return throwError(() => new Error('No refresh token available'));
    }
  }
}

