import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';
import { RevealDirective } from '../../shared/reveal.directive';

@Component({
  selector: 'app-features',
  standalone: true,
  imports: [CommonModule, LandingTranslatePipe, RevealDirective],
  templateUrl: './features.component.html'
})
export class FeaturesComponent {
  items = [
    { n: '01', title: 'SERVICES.S1T', body: 'SERVICES.S1D', href: '#ride' },
    { n: '02', title: 'SERVICES.S2T', body: 'SERVICES.S2D', href: '#fleet' },
    { n: '03', title: 'SERVICES.S3T', body: 'SERVICES.S3D', href: '#ride' },
    { n: '04', title: 'SERVICES.S4T', body: 'SERVICES.S4D', href: '#contact' }
  ];
}
