/** Mirrors Domain.Authorization.Permissions — keep in sync with backend. */
export const Permissions = {
  Admin: {
    Dashboard: { View: 'Admin.Dashboard.View' },
    Orders: { View: 'Admin.Orders.View', Create: 'Admin.Orders.Create', Edit: 'Admin.Orders.Edit' },
    Shifts: { View: 'Admin.Shifts.View', Create: 'Admin.Shifts.Create', Edit: 'Admin.Shifts.Edit', Delete: 'Admin.Shifts.Delete' },
    Merchants: { View: 'Admin.Merchants.View', Create: 'Admin.Merchants.Create', Edit: 'Admin.Merchants.Edit', Delete: 'Admin.Merchants.Delete' },
    Deliveries: { View: 'Admin.Deliveries.View', Create: 'Admin.Deliveries.Create', Edit: 'Admin.Deliveries.Edit', Delete: 'Admin.Deliveries.Delete' },
    Customers: { View: 'Admin.Customers.View', Create: 'Admin.Customers.Create', Edit: 'Admin.Customers.Edit' },
    Vehicles: { View: 'Admin.Vehicles.View', Create: 'Admin.Vehicles.Create', Edit: 'Admin.Vehicles.Edit', Delete: 'Admin.Vehicles.Delete' },
    Categories: { View: 'Admin.Categories.View', Create: 'Admin.Categories.Create', Edit: 'Admin.Categories.Edit', Delete: 'Admin.Categories.Delete' },
    SubCategories: { View: 'Admin.SubCategories.View', Create: 'Admin.SubCategories.Create', Edit: 'Admin.SubCategories.Edit', Delete: 'Admin.SubCategories.Delete' },
    Cities: { View: 'Admin.Cities.View', Create: 'Admin.Cities.Create', Edit: 'Admin.Cities.Edit', Delete: 'Admin.Cities.Delete' },
    Settlements: { View: 'Admin.Settlements.View', Create: 'Admin.Settlements.Create' },
    Journals: { View: 'Admin.Journals.View' },
    Reports: { View: 'Admin.Reports.View' },
    SystemUsers: { View: 'Admin.SystemUsers.View', Create: 'Admin.SystemUsers.Create', Edit: 'Admin.SystemUsers.Edit', Delete: 'Admin.SystemUsers.Delete' },
    Roles: { View: 'Admin.Roles.View', Create: 'Admin.Roles.Create', Edit: 'Admin.Roles.Edit', Delete: 'Admin.Roles.Delete' },
    Support: { View: 'Admin.Support.View', Edit: 'Admin.Support.Edit' }
  },
  Merchant: {
    Home: { View: 'Merchant.Home.View' },
    Orders: { View: 'Merchant.Orders.View', Edit: 'Merchant.Orders.Edit' },
    Vehicles: { View: 'Merchant.Vehicles.View', Create: 'Merchant.Vehicles.Create', Edit: 'Merchant.Vehicles.Edit', Delete: 'Merchant.Vehicles.Delete' },
    Payments: { View: 'Merchant.Payments.View' },
    Staff: { View: 'Merchant.Staff.View', Create: 'Merchant.Staff.Create', Edit: 'Merchant.Staff.Edit', Delete: 'Merchant.Staff.Delete' }
  }
} as const;

type Leaves<T> = T extends string ? T : { [K in keyof T]: Leaves<T[K]> }[keyof T];
export type Permission = Leaves<typeof Permissions>;
