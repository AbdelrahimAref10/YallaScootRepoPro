import { Injectable } from '@angular/core';

import en from '../../../../assets/i18n/landing/en.json';
import ru from '../../../../assets/i18n/landing/ru.json';
import de from '../../../../assets/i18n/landing/de.json';

export type AppLang = 'en' | 'ru' | 'de';

/**
 * Landing-page only languages (en/ru/de). Kept apart from the dashboard's
 * locale (en/ar) so it never touches <html> lang/dir or the dark theme;
 * the landing root applies lang/dir on its own element instead.
 */
@Injectable({
  providedIn: 'root'
})
export class LandingLanguageService {
  currentLang: AppLang = 'en';
  private translations: Record<AppLang, any> = { en, ru, de };

  constructor() {
    let saved: string | null = null;
    try {
      saved = localStorage.getItem('yalla_lang');
    } catch {}
    const allowed: AppLang[] = ['en', 'ru', 'de'];
    this.currentLang = allowed.includes(saved as AppLang) ? (saved as AppLang) : 'en';
  }

  setLanguage(lang: AppLang) {
    this.currentLang = lang;
    try {
      localStorage.setItem('yalla_lang', lang);
    } catch {}
  }

  instant(key: string): string {
    const keys = key.split('.');
    let result = this.translations[this.currentLang];
    for (const k of keys) {
      if (result && result[k]) {
        result = result[k];
      } else {
        return key;
      }
    }
    return typeof result === 'string' ? result : key;
  }
}
