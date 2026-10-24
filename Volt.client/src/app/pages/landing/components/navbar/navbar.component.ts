import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AppLang, LandingLanguageService } from '../../services/landing-language.service';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, LandingTranslatePipe],
  templateUrl: './navbar.component.html'
})
export class NavbarComponent {
  scrolled = false;
  open = false;
  langOpen = false;
  missingFlags: Record<string, boolean> = {};
  langs: { id: AppLang; code: string; label: string; flag: string }[] = [
    { id: 'en', code: 'EN', label: 'English', flag: 'flag-en.png' },
    { id: 'ru', code: 'RU', label: 'Русский', flag: 'flag-ru.png' },
    { id: 'de', code: 'DE', label: 'Deutsch', flag: 'flag-de.png' }
  ];

  constructor(public langService: LandingLanguageService) {}

  get currentLang() {
    return this.langs.find((lang) => lang.id === this.langService.currentLang) ?? this.langs[0];
  }

  @HostListener('window:scroll')
  onScroll() {
    this.scrolled = window.scrollY > 24;
  }

  @HostListener('document:click')
  closeLangMenu() {
    this.langOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.langOpen = false;
  }

  toggleLang(event: Event) {
    event.stopPropagation();
    this.langOpen = !this.langOpen;
  }

  switchLang(lang: AppLang) {
    this.langService.setLanguage(lang);
    this.langOpen = false;
  }

  onFlagError(file: string) {
    this.missingFlags = { ...this.missingFlags, [file]: true };
  }

  closeMenu() {
    this.open = false;
    this.langOpen = false;
  }
}
