using Application;
using Infrastructure;
using Infrastructure.Configuration;
using Microsoft.AspNetCore.StaticFiles;

namespace Volt.Server
{
    public static class StatupExtensions
    {
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
                    // Cache static files for 1 year
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
                }
            });

            app.UseSwaggerUi();
            app.UseOpenApi();
            app.UseRouting();
            app.UseHttpsRedirection();

            // JWT Authentication
            app.UseAuthentication();

            // Admin controllers only use [Authorize]; rider-app tokens must not reach them.
            app.Use(async (context, next) =>
            {
                var user = context.User;
                if (context.Request.Path.StartsWithSegments("/api/admin")
                    && user.Identity?.IsAuthenticated == true
                    && user.IsInRole(Domain.Enums.AppRoleNames.Delivery)
                    && !user.IsInRole(Domain.Enums.AppRoleNames.SuperAdmin))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                await next();
            });

            app.UseAuthorization();
            app.MapControllers();

            // Map SignalR Hub
            app.MapHub<Presentation.Hubs.AdminNotificationHub>("/AdminNotificationHub");
            app.MapHub<Presentation.Hubs.MerchantNotificationHub>("/MerchantNotificationHub");

            app.MapFallbackToFile("/index.html");
            return app;
        }

    }
}
