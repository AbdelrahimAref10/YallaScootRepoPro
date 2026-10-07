import { ApplicationRef, Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { versioned } from '../utils/build-version';

export type AppLocale = 'en' | 'ar';

const STORAGE_KEY = 'volt-locale';
const PARAM = /{{\s*(\w+)\s*}}/g;

/** { a: { b: 'x' } } → Map { 'a.b' => 'x' } */
function flatten(tree: Record<string, unknown>, prefix = '', into = new Map<string, string>()): Map<string, string> {
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') into.set(path, value);
    else if (value && typeof value === 'object') flatten(value as Record<string, unknown>, path, into);
  }
  return into;
}
const SUPPORTED: AppLocale[] = ['en', 'ar'];

@Injectable({ providedIn: 'root' })
export class LocaleService {
  /** Flattened "a.b.c" → text, so a lookup is one Map read instead of a walk through the JSON tree. */
  private readonly translations = signal<ReadonlyMap<string, string>>(new Map());
  private readonly englishFallback = signal<ReadonlyMap<string, string>>(new Map());
  private readonly currentLocale = signal<AppLocale>(this.readStoredLocale());

  readonly locale = this.currentLocale.asReadonly();
  readonly isRtl = computed(() => this.currentLocale() === 'ar');
  readonly ready = signal(false);

  readonly locales: { code: AppLocale; label: string; native: string }[] = [
    { code: 'en', label: 'English', native: 'English' },
    { code: 'ar', label: 'Arabic', native: 'العربية' }
  ];

  readonly adminLocales = this.locales;

  constructor(
    private http: HttpClient,
    private appRef: ApplicationRef
  ) {}

  async init(): Promise<void> {
    const en = await firstValueFrom(
      this.http.get<Record<string, unknown>>(versioned('assets/i18n/en.json'))
    );
    this.englishFallback.set(flatten(en));

    await this.loadLocale(this.currentLocale());
    this.applyDocumentLocale(this.currentLocale());
    this.ready.set(true);
  }

  async setLocale(locale: AppLocale): Promise<void> {
    if (!SUPPORTED.includes(locale)) {
      return;
    }
    if (locale === this.currentLocale()) {
      return;
    }
    await this.loadLocale(locale);
    this.currentLocale.set(locale);
    localStorage.setItem(STORAGE_KEY, locale);
    this.applyDocumentLocale(locale);
    this.appRef.tick();
  }

  toggleAdminLocale(): Promise<void> {
    const current = this.currentLocale();
    const next: AppLocale = current === 'ar' ? 'en' : 'ar';
    return this.setLocale(next);
  }

  ensureAdminLocale(): Promise<void> {
    if (SUPPORTED.includes(this.currentLocale())) {
      return Promise.resolve();
    }
    return this.setLocale('en');
  }

  translate(key: string, params?: Record<string, string | number>): string {
    const value = this.translations().get(key) ?? this.englishFallback().get(key);
    if (value == null) {
      return key;
    }
    if (!params) {
      return value;
    }
    return value.replace(PARAM, (match, name: string) => (name in params ? String(params[name]) : match));
  }

  private async loadLocale(locale: AppLocale): Promise<void> {
    if (locale === 'en') {
      this.translations.set(this.englishFallback());
      return;
    }
    try {
      const data = await firstValueFrom(
        this.http.get<Record<string, unknown>>(versioned(`assets/i18n/${locale}.json`))
      );
      this.translations.set(flatten(data));
    } catch {
      this.translations.set(this.englishFallback());
    }
  }

  private applyDocumentLocale(locale: AppLocale): void {
    const html = document.documentElement;
    html.lang = locale;
    html.dir = locale === 'ar' ? 'rtl' : 'ltr';
    for (const code of SUPPORTED) {
      html.classList.toggle(`locale-${code}`, locale === code);
    }
  }

  private readStoredLocale(): AppLocale {
    const stored = localStorage.getItem(STORAGE_KEY);
    return SUPPORTED.includes(stored as AppLocale) ? (stored as AppLocale) : 'en';
  }
}
