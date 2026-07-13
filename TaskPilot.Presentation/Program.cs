using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using TaskPilot.AI.Extensions;
using TaskPilot.Data;
using TaskPilot.Infrastructure.Extensions;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;
using TaskPilot.Presentation.Middlewares;
using TaskPilot.Presentation.Models;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;
using Hangfire;
using TaskPilot.Models.Common;

namespace TaskPilot.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddData(builder.Configuration);
            builder.Services.AddServices(builder.Configuration);
           // builder.Services.AddScoped<TaskPilot.Services.Interfaces.IAgileCoachService, TaskPilot.Services.Implementations.AgileCoachService>();
            builder.Services.AddAiLayer(builder.Configuration);
            builder.Services.AddScoped<TaskPilot.AI.Agents.AgileCoachAgent>();
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
                            "https://taskpilotapi.runasp.net" // production (adjust to real frontend URL)
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();  // now valid because origin is explicit
                });
            });
            builder.Services.AddSignalR();
            builder.Services.AddScoped<TaskPilot.Services.Interfaces.INotificationNotifier, TaskPilot.Presentation.Services.NotificationNotifier>();
            
            builder.Services.AddControllers()
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
               o.RequireHttpsMetadata = false;
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


            app.UseSwagger();
            app.UseSwaggerUI();
            
            app.UseHangfireDashboard("/hangfire");
            RecurringJob.AddOrUpdate<TaskPilot.Services.BackgroundJobs.SprintRiskDetectionJob>(
                "SprintRiskDetectionJob",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily);

            //app.UseCors("AllowAll");
            app.UseCors("AllowFrontend");

            app.UseHttpsRedirection();

            app.UseMiddleware<LanguageMiddleware>();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<TaskPilot.Presentation.Hubs.NotificationHub>("/hubs/notifications");

            app.Run();
        }
    }
}
