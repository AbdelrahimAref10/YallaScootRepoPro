import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Mirrors ASP.NET Identity's default AllowedUserNameCharacters. */
const USER_NAME_PATTERN = /^[A-Za-z0-9._@+-]*$/;

/** English letters, digits and - . _ @ + only (no spaces, no Arabic or hidden characters). */
export function userNameValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value as string | null;
  return !value || USER_NAME_PATTERN.test(value) ? null : { userNameChars: true };
}
