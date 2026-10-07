using Application;
using Infrastructure;
using Infrastructure.Configuration;
using Microsoft.AspNetCore.StaticFiles;

namespace Volt.Server
{
    public static class StatupExtensions
    {
        private const string NoCache = "no-cache, no-store, must-revalidate";

        public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
        {
            builder.Services.AddApplicationServices();
            builder.Services.AddDatabaseServices(builder.Configuration);
            builder.Services.AddFireBaseConfigurations(builder.Configuration, builder.Environment.ContentRootPath);

            // Add SignalR
            builder.Services.AddSignalR();

            // Register Admin Notification Hub Service (must be here as it requires Presentation layer)
            builder.Services.AddScoped<Infrastructure.Services.IAdminNotificationHubService, Presentation.Services.AdminNotificationHubService>();
            builder.Services.AddScoped<Infrastructure.Services.IMerchantNotificationHubService, Presentation.Services.MerchantNotificationHubService>();

            // Sub-role permissions: [HasPermission(Permissions.Admin.Orders.View)]
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Presentation.Authorization.PermissionPolicyProvider>();
            builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Presentation.Authorization.PermissionAuthorizationHandler>();

            builder.Services.AddControllers();
            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    policy => policy
                        .AllowAnyOrigin() // Allow any origin
                        .AllowAnyMethod() // Allow any method (GET, POST, etc.)
                        .AllowAnyHeader()); // Allow any header
            });
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddOpenApiDocument(document =>
            {
                document.Title = "Volt API";
                document.Description =
                    "Grouped by controller folder (Admin, Auth, Customer, General). Each group lists that area's controllers.";
                document.OperationProcessors.Add(new OpenApi.ControllerFolderTagsProcessor());
            });
            return builder.Build();
        }

        public static WebApplication ConfigurePipeline(this WebApplication app)
        {
            // Global exception handling must be first
            app.UseMiddleware<Presentation.Middleware.GlobalExceptionHandlingMiddleware>();

            app.UseCors("AllowAll");

            // Static files must be before routing
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    // index.html must never be cached: it points at the current build's hashed bundles.
                    // A cached copy keeps running the previous release against the new API after a deploy.
                    // Hashed bundles (main-XXXX.js) change name every build, so they can be cached for a year.
                    var isHtml = ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
                    ctx.Context.Response.Headers.Append("Cache-Control", isHtml ? NoCache : "public,max-age=31536000");
                }
            });

            app.UseSwaggerUi();
            app.UseOpenApi();
            app.UseRouting();
            app.UseHttpsRedirection();

            // JWT Authentication
            app.UseAuthentication();

            app.UseAuthorization();
            app.MapControllers();

            // Map SignalR Hub
            app.MapHub<Presentation.Hubs.AdminNotificationHub>("/AdminNotificationHub");
            app.MapHub<Presentation.Hubs.MerchantNotificationHub>("/MerchantNotificationHub");

            // An unknown API route is a 404, not the Angular page (which the client would fail to parse as JSON).
            app.Map("/api/{**path}", (HttpContext context) => Results.NotFound());

            // Client-side routes (/main/dashboard, ...) get index.html, never from cache.
            app.MapFallbackToFile("/index.html", new StaticFileOptions
            {
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.Append("Cache-Control", NoCache)
            });
            return app;
        }

    }
}
