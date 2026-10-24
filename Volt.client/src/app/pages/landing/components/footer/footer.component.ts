import { Component } from '@angular/core';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [LandingTranslatePipe],
  templateUrl: './footer.component.html'
})
export class FooterComponent {}
