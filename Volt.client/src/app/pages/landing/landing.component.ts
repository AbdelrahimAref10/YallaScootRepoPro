import { Component, ViewEncapsulation } from '@angular/core';
import { NavbarComponent } from './components/navbar/navbar.component';
import { HeroComponent } from './components/hero/hero.component';
import { AboutComponent } from './components/about/about.component';
import { FeaturesComponent } from './components/features/features.component';
import { WhyComponent } from './components/why/why.component';
import { FleetComponent } from './components/fleet/fleet.component';
import { CtaComponent } from './components/cta/cta.component';
import { TestimonialsComponent } from './components/testimonials/testimonials.component';
import { FaqComponent } from './components/faq/faq.component';
import { BlogComponent } from './components/blog/blog.component';
import { FooterComponent } from './components/footer/footer.component';
import { LandingLanguageService } from './services/landing-language.service';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [
    NavbarComponent,
    HeroComponent,
    AboutComponent,
    FeaturesComponent,
    WhyComponent,
    FleetComponent,
    CtaComponent,
    TestimonialsComponent,
    FaqComponent,
    BlogComponent,
    FooterComponent
  ],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.css',
  // The landing's base styles (.lp-btn, .reveal, .eyebrow …) are shared by its
  // child components, so they are global but scoped under .yl-landing.
  encapsulation: ViewEncapsulation.None
})
export class LandingComponent {
  constructor(public langService: LandingLanguageService) {}
}
