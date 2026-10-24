import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';
import { RevealDirective } from '../../shared/reveal.directive';
import { PhotoComponent } from '../../shared/photo.component';

@Component({
  selector: 'app-blog',
  standalone: true,
  imports: [CommonModule, LandingTranslatePipe, RevealDirective, PhotoComponent],
  templateUrl: './blog.component.html'
})
export class BlogComponent {
  steps = [
    { n: 'RIDE.N1', title: 'RIDE.T1', body: 'RIDE.D1', file: 'blog-1.jpg' },
    { n: 'RIDE.N2', title: 'RIDE.T2', body: 'RIDE.D2', file: 'blog-2.jpg' },
    { n: 'RIDE.N3', title: 'RIDE.T3', body: 'RIDE.D3', file: 'blog-3.jpg' }
  ];
}
