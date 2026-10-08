import { Injectable } from '@angular/core';

const SOUND_URL = 'assets/sounds/notification.wav';
const UNLOCK_EVENTS = ['pointerdown', 'keydown', 'touchstart'] as const;

/**
 * Plays the notification sound for realtime admin / merchant notifications.
 *
 * Browsers block audio until the user has interacted with the page, unless the site has a high
 * media-engagement score (why it rang on localhost but not on the server). So the sound is decoded
 * once into a Web Audio buffer and the AudioContext is resumed on the first click / key press
 * anywhere on the page; after that every notification can ring without any further click.
 */
@Injectable({ providedIn: 'root' })
export class NotificationSoundService {
  private readonly context: AudioContext | null = this.createContext();
  private buffer: AudioBuffer | null = null;
  private loading: Promise<void> | null = null;

  constructor() {
    if (!this.context) return;
    const unlock = () => {
      void this.context!.resume().then(() => {
        if (this.context!.state === 'running') {
          UNLOCK_EVENTS.forEach(e => document.removeEventListener(e, unlock, true));
        }
      });
      void this.load();
    };
    UNLOCK_EVENTS.forEach(e => document.addEventListener(e, unlock, { capture: true, passive: true }));
    void this.load();
  }

  play(): void {
    if (this.context && this.buffer && this.context.state === 'running') {
      const source = this.context.createBufferSource();
      const gain = this.context.createGain();
      gain.gain.value = 0.9;
      source.buffer = this.buffer;
      source.connect(gain).connect(this.context.destination);
      source.start();
      return;
    }
    // Not unlocked yet (or no Web Audio): the browser may still allow a plain element.
    try {
      const audio = new Audio(SOUND_URL);
      audio.volume = 0.9;
      audio.play().catch(error => console.warn('Notification sound blocked until the page is clicked:', error));
    } catch (error) {
      console.warn('Error creating audio element:', error);
    }
  }

  private load(): Promise<void> {
    if (!this.context || this.buffer) return Promise.resolve();
    this.loading ??= fetch(SOUND_URL)
      .then(res => res.arrayBuffer())
      .then(data => this.context!.decodeAudioData(data))
      .then(buffer => { this.buffer = buffer; })
      .catch(error => {
        this.loading = null;
        console.warn('Could not load notification sound:', error);
      });
    return this.loading;
  }

  private createContext(): AudioContext | null {
    const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    try {
      return Ctor ? new Ctor() : null;
    } catch {
      return null;
    }
  }
}
