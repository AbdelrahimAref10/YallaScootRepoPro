using Application;
using Infrastructure;
using Infrastructure.Configuration;
using Microsoft.AspNetCore.StaticFiles;

namespace Volt.Server
{
    public static class StatupExtensions
    {
        private const string NoCache = "no-cache, no-store, must-revalidate";

        // Angular build output: name-HASH.js / name-HASH.css (e.g. main-ABCD1234.js, chunk-2W3EUD6L.js).
        private static readonly System.Text.RegularExpressions.Regex HashedBundle =
            new(@"-[A-Z0-9]{8}\.(js|css)$", System.Text.RegularExpressions.RegexOptions.Compiled);

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
                    // Only hashed bundles (main-XXXX.js, styles-XXXX.css) change name every build, so only
                    // they may be cached for a year. Everything else keeps its name across releases
                    // (index.html, assets/i18n/*.json, appSettings.json, images) and must be revalidated,
                    // or browsers keep last release's copy: old translations, old bundle references.
                    var name = ctx.File.Name;
                    var cache = HashedBundle.IsMatch(name) ? "public,max-age=31536000,immutable"
                        : name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? NoCache
                        : "no-cache"; // revalidate: unchanged files come back as a cheap 304

                    ctx.Context.Response.Headers.Append("Cache-Control", cache);
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
