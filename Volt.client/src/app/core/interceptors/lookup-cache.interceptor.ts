import { Injectable, inject } from '@angular/core';
import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest, HttpResponse } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LookupService } from '../services/lookup.service';

/** After any successful write, cached dropdown lists may be stale: clear them. */
@Injectable()
export class LookupCacheInterceptor implements HttpInterceptor {
  private readonly lookups = inject(LookupService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (req.method === 'GET') {
      return next.handle(req);
    }
    return next.handle(req).pipe(
      tap(event => {
        if (event instanceof HttpResponse && event.ok) this.lookups.clear();
      })
    );
  }
}
