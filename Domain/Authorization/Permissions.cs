namespace Domain.Authorization
{
    /// <summary>
    /// Every permission in the system, named "{Scope}.{Module}.{Action}".
    /// This class is the single source of truth: <see cref="PermissionCatalog"/> reads it
    /// by reflection and the seeder syncs it into VO_Permission.
    /// </summary>
    public static class Permissions
    {
        public static class Admin
        {
            public static class Dashboard
            {
                public const string View = "Admin.Dashboard.View";
            }

            public static class Orders
            {
                public const string View = "Admin.Orders.View";
                public const string Create = "Admin.Orders.Create";
                public const string Edit = "Admin.Orders.Edit";
            }

            public static class Shifts
            {
                public const string View = "Admin.Shifts.View";
                public const string Create = "Admin.Shifts.Create";
                public const string Edit = "Admin.Shifts.Edit";
                public const string Delete = "Admin.Shifts.Delete";
            }

            public static class Merchants
            {
                public const string View = "Admin.Merchants.View";
                public const string Create = "Admin.Merchants.Create";
                public const string Edit = "Admin.Merchants.Edit";
                public const string Delete = "Admin.Merchants.Delete";
            }

            public static class Deliveries
            {
                public const string View = "Admin.Deliveries.View";
                public const string Create = "Admin.Deliveries.Create";
                public const string Edit = "Admin.Deliveries.Edit";
                public const string Delete = "Admin.Deliveries.Delete";
            }

            public static class Customers
            {
                public const string View = "Admin.Customers.View";
                public const string Create = "Admin.Customers.Create";
                public const string Edit = "Admin.Customers.Edit";
            }

            public static class Vehicles
            {
                public const string View = "Admin.Vehicles.View";
                public const string Create = "Admin.Vehicles.Create";
                public const string Edit = "Admin.Vehicles.Edit";
                public const string Delete = "Admin.Vehicles.Delete";
            }

            public static class Categories
            {
                public const string View = "Admin.Categories.View";
                public const string Create = "Admin.Categories.Create";
                public const string Edit = "Admin.Categories.Edit";
                public const string Delete = "Admin.Categories.Delete";
            }

            public static class SubCategories
            {
                public const string View = "Admin.SubCategories.View";
                public const string Create = "Admin.SubCategories.Create";
                public const string Edit = "Admin.SubCategories.Edit";
                public const string Delete = "Admin.SubCategories.Delete";
            }

            public static class Cities
            {
                public const string View = "Admin.Cities.View";
                public const string Create = "Admin.Cities.Create";
                public const string Edit = "Admin.Cities.Edit";
                public const string Delete = "Admin.Cities.Delete";
            }

            public static class Settlements
            {
                public const string View = "Admin.Settlements.View";
                public const string Create = "Admin.Settlements.Create";
            }

            public static class Journals
            {
                public const string View = "Admin.Journals.View";
            }

            public static class Reports
            {
                public const string View = "Admin.Reports.View";
            }

            public static class SystemUsers
            {
                public const string View = "Admin.SystemUsers.View";
                public const string Create = "Admin.SystemUsers.Create";
                public const string Edit = "Admin.SystemUsers.Edit";
                public const string Delete = "Admin.SystemUsers.Delete";
            }

            public static class Roles
            {
                public const string View = "Admin.Roles.View";
                public const string Create = "Admin.Roles.Create";
                public const string Edit = "Admin.Roles.Edit";
                public const string Delete = "Admin.Roles.Delete";
            }

            public static class Support
            {
                public const string View = "Admin.Support.View";
                public const string Edit = "Admin.Support.Edit";
            }
        }

        public static class Merchant
        {
            public static class Home
            {
                public const string View = "Merchant.Home.View";
            }

            public static class Orders
            {
                public const string View = "Merchant.Orders.View";
                public const string Edit = "Merchant.Orders.Edit";
            }

            public static class Vehicles
            {
                public const string View = "Merchant.Vehicles.View";
                public const string Create = "Merchant.Vehicles.Create";
                public const string Edit = "Merchant.Vehicles.Edit";
                public const string Delete = "Merchant.Vehicles.Delete";
            }

            public static class Payments
            {
                public const string View = "Merchant.Payments.View";
            }

            public static class Staff
            {
                public const string View = "Merchant.Staff.View";
                public const string Create = "Merchant.Staff.Create";
                public const string Edit = "Merchant.Staff.Edit";
                public const string Delete = "Merchant.Staff.Delete";
            }
        }
    }
}
