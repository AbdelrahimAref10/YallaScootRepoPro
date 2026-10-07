import { Directive, Input, TemplateRef, ViewContainerRef, effect, signal } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

/**
 * Renders its element only when the user's sub-role grants the permission
 * (or any of the permissions when given an array). Updates live when permissions change.
 *
 *   <button *appHasPermission="perms.Admin.Orders.Create">New order</button>
 */
@Directive({
  selector: '[appHasPermission]',
  standalone: true
})
export class HasPermissionDirective {
  private readonly required = signal<readonly string[]>([]);
  private rendered = false;

  @Input({ required: true })
  set appHasPermission(value: string | readonly string[]) {
    this.required.set(typeof value === 'string' ? [value] : value);
  }

  constructor(templateRef: TemplateRef<unknown>, viewContainer: ViewContainerRef, authService: AuthService) {
    effect(() => {
      const allowed = authService.hasAnyPermission(this.required());
      if (allowed && !this.rendered) {
        viewContainer.createEmbeddedView(templateRef);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        viewContainer.clear();
        this.rendered = false;
      }
    });
  }
}
