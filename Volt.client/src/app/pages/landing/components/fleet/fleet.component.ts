import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';
import { RevealDirective } from '../../shared/reveal.directive';

@Component({
  selector: 'app-fleet',
  standalone: true,
  imports: [CommonModule, LandingTranslatePipe, RevealDirective],
  templateUrl: './fleet.component.html'
})
export class FleetComponent {
  types = [
    { name: 'FLEET.V1', file: 'electric-scooter.jpg' },
    { name: 'FLEET.V2', file: 'electric-bike.jpg' },
    { name: 'FLEET.V3', file: 'petrol-scooter.jpg' }
  ];
}
