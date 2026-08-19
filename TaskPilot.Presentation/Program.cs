using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskPilot.AI.Extensions;
using TaskPilot.Data;
using TaskPilot.Infrastructure.Extensions;
using TaskPilot.Infrastructure.Services.Google;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;
using TaskPilot.Presentation.Middlewares;
using TaskPilot.Presentation.Models;
using TaskPilot.Presentation.Extensions;
using TaskPilot.Services;
using TaskPilot.Services.BackgroundJobs;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation
{
    public partial class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddData(builder.Configuration);
            builder.Services.AddServices(builder.Configuration);
           // builder.Services.AddScoped<TaskPilot.Services.Interfaces.IAgileCoachService, TaskPilot.Services.Implementations.AgileCoachService>();
            builder.Services.AddAiLayer(builder.Configuration);

            builder.Services.AddInfrastructure(
    builder.Configuration);
            builder.Services.AddPaymentLayer(builder.Configuration);
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy
                        .WithOrigins(
                            "http://localhost:4200",        // Angular dev
                            "http://localhost:4000",        // any other local port
                            "https://taskpilotapi.runasp.net", // production (adjust to real frontend URL)
                            "https://taskpilot.runasp.net"
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();  // now valid because origin is explicit
                });
            });
            builder.Services.AddTaskPilotSignalR(builder.Configuration);
            builder.Services.AddScoped<TaskPilot.Services.Interfaces.INotificationNotifier, TaskPilot.Presentation.Services.NotificationNotifier>();
            builder.Services.AddScoped<TaskPilot.Services.Interfaces.IProjectSetupStatusNotifier, TaskPilot.Presentation.Services.ProjectSetupStatusNotifier>();
            builder.Services.AddScoped<TaskPilot.Services.Interfaces.ITaskStatusChangeNotifier, TaskPilot.Presentation.Services.TaskStatusChangeNotifier>();

            builder.Services.AddScoped<TaskPilot.Services.Interfaces.External.IGoogleCalendarService, TaskPilot.Infrastructure.Services.Google.GoogleCalendarService>();
            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<TaskPilot.Presentation.Filters.ProjectIdTelemetryActionFilter>();
            })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters
                        .Add(new JsonStringEnumConverter());
                });

            builder.Services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
            
            builder.Services.AddHangfireServer();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.OperationFilter<LanguageHeaderFilter>();
                options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme."
                });

                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("bearer", document)] = []
                });
            }); builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
           .AddJwtBearer(o =>
           {
               o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
               o.SaveToken = false;
               o.TokenValidationParameters = new TokenValidationParameters
               {
                   ValidateIssuer = true,
                   ValidateAudience = true,
                   ValidateIssuerSigningKey = true,
                   ValidateLifetime = true,

                   ValidIssuer = builder.Configuration["JWTSettings:Issuer"],
                   ValidAudience = builder.Configuration["JWTSettings:Audience"],

                   IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWTSettings:Key"])),

                   RoleClaimType = ClaimTypes.Role,
                   NameClaimType = ClaimTypes.NameIdentifier
               };


                    
       
               o.Events = new JwtBearerEvents
               {
                   OnMessageReceived = context =>
                   {
                       var accessToken = context.Request.Query["access_token"];
                       var path = context.HttpContext.Request.Path;
                       if (!string.IsNullOrEmpty(accessToken) &&
                           path.StartsWithSegments("/hubs/notifications"))
                       {
                           context.Token = accessToken;
                       }
                       return Task.CompletedTask;
                   },
                   OnChallenge = context =>
                   {
                       context.HandleResponse();

                       context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                       context.Response.ContentType = "application/json";


                       var localizer = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
                       Error error = CommonErrors.Unauthorized();
                       var description = localizer.GetString(error.Code);
                       var response = ApiResponse.Fail(
                           error.Code,
                           description);
                       return context.Response.WriteAsync(JsonSerializer.Serialize(response));
                   }
               };
           });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("ProfileComplete", policy =>
                    policy.RequireAssertion(context =>
                        context.User.IsInRole(nameof(UserRole.ProjectManager)) ||
                        context.User.HasClaim("ProfileCompleted", "True")
                    ));
            });
            var app = builder.Build();

            if (builder.Configuration.GetValue<bool>("SignalR:RedisEnabled"))
            {
                app.Logger.LogInformation(
                    "SignalR Redis backplane enabled on backend {BackendInstance} with channel prefix {ChannelPrefix}",
                    Environment.MachineName,
                    builder.Configuration["SignalR:ChannelPrefix"]);
            }


            app.UseSwagger();
            app.UseSwaggerUI();

            if (app.Environment.IsDevelopment())
            {
                app.UseHangfireDashboard("/hangfire", new DashboardOptions
                {
                    Authorization = new[] { new HangfireAllowAllDashboardFilter() }
                });
            }

            using (var scope = app.Services.CreateScope())
            {
                var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

                recurringJobManager.AddOrUpdate<SprintRiskDetectionJob>(
                    "SprintRiskDetectionJob",
                    job => job.ExecuteAsync(CancellationToken.None),
                    Cron.Daily);
            }
            //app.UseCors("AllowAll");
            app.UseCors("AllowFrontend");

            app.UseHttpsRedirection();

            app.UseMiddleware<LanguageMiddleware>();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<TaskPilot.Presentation.Hubs.NotificationHub>("/hubs/notifications");

            if (!app.Environment.IsDevelopment() && !app.Environment.IsProduction())
            {
                app.MapHangfireDashboard("/hangfire", new DashboardOptions
                {
                    Authorization = new[] { new HangfireAllowAllDashboardFilter() }
                }).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = "Admin" });
            }

            app.Run();
           
    }
    }
}
public class HangfireAllowAllDashboardFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        return true;
    }
}
public partial class Program { }
