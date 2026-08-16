using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Context;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

var services = new ServiceCollection();
services.AddSingleton<ICurrentUserService, MockUser>();
services.AddLogging(l => l.AddConsole().SetMinimumLevel(LogLevel.Information));
services.AddDbContext<ApplicationDbContext>(options => {
    options.UseSqlServer("Server=db49999.public.databaseasp.net; Database=db49999; User Id=db49999; Password=Qj6#+Xw2o9W=; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;");
});
var sp = services.BuildServiceProvider();
var context = sp.GetRequiredService<ApplicationDbContext>();

var p2 = Guid.Parse("d1201b52-0bd3-4671-d12b-08def6ae054f"); // SLOW

Console.WriteLine("Querying SLOW project with standard Include...");
var sw2 = Stopwatch.StartNew();
var slowProject = context.Projects
    .Include(x => x.SetupState)
    .Include(x => x.ProjectEmployees)
        .ThenInclude(pe => pe.Employee)
            .ThenInclude(employee => employee.UserSkills)
    .FirstOrDefault(x => x.Id == p2 && !x.IsDeleted);
sw2.Stop();
Console.WriteLine($"Standard took {sw2.ElapsedMilliseconds}ms");

context.ChangeTracker.Clear();

Console.WriteLine("Querying SLOW project with AsSplitQuery...");
var sw3 = Stopwatch.StartNew();
var splitProject = context.Projects
    .Include(x => x.SetupState)
    .Include(x => x.ProjectEmployees)
        .ThenInclude(pe => pe.Employee)
            .ThenInclude(employee => employee.UserSkills)
    .AsSplitQuery()
    .FirstOrDefault(x => x.Id == p2 && !x.IsDeleted);
sw3.Stop();
Console.WriteLine($"SplitQuery took {sw3.ElapsedMilliseconds}ms");

context.ChangeTracker.Clear();

Console.WriteLine("Querying SLOW project with Select...");
var sw4 = Stopwatch.StartNew();
var projected = context.Projects
    .Where(x => x.Id == p2 && !x.IsDeleted)
    .Select(p => new {
        p.Id,
        p.NameEn,
        p.TechStack,
        p.PlatformTargets,
        p.ProjectType,
        SetupState = p.SetupState,
        ActiveMemberCount = p.ProjectEmployees.Count(pe => pe.IsActive && pe.Employee != null && !pe.Employee.IsDeactivated),
        MembersWithSkillsCount = p.ProjectEmployees.Count(pe => pe.IsActive && pe.Employee != null && !pe.Employee.IsDeactivated && pe.Employee.UserSkills.Any())
    })
    .FirstOrDefault();
sw4.Stop();
Console.WriteLine($"Select took {sw4.ElapsedMilliseconds}ms");

class MockUser : ICurrentUserService {
    public Guid? UserId => Guid.Empty;
    public string? Role => "ProjectManager";
    public bool IsAuthenticated => true;
}
