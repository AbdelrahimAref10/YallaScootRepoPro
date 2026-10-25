import { Component, inject } from '@angular/core';
import { Meta } from '@angular/platform-browser';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter } from 'rxjs';
import { SplashService } from './core/services/splash.service';
import { NotificationSoundService } from './core/services/notification-sound.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  standalone: true,
  imports: [RouterModule]
})
export class AppComponent {
  readonly splash = inject(SplashService);
  // Created at startup so the first click anywhere (even "Sign in") unlocks notification audio.
  private readonly notificationSound = inject(NotificationSoundService);

  constructor() {
    // Only the landing page belongs in search results; login and the panels share
    // index.html, so they switch its robots tag to noindex once routed.
    const meta = inject(Meta);
    inject(Router).events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(e => {
      const isLanding = (e as NavigationEnd).urlAfterRedirects.split(/[?#]/)[0] === '/';
      meta.updateTag({ name: 'robots', content: isLanding ? 'index, follow, max-image-preview:large' : 'noindex, nofollow' });
    });
  }
}
