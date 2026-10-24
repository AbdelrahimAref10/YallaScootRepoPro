import { Component } from '@angular/core';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';
import { PhotoComponent } from '../../shared/photo.component';

@Component({
  selector: 'app-hero',
  standalone: true,
  imports: [LandingTranslatePipe, PhotoComponent],
  templateUrl: './hero.component.html'
})
export class HeroComponent {}
