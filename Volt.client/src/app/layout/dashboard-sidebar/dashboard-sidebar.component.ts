import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

export interface DashboardMenuItem {
  labelKey: string;
  route: string;
  icon: string;
  /** Optional section heading (i18n key); a new heading starts where the key changes. */
  groupKey?: string;
}

const ADMIN_MENU_ITEMS: DashboardMenuItem[] = [
  { labelKey: 'nav.dashboard', route: '/main/dashboard', icon: 'dashboard', groupKey: 'nav.groupOverview' },
  { labelKey: 'nav.orders', route: '/main/orders', icon: 'orders', groupKey: 'nav.groupOverview' },
  { labelKey: 'nav.shifts', route: '/main/shifts', icon: 'shifts', groupKey: 'nav.groupOverview' },
  { labelKey: 'nav.merchants', route: '/main/merchants', icon: 'merchants', groupKey: 'nav.groupPeople' },
  { labelKey: 'nav.deliveries', route: '/main/deliveries', icon: 'riders', groupKey: 'nav.groupPeople' },
  { labelKey: 'nav.customers', route: '/main/customers', icon: 'users', groupKey: 'nav.groupPeople' },
  { labelKey: 'nav.vehicles', route: '/main/vehicles', icon: 'vehicles', groupKey: 'nav.groupCatalog' },
  { labelKey: 'nav.categories', route: '/main/categories', icon: 'categories', groupKey: 'nav.groupCatalog' },
  { labelKey: 'nav.subcategories', route: '/main/subcategories', icon: 'subcategories', groupKey: 'nav.groupCatalog' },
  { labelKey: 'nav.cities', route: '/main/cities', icon: 'cities', groupKey: 'nav.groupCatalog' },
  { labelKey: 'nav.settlements', route: '/main/settlements', icon: 'money', groupKey: 'nav.groupFinance' },
  { labelKey: 'nav.journals', route: '/main/journals', icon: 'ledger', groupKey: 'nav.groupFinance' },
  { labelKey: 'nav.reports', route: '/main/reports', icon: 'reports', groupKey: 'nav.groupFinance' },
  { labelKey: 'nav.systemUsers', route: '/main/users', icon: 'system-users', groupKey: 'nav.groupSystem' },
  { labelKey: 'nav.roles', route: '/main/roles', icon: 'roles', groupKey: 'nav.groupSystem' },
  { labelKey: 'nav.support', route: '/main/support', icon: 'support', groupKey: 'nav.groupSystem' }
];

@Component({
  selector: 'app-dashboard-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslatePipe],
  templateUrl: './dashboard-sidebar.component.html',
  styleUrl: './dashboard-sidebar.component.css'
})
export class DashboardSidebarComponent {
  @Input() isOpen = true;
  @Input() isMobileOpen = false;
  @Input() menuItems: DashboardMenuItem[] = ADMIN_MENU_ITEMS;
  /** Route that should use exact routerLinkActive matching (home/dashboard). */
  @Input() exactActiveRoute = '/main/dashboard';
  @Input() footerKey = 'app.adminFooter';
  @Output() closeMobile: EventEmitter<void> = new EventEmitter<void>();

  /** True when this item opens a new section (its group differs from the previous item's). */
  startsGroup(index: number): boolean {
    const key = this.menuItems[index]?.groupKey;
    return !!key && key !== this.menuItems[index - 1]?.groupKey;
  }

  onCloseMobile(): void {
    this.closeMobile.emit();
  }

  onNavClick(): void {
    if (this.isMobileOpen) {
      this.closeMobile.emit();
    }
  }
}
