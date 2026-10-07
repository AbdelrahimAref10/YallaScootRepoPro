using Domain.Enums;
using System.Reflection;

namespace Domain.Authorization
{
    public sealed record PermissionDefinition(string Name, AppRole Scope, string Module, string Action);

    /// <summary>Flattened view of <see cref="Permissions"/>.</summary>
    public static class PermissionCatalog
    {
        public const string AdminScope = "Admin";
        public const string MerchantScope = "Merchant";

        /// <summary>Display order of actions in a module.</summary>
        public static readonly string[] Actions = ["View", "Create", "Edit", "Delete"];

        public static IReadOnlyList<PermissionDefinition> All { get; } = Build();

        private static readonly Dictionary<string, PermissionDefinition> ByName = All.ToDictionary(p => p.Name);

        public static IEnumerable<PermissionDefinition> ForScope(AppRole scope) =>
            All.Where(p => p.Scope == scope);

        public static PermissionDefinition? Find(string name) => ByName.GetValueOrDefault(name);

        public static bool Exists(string name) => ByName.ContainsKey(name);

        /// <summary>Only the admin panel and the merchant panel have sub-roles.</summary>
        public static bool IsSubRoleScope(AppRole scope) =>
            scope is AppRole.SuperAdmin or AppRole.Merchant;

        private static List<PermissionDefinition> Build()
        {
            var result = new List<PermissionDefinition>();
            foreach (var scopeType in typeof(Permissions).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
            {
                var scope = scopeType.Name switch
                {
                    AdminScope => AppRole.SuperAdmin,
                    MerchantScope => AppRole.Merchant,
                    _ => throw new InvalidOperationException($"Unknown permission scope '{scopeType.Name}'")
                };

                foreach (var moduleType in scopeType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
                {
                    foreach (var field in moduleType.GetFields(BindingFlags.Public | BindingFlags.Static)
                                 .Where(f => f.IsLiteral && f.FieldType == typeof(string)))
                    {
                        var name = (string)field.GetRawConstantValue()!;
                        result.Add(new PermissionDefinition(name, scope, moduleType.Name, field.Name));
                    }
                }
            }
            return result;
        }
    }
}
