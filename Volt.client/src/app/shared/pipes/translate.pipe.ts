import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocaleService } from '../../core/services/locale.service';

/**
 * `{{ 'orders.title' | t }}`. Impure so it follows a language switch, but memoized:
 * it re-translates only when the key, the params object or the language changes,
 * so change detection costs one comparison per binding instead of a lookup.
 */
@Pipe({
  name: 't',
  standalone: true,
  pure: false
})
export class TranslatePipe implements PipeTransform {
  private readonly localeService = inject(LocaleService);

  private lastKey?: string;
  private lastParams?: Record<string, string | number>;
  private lastLocale?: string;
  private lastValue = '';

  transform(key: string, params?: Record<string, string | number>): string {
    const locale = this.localeService.locale();
    if (key === this.lastKey && params === this.lastParams && locale === this.lastLocale) {
      return this.lastValue;
    }
    this.lastKey = key;
    this.lastParams = params;
    this.lastLocale = locale;
    this.lastValue = this.localeService.translate(key, params);
    return this.lastValue;
  }
}
