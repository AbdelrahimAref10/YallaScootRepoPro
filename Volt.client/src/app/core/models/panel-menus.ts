import { Permissions } from './permissions';

export interface DashboardMenuItem {
  labelKey: string;
  route: string;
  icon: string;
  /** Optional section heading (i18n key); a new heading starts where the key changes. */
  groupKey?: string;
  /** Hidden unless the user's sub-role grants this permission. */
  permission?: string;
}

export const ADMIN_MENU_ITEMS: DashboardMenuItem[] = [
  { labelKey: 'nav.dashboard', route: '/main/dashboard', icon: 'dashboard', groupKey: 'nav.groupOverview', permission: Permissions.Admin.Dashboard.View },
  { labelKey: 'nav.orders', route: '/main/orders', icon: 'orders', groupKey: 'nav.groupOverview', permission: Permissions.Admin.Orders.View },
  { labelKey: 'nav.shifts', route: '/main/shifts', icon: 'shifts', groupKey: 'nav.groupOverview', permission: Permissions.Admin.Shifts.View },
  { labelKey: 'nav.merchants', route: '/main/merchants', icon: 'merchants', groupKey: 'nav.groupPeople', permission: Permissions.Admin.Merchants.View },
  { labelKey: 'nav.deliveries', route: '/main/deliveries', icon: 'riders', groupKey: 'nav.groupPeople', permission: Permissions.Admin.Deliveries.View },
  { labelKey: 'nav.customers', route: '/main/customers', icon: 'users', groupKey: 'nav.groupPeople', permission: Permissions.Admin.Customers.View },
  { labelKey: 'nav.vehicles', route: '/main/vehicles', icon: 'vehicles', groupKey: 'nav.groupCatalog', permission: Permissions.Admin.Vehicles.View },
  { labelKey: 'nav.categories', route: '/main/categories', icon: 'categories', groupKey: 'nav.groupCatalog', permission: Permissions.Admin.Categories.View },
  { labelKey: 'nav.subcategories', route: '/main/subcategories', icon: 'subcategories', groupKey: 'nav.groupCatalog', permission: Permissions.Admin.SubCategories.View },
  { labelKey: 'nav.cities', route: '/main/cities', icon: 'cities', groupKey: 'nav.groupCatalog', permission: Permissions.Admin.Cities.View },
  { labelKey: 'nav.settlements', route: '/main/settlements', icon: 'money', groupKey: 'nav.groupFinance', permission: Permissions.Admin.Settlements.View },
  { labelKey: 'nav.journals', route: '/main/journals', icon: 'ledger', groupKey: 'nav.groupFinance', permission: Permissions.Admin.Journals.View },
  { labelKey: 'nav.reports', route: '/main/reports', icon: 'reports', groupKey: 'nav.groupFinance', permission: Permissions.Admin.Reports.View },
  { labelKey: 'nav.systemUsers', route: '/main/users', icon: 'system-users', groupKey: 'nav.groupSystem', permission: Permissions.Admin.SystemUsers.View },
  { labelKey: 'nav.roles', route: '/main/roles', icon: 'roles', groupKey: 'nav.groupSystem', permission: Permissions.Admin.Roles.View },
  { labelKey: 'nav.support', route: '/main/support', icon: 'support', groupKey: 'nav.groupSystem', permission: Permissions.Admin.Support.View }
];

export const MERCHANT_MENU_ITEMS: DashboardMenuItem[] = [
  { labelKey: 'merchant.nav.home', route: '/merchant/home', icon: 'dashboard', permission: Permissions.Merchant.Home.View },
  { labelKey: 'merchant.nav.orders', route: '/merchant/orders', icon: 'orders', permission: Permissions.Merchant.Orders.View },
  { labelKey: 'merchant.nav.vehicles', route: '/merchant/vehicles', icon: 'vehicles', permission: Permissions.Merchant.Vehicles.View },
  { labelKey: 'merchant.nav.payments', route: '/merchant/payments', icon: 'money', permission: Permissions.Merchant.Payments.View },
  { labelKey: 'merchant.nav.staff', route: '/merchant/staff', icon: 'system-users', permission: Permissions.Merchant.Staff.View }
];
