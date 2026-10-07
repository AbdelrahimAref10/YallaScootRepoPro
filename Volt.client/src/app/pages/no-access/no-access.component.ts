import { Component } from '@angular/core';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

/** Shown inside a panel when the user's sub-role grants no page at all. */
@Component({
  selector: 'app-no-access',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    <section class="no-access">
      <h2>{{ 'access.noAccessTitle' | t }}</h2>
      <p>{{ 'access.noAccessBody' | t }}</p>
    </section>
  `,
  styles: [`
    .no-access { max-width: 520px; margin: 64px auto; text-align: center; }
    .no-access h2 { margin-bottom: 8px; }
    .no-access p { opacity: .75; }
  `]
})
export class NoAccessComponent {}
