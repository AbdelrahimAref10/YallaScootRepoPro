using Microsoft.AspNetCore.Mvc.Controllers;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Volt.Server.OpenApi;

/// <summary>
/// Swagger groups by OpenAPI tags, not by C# folders.
/// This maps each action to a tag: "{folder} / {controller}" so the UI lists
/// Admin, Auth, Customer, General together — like Solution Explorer.
/// </summary>
public sealed class ControllerFolderTagsProcessor : IOperationProcessor
{
    private const string ControllersNamespace = "Volt.Server.Controllers.";

    public bool Process(OperationProcessorContext context)
    {
        var controllerType = ResolveControllerType(context);
        var folder = ResolveFolder(controllerType);
        var controller = StripControllerSuffix(controllerType?.Name ?? "Unknown");

        var tags = context.OperationDescription.Operation.Tags
            ??= new System.Collections.Generic.List<string>();
        tags.Clear();
        tags.Add($"{folder} / {controller}");
        return true;
    }

    private static Type? ResolveControllerType(OperationProcessorContext context)
    {
        if (context is AspNetCoreOperationProcessorContext aspNet
            && aspNet.ApiDescription.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            return descriptor.ControllerTypeInfo.AsType();
        }

        return context.ControllerType;
    }

    private static string ResolveFolder(Type? controllerType)
    {
        var ns = controllerType?.Namespace ?? string.Empty;
        if (ns.StartsWith(ControllersNamespace, StringComparison.Ordinal))
        {
            var rest = ns[ControllersNamespace.Length..];
            var folder = rest.Split('.', 2)[0];
            if (!string.IsNullOrWhiteSpace(folder))
            {
                return folder;
            }
        }

        return "Other";
    }

    private static string StripControllerSuffix(string name)
    {
        const string suffix = "Controller";
        return name.EndsWith(suffix, StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
    }
}
