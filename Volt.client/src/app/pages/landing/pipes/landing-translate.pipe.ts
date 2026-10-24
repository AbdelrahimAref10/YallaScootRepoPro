import { Pipe, PipeTransform } from '@angular/core';
import { LandingLanguageService } from '../services/landing-language.service';

@Pipe({
  name: 'translate',
  standalone: true,
  pure: false
})
export class LandingTranslatePipe implements PipeTransform {
  constructor(private langService: LandingLanguageService) {}

  transform(key: string): string {
    return this.langService.instant(key);
  }
}
