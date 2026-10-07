import { Routes, provideRouter } from '@angular/router';
import { DashboardLayoutComponent } from './layout/dashboard-layout/dashboard-layout.component';
import { MerchantLayoutComponent } from './layout/merchant-layout/merchant-layout.component';
import { AdminLoginComponent } from './pages/admin-login/admin-login.component';
import { adminEntryGuard } from './core/guards/admin-entry.guard';
import { loginGuestGuard, merchantGuard, superAdminGuard } from './core/guards/role.guard';
import { permissionGuard } from './core/guards/permission.guard';
import { Permissions } from './core/models/permissions';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: '/login' },
  { path: 'admin', canActivate: [adminEntryGuard], children: [] },
  { path: 'admin/login', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: AdminLoginComponent, canActivate: [loginGuestGuard] },
  {
    path: 'main',
    component: DashboardLayoutComponent,
    canActivate: [superAdminGuard],
    canActivateChild: [permissionGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        data: { permission: Permissions.Admin.Dashboard.View },
        loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'categories',
        data: { permission: Permissions.Admin.Categories.View },
        loadComponent: () => import('./pages/categories/categories.component').then(m => m.CategoriesComponent)
      },
      {
        path: 'categories/new',
        data: { permission: Permissions.Admin.Categories.Create },
        loadComponent: () => import('./pages/categories/category-form/category-form.component').then(m => m.CategoryFormComponent)
      },
      {
        path: 'categories/:id/edit',
        data: { permission: Permissions.Admin.Categories.Edit },
        loadComponent: () => import('./pages/categories/category-form/category-form.component').then(m => m.CategoryFormComponent)
      },
      {
        path: 'subcategories',
        data: { permission: Permissions.Admin.SubCategories.View },
        loadComponent: () => import('./pages/subcategories/subcategories.component').then(m => m.SubCategoriesComponent)
      },
      {
        path: 'subcategories/new',
        data: { permission: Permissions.Admin.SubCategories.Create },
        loadComponent: () => import('./pages/subcategories/subcategory-form/subcategory-form.component').then(m => m.SubCategoryFormComponent)
      },
      {
        path: 'subcategories/:id/edit',
        data: { permission: Permissions.Admin.SubCategories.Edit },
        loadComponent: () => import('./pages/subcategories/subcategory-form/subcategory-form.component').then(m => m.SubCategoryFormComponent)
      },
      {
        path: 'vehicles',
        data: { permission: Permissions.Admin.Vehicles.View },
        loadComponent: () => import('./pages/vehicles/vehicles.component').then(m => m.VehiclesComponent)
      },
      {
        path: 'vehicles/new',
        data: { permission: Permissions.Admin.Vehicles.Create },
        loadComponent: () => import('./pages/vehicles/vehicle-form/vehicle-form.component').then(m => m.VehicleFormComponent)
      },
      {
        path: 'vehicles/:id/edit',
        data: { permission: Permissions.Admin.Vehicles.Edit },
        loadComponent: () => import('./pages/vehicles/vehicle-form/vehicle-form.component').then(m => m.VehicleFormComponent)
      },
      {
        path: 'customers',
        data: { permission: Permissions.Admin.Customers.View },
        loadComponent: () => import('./pages/customers/customers.component').then(m => m.CustomersComponent)
      },
      {
        path: 'customers/new',
        data: { permission: Permissions.Admin.Customers.Create },
        loadComponent: () => import('./pages/customers/customer-form/customer-form.component').then(m => m.CustomerFormComponent)
      },
      {
        path: 'customers/:id',
        data: { permission: Permissions.Admin.Customers.View },
        loadComponent: () => import('./pages/customers/customer-detail/customer-detail.component').then(m => m.CustomerDetailComponent)
      },
      {
        path: 'users',
        data: { permission: Permissions.Admin.SystemUsers.View },
        loadComponent: () => import('./pages/users/users.component').then(m => m.UsersComponent)
      },
      {
        path: 'users/:id',
        data: { permission: Permissions.Admin.SystemUsers.View },
        loadComponent: () => import('./pages/users/user-detail/user-detail.component').then(m => m.UserDetailComponent)
      },
      {
        path: 'roles',
        data: { permission: Permissions.Admin.Roles.View },
        loadComponent: () => import('./pages/roles/roles.component').then(m => m.RolesComponent)
      },
      {
        path: 'cities',
        data: { permission: Permissions.Admin.Cities.View },
        loadComponent: () => import('./pages/cities/cities.component').then(m => m.CitiesComponent)
      },
      {
        path: 'cities/new',
        data: { permission: Permissions.Admin.Cities.Create },
        loadComponent: () => import('./pages/cities/city-form/city-form.component').then(m => m.CityFormComponent)
      },
      {
        path: 'cities/:id/edit',
        data: { permission: Permissions.Admin.Cities.Edit },
        loadComponent: () => import('./pages/cities/city-form/city-form.component').then(m => m.CityFormComponent)
      },
      {
        path: 'cities/:id/rates',
        data: { permission: Permissions.Admin.Cities.Edit },
        loadComponent: () => import('./pages/cities/city-zone-rates/city-zone-rates.component').then(m => m.CityZoneRatesComponent)
      },
      {
        path: 'orders',
        data: { permission: Permissions.Admin.Orders.View },
        loadComponent: () => import('./pages/orders/orders.component').then(m => m.OrdersComponent)
      },
      {
        path: 'orders/new',
        data: { permission: Permissions.Admin.Orders.Create },
        loadComponent: () => import('./pages/orders/order-form/order-form.component').then(m => m.OrderFormComponent)
      },
      {
        path: 'orders/:id/edit',
        data: { permission: Permissions.Admin.Orders.Edit },
        loadComponent: () => import('./pages/orders/order-form/order-form.component').then(m => m.OrderFormComponent)
      },
      {
        path: 'orders/:id',
        data: { permission: Permissions.Admin.Orders.View },
        loadComponent: () => import('./pages/orders/order-detail/order-detail.component').then(m => m.OrderDetailComponent)
      },
      {
        path: 'settlements',
        data: { permission: Permissions.Admin.Settlements.View },
        loadComponent: () => import('./pages/settlements/settlements.component').then(m => m.SettlementsComponent)
      },
      {
        path: 'journals',
        data: { permission: Permissions.Admin.Journals.View },
        loadComponent: () => import('./pages/journals/journals.component').then(m => m.JournalsComponent)
      },
      {
        path: 'merchants',
        data: { permission: Permissions.Admin.Merchants.View },
        loadComponent: () => import('./pages/merchants/merchants.component').then(m => m.MerchantsComponent)
      },
      {
        path: 'merchants/new',
        data: { permission: Permissions.Admin.Merchants.Create },
        loadComponent: () => import('./pages/merchants/merchant-form/merchant-form.component').then(m => m.MerchantFormComponent)
      },
      {
        path: 'merchants/:id/edit',
        data: { permission: Permissions.Admin.Merchants.Edit },
        loadComponent: () => import('./pages/merchants/merchant-form/merchant-form.component').then(m => m.MerchantFormComponent)
      },
      {
        path: 'deliveries',
        data: { permission: Permissions.Admin.Deliveries.View },
        loadComponent: () => import('./pages/deliveries/deliveries.component').then(m => m.DeliveriesComponent)
      },
      {
        path: 'deliveries/new',
        data: { permission: Permissions.Admin.Deliveries.Create },
        loadComponent: () => import('./pages/deliveries/delivery-form/delivery-form.component').then(m => m.DeliveryFormComponent)
      },
      {
        path: 'deliveries/:id/edit',
        data: { permission: Permissions.Admin.Deliveries.Edit },
        loadComponent: () => import('./pages/deliveries/delivery-form/delivery-form.component').then(m => m.DeliveryFormComponent)
      },
      {
        path: 'shifts',
        data: { permission: Permissions.Admin.Shifts.View },
        loadComponent: () => import('./pages/shifts/shifts.component').then(m => m.ShiftsComponent)
      },
      {
        path: 'shifts/new',
        data: { permission: Permissions.Admin.Shifts.Create },
        loadComponent: () => import('./pages/shifts/shift-form/shift-form.component').then(m => m.ShiftFormComponent)
      },
      {
        path: 'shifts/:id/edit',
        data: { permission: Permissions.Admin.Shifts.Edit },
        loadComponent: () => import('./pages/shifts/shift-form/shift-form.component').then(m => m.ShiftFormComponent)
      },
      {
        path: 'reports',
        data: { permission: Permissions.Admin.Reports.View, scope: 'admin' },
        loadComponent: () => import('./pages/reports/reports.component').then(m => m.ReportsComponent)
      },
      {
        path: 'reports/:key',
        data: { permission: Permissions.Admin.Reports.View, scope: 'admin' },
        loadComponent: () => import('./pages/reports/report-viewer/report-viewer.component').then(m => m.ReportViewerComponent)
      },
      {
        path: 'support',
        data: { permission: Permissions.Admin.Support.View },
        loadComponent: () => import('./pages/support/support.component').then(m => m.SupportComponent)
      },
      {
        path: 'profile',
        loadComponent: () => import('./pages/profile/profile.component').then(m => m.ProfileComponent)
      },
      {
        path: 'no-access',
        loadComponent: () => import('./pages/no-access/no-access.component').then(m => m.NoAccessComponent)
      }
    ]
  },
  {
    path: 'merchant',
    component: MerchantLayoutComponent,
    canActivate: [merchantGuard],
    canActivateChild: [permissionGuard],
    children: [
      { path: '', redirectTo: 'home', pathMatch: 'full' },
      {
        path: 'home',
        data: { permission: Permissions.Merchant.Home.View },
        loadComponent: () =>
          import('./pages/merchant/merchant-home.component').then(m => m.MerchantHomeComponent)
      },
      {
        path: 'orders',
        data: { permission: Permissions.Merchant.Orders.View },
        loadComponent: () =>
          import('./pages/merchant/merchant-orders/merchant-orders.component').then(m => m.MerchantOrdersComponent)
      },
      {
        path: 'orders/:id',
        data: { permission: Permissions.Merchant.Orders.View },
        loadComponent: () =>
          import('./pages/merchant/merchant-order-detail/merchant-order-detail.component').then(
            m => m.MerchantOrderDetailComponent
          )
      },
      {
        path: 'payments',
        data: { permission: Permissions.Merchant.Payments.View },
        loadComponent: () =>
          import('./pages/merchant/merchant-payments/merchant-payments.component').then(
            m => m.MerchantPaymentsComponent
          )
      },
      {
        path: 'vehicles',
        data: { permission: Permissions.Merchant.Vehicles.View },
        loadComponent: () =>
          import('./pages/merchant/merchant-vehicles/merchant-vehicles.component').then(
            m => m.MerchantVehiclesComponent
          )
      },
      {
        path: 'vehicles/new',
        data: { permission: Permissions.Merchant.Vehicles.Create },
        loadComponent: () =>
          import('./pages/merchant/merchant-vehicles/merchant-vehicle-form/merchant-vehicle-form.component').then(
            m => m.MerchantVehicleFormComponent
          )
      },
      {
        path: 'vehicles/:id/edit',
        data: { permission: Permissions.Merchant.Vehicles.Edit },
        loadComponent: () =>
          import('./pages/merchant/merchant-vehicles/merchant-vehicle-form/merchant-vehicle-form.component').then(
            m => m.MerchantVehicleFormComponent
          )
      },
      {
        path: 'reports',
        data: { permission: Permissions.Merchant.Reports.View, scope: 'merchant' },
        loadComponent: () => import('./pages/reports/reports.component').then(m => m.ReportsComponent)
      },
      {
        path: 'reports/:key',
        data: { permission: Permissions.Merchant.Reports.View, scope: 'merchant' },
        loadComponent: () =>
          import('./pages/reports/report-viewer/report-viewer.component').then(m => m.ReportViewerComponent)
      },
      {
        path: 'staff',
        data: { permission: Permissions.Merchant.Staff.View },
        loadComponent: () =>
          import('./pages/merchant/merchant-staff/merchant-staff.component').then(m => m.MerchantStaffComponent)
      },
      {
        path: 'no-access',
        loadComponent: () => import('./pages/no-access/no-access.component').then(m => m.NoAccessComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];

export const appRouterProviders = [provideRouter(routes)];
