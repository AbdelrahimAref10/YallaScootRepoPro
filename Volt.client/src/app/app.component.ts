import { Component, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SplashService } from './core/services/splash.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  standalone: true,
  imports: [RouterModule]
})
export class AppComponent {
  readonly splash = inject(SplashService);
}
