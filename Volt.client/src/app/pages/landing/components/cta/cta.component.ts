import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';
import { PhotoComponent } from '../../shared/photo.component';

@Component({
  selector: 'app-cta',
  standalone: true,
  imports: [CommonModule, LandingTranslatePipe, PhotoComponent],
  templateUrl: './cta.component.html'
})
export class CtaComponent {}
