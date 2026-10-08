import { Component, ElementRef, HostListener, Input, ViewChild, computed, forwardRef, inject, signal, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { LocaleService } from '../../../core/services/locale.service';
import { TranslatePipe } from '../../pipes/translate.pipe';
import { COUNTRY_DIAL_CODES, DEFAULT_COUNTRY, PREFERRED_COUNTRIES } from './country-dial-codes';

interface Country {
  iso: string;
  dial: string;
  name: string;
}

/**
 * Country code dropdown + local number. The form value is the country code followed by
 * the local number as typed, digits only without a "+" (Egypt + 010500 → "20010500");
 * empty while no local number is typed.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-phone-input',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './phone-input.component.html',
  styleUrl: './phone-input.component.css',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => PhoneInputComponent), multi: true }]
})
export class PhoneInputComponent implements ControlValueAccessor {
  private readonly cdr = inject(ChangeDetectorRef);
  @Input() inputId = '';
  @Input() placeholder = '';

  @ViewChild('searchInput') searchInput?: ElementRef<HTMLInputElement>;

  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly locale = inject(LocaleService);

  readonly open = signal(false);
  readonly search = signal('');
  readonly iso = signal(DEFAULT_COUNTRY);
  localNumber = '';
  disabled = false;

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  readonly countries = computed<Country[]>(() => {
    const names = this.displayNames(this.locale.locale());
    const list = COUNTRY_DIAL_CODES.map(([iso, dial]) => ({ iso, dial, name: names?.of(iso) ?? iso }));
    const preferred = PREFERRED_COUNTRIES
      .map(iso => list.find(c => c.iso === iso))
      .filter((c): c is Country => !!c);
    const rest = list.filter(c => !PREFERRED_COUNTRIES.includes(c.iso))
      .sort((a, b) => a.name.localeCompare(b.name));
    return [...preferred, ...rest];
  });

  readonly filtered = computed<Country[]>(() => {
    const term = this.search().trim().toLowerCase().replace(/^\+/, '');
    if (!term) return this.countries();
    return this.countries().filter(c =>
      c.name.toLowerCase().includes(term) || c.iso.toLowerCase() === term || c.dial.startsWith(term));
  });

  readonly selected = computed<Country>(() =>
    this.countries().find(c => c.iso === this.iso()) ?? this.countries()[0]);

  writeValue(value: string | null): void {
    this.cdr.markForCheck();
    // Accepts "201001234567" as well as older "+201001234567" values.
    const digits = (value ?? '').replace(/[\s-]/g, '').replace(/^\+/, '');
    // A leading 0 (or nothing) is a local number, not a country code.
    if (!digits || digits.startsWith('0')) {
      this.localNumber = digits;
      return;
    }
    // Longest matching dial code wins (1876 Jamaica before 1).
    const match = [...COUNTRY_DIAL_CODES]
      .filter(([, dial]) => digits.startsWith(dial))
      .sort((a, b) => b[1].length - a[1].length || this.rank(a[0]) - this.rank(b[0]))[0];
    if (match) {
      const sameDial = this.iso() !== match[0] && COUNTRY_DIAL_CODES.some(([iso, d]) => iso === this.iso() && d === match[1]);
      if (!sameDial) this.iso.set(match[0]);
      this.localNumber = digits.slice(match[1].length);
    } else {
      this.localNumber = digits;
    }
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.cdr.markForCheck();
    this.disabled = isDisabled;
    if (isDisabled) this.open.set(false);
  }

  toggle(): void {
    if (this.disabled) return;
    this.open.update(v => !v);
    if (this.open()) {
      this.search.set('');
      setTimeout(() => this.searchInput?.nativeElement.focus());
    }
  }

  choose(country: Country): void {
    this.iso.set(country.iso);
    this.open.set(false);
    this.emit();
  }

  onNumberInput(value: string): void {
    // Digits only.
    this.localNumber = value.replace(/\D/g, '');
    this.emit();
  }

  onBlur(): void {
    this.onTouched();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
      this.onTouched();
    }
  }

  @HostListener('keydown.escape')
  onEscape(): void {
    this.open.set(false);
  }

  private emit(): void {
    // The local number is kept as typed, leading 0 included: Egypt + 010500 → 20010500.
    const local = this.localNumber;
    this.onChange(local ? `${this.selected().dial}${local}` : '');
  }

  /** Shared codes (+1, +7): prefer the more common country. */
  private rank(iso: string): number {
    const i = PREFERRED_COUNTRIES.indexOf(iso);
    return i < 0 ? PREFERRED_COUNTRIES.length : i;
  }

  private displayNames(locale: string): Intl.DisplayNames | null {
    try {
      return new Intl.DisplayNames([locale], { type: 'region' });
    } catch {
      return null;
    }
  }
}
