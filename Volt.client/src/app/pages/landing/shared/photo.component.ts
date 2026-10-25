import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-photo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <img
      *ngIf="!failed"
      class="absolute inset-0 h-full w-full"
      [class.object-cover]="fit !== 'contain'"
      [class.object-contain]="fit === 'contain'"
      [class.p-6]="fit === 'contain'"
      [src]="src"
      [alt]="alt"
      [attr.loading]="eager ? 'eager' : 'lazy'"
      [attr.fetchpriority]="eager ? 'high' : null"
      decoding="async"
      (error)="failed = true"
    />
    <div class="absolute inset-0" [class.invisible]="!failed">
      <ng-content></ng-content>
    </div>
  `,
  styles: [':host { display: block; position: absolute; inset: 0; }']
})
export class PhotoComponent {
  @Input() file = '';
  @Input() alt = '';
  @Input() fit: 'cover' | 'contain' = 'cover';
  /** Above-the-fold photo (the hero): load at once instead of lazily. */
  @Input() eager = false;
  failed = false;

  get src(): string {
    return `assets/images/${this.file}`;
  }
}
