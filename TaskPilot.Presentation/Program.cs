using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
namespace TaskPilot.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddData(builder.Configuration);
            builder.Services.AddServices(builder.Configuration);
            builder.Services.AddAiLayer(builder.Configuration);
            builder.Services.AddInfrastructure(
    builder.Configuration);
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters
                        .Add(new JsonStringEnumConverter());
                });

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

                   IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWTSettings:Key"]))
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
                       var  description = localizer.GetString(error.Code);
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
            
            app.UseCors("AllowAll");
            app.UseHttpsRedirection();

            app.UseMiddleware<LanguageMiddleware>();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
