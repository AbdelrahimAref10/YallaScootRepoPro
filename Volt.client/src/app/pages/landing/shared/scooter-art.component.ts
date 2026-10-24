import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-scooter-art',
  standalone: true,
  template: `
    <svg viewBox="0 0 640 400" class="h-full w-full" fill="none" aria-hidden="true">
      <ellipse cx="320" cy="332" rx="190" ry="16" fill="currentColor" opacity="0.12"/>
      <circle cx="188" cy="286" r="62" stroke="currentColor" stroke-width="16"/>
      <circle cx="188" cy="286" r="16" [attr.fill]="onYellow ? '#161616' : '#FFD000'"/>
      <circle cx="456" cy="286" r="62" stroke="currentColor" stroke-width="16"/>
      <circle cx="456" cy="286" r="16" [attr.fill]="onYellow ? '#161616' : '#FFD000'"/>
      <path d="M145 268c48-10 96-20 156-20 78 0 120 10 168 32" stroke="currentColor" stroke-width="18" stroke-linecap="round"/>
      <path d="M408 246L468 112" stroke="currentColor" stroke-width="16" stroke-linecap="round"/>
      <path d="M424 118h108" stroke="currentColor" stroke-width="16" stroke-linecap="round"/>
      <path d="M512 118h28" [attr.stroke]="onYellow ? '#E10600' : '#FFD000'" stroke-width="16" stroke-linecap="round"/>
      <path d="M268 244l36-72h78" [attr.stroke]="onYellow ? '#161616' : '#FFFFFF'" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"/>
    </svg>
  `
})
export class ScooterArtComponent {
  @Input() onYellow = false;
}
