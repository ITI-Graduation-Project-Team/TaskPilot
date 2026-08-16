using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace TaskPilot.Tests.Endpoints
{
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Role, "ProjectManager"),
                new Claim(ClaimTypes.NameIdentifier, "feecbd5c-8bba-4ba3-902b-08def90ebad3") // Using a real guid from previous prompts just in case
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class OptimizationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public OptimizationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(defaultScheme: "TestScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            "TestScheme", options => { });
                });
            });
        }

        [Fact]
        public async Task TestServiceMethodsExecuteCorrectly()
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TaskPilot.Data.Context.ApplicationDbContext>();
            var companyService = scope.ServiceProvider.GetRequiredService<TaskPilot.Services.Interfaces.ICompanyService>();

            var company = dbContext.Companies.FirstOrDefault();
            if (company == null)
            {
                Console.WriteLine("No company found in DB.");
                return;
            }

            Console.WriteLine($"\n--- STATISTICS SERVICE RESULT ---");
            var statResult = await companyService.GetEmployeeStatisticsAsync(company.Id);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(statResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"\n--- EMPLOYEES SERVICE RESULT ---");
            var empResult = await companyService.GetCompanyEmployeesAsync(company.Id, 1, 10, null);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(empResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
