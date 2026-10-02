import { Injectable, signal } from '@angular/core';

const DURATION_MS = 3000;

@Injectable({ providedIn: 'root' })
export class SplashService {
  readonly visible = signal(true);
  private hideTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.scheduleHide();
  }

  /** Full reload already starts visible. Call again after login. */
  play(): void {
    this.visible.set(true);
    this.scheduleHide();
  }

  private scheduleHide(): void {
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
    this.hideTimer = setTimeout(() => {
      this.visible.set(false);
      this.hideTimer = null;
    }, DURATION_MS);
  }
}
