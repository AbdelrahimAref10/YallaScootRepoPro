import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateChildFn, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ADMIN_MENU_ITEMS, DashboardMenuItem, MERCHANT_MENU_ITEMS } from '../models/panel-menus';

/**
 * Child routes declare `data: { permission: '...' }` (or an array: any one grants access).
 * Without the permission the user goes to the first page of the panel they may open,
 * or to the panel's no-access page when there is none.
 */
export const permissionGuard: CanActivateChildFn = (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
  const required = route.data?.['permission'] as string | string[] | undefined;
  if (!required) {
    return true;
  }

  const auth = inject(AuthService);
  const needed = Array.isArray(required) ? required : [required];
  if (auth.hasAnyPermission(needed)) {
    return true;
  }

  return firstAllowedRoute(auth, inject(Router), state.url);
};

function firstAllowedRoute(auth: AuthService, router: Router, url: string): UrlTree {
  const isMerchant = url.startsWith('/merchant');
  const menu: DashboardMenuItem[] = isMerchant ? MERCHANT_MENU_ITEMS : ADMIN_MENU_ITEMS;
  const target = menu.find(item => !item.permission || auth.hasPermission(item.permission));
  // Avoid a redirect loop when the target itself is the page being denied.
  if (target && target.route !== url) {
    return router.createUrlTree([target.route]);
  }
  return router.createUrlTree([isMerchant ? '/merchant/no-access' : '/main/no-access']);
}
