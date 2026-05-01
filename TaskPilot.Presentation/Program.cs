using TaskPilot.Data;
using TaskPilot.Data.Identity;
using TaskPilot.Models.Common;
using TaskPilot.Services;

namespace TaskPilot.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ── Layer registrations ──
            builder.Services.AddData(builder.Configuration);
            builder.Services.AddServices();

            // ── Infrastructure services (implementations in Data, interfaces in Models) ──
            builder.Services.AddScoped<IIdentityService, IdentityService>();

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
