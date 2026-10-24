import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';
import { RevealDirective } from '../../shared/reveal.directive';
import { PhotoComponent } from '../../shared/photo.component';

@Component({
  selector: 'app-about',
  standalone: true,
  imports: [CommonModule, LandingTranslatePipe, RevealDirective, PhotoComponent],
  templateUrl: './about.component.html'
})
export class AboutComponent {
  stats = [
    { n: 'ABOUT.S1N', l: 'ABOUT.S1L' },
    { n: 'ABOUT.S2N', l: 'ABOUT.S2L' },
    { n: 'ABOUT.S3N', l: 'ABOUT.S3L' },
    { n: 'ABOUT.S4N', l: 'ABOUT.S4L' }
  ];
}
