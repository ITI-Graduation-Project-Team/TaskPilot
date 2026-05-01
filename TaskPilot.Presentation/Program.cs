using TaskPilot.Data;
using TaskPilot.Models.Common;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;

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

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
           
            var app = builder.Build();

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
