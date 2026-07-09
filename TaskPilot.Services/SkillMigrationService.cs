using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Skills;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services;

public class SkillMigrationService : ISkillMigrationService
{
    private readonly IRepository<Skill> _skillRepository;
    private readonly IRepository<SkillAlias> _skillAliasRepository;
    private readonly IRepository<UserSkill> _userSkillRepository;
    private readonly IRepository<TaskRequiredSkill> _taskRequiredSkillRepository;

    public SkillMigrationService(
        IRepository<Skill> skillRepository,
        IRepository<SkillAlias> skillAliasRepository,
        IRepository<UserSkill> userSkillRepository,
        IRepository<TaskRequiredSkill> taskRequiredSkillRepository)
    {
        _skillRepository = skillRepository;
        _skillAliasRepository = skillAliasRepository;
        _userSkillRepository = userSkillRepository;
        _taskRequiredSkillRepository = taskRequiredSkillRepository;
    }

    public async Task<Result<SkillMigrationReportDto>> MergeSkillsAsync(SkillMergeRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.ObsoleteSkillIds == null || !request.ObsoleteSkillIds.Any())
            return Result.Failure<SkillMigrationReportDto>(SkillErrors.EmptyList);

        if (request.ObsoleteSkillIds.Contains(request.CanonicalSkillId))
            return Result.Failure<SkillMigrationReportDto>(SkillErrors.DuplicateCanonicalSkill);

        var canonicalSkill = await _skillRepository.GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == request.CanonicalSkillId, cancellationToken);

        if (canonicalSkill == null)
            return Result.Failure<SkillMigrationReportDto>(SkillErrors.CanonicalSkillNotFound);

        var obsoleteSkills = await _skillRepository.GetQueryable()
            .Where(s => request.ObsoleteSkillIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        if (obsoleteSkills.Count != request.ObsoleteSkillIds.Count)
            return Result.Failure<SkillMigrationReportDto>(SkillErrors.NotFound);

        // Pre-validation pass for aliases
        foreach (var obsolete in obsoleteSkills)
        {
            var aliasAlreadyExists = await _skillAliasRepository.GetQueryable()
                .AnyAsync(a => a.Alias.ToLower() == obsolete.Name.ToLower(), cancellationToken);
            
            if (aliasAlreadyExists)
                return Result.Failure<SkillMigrationReportDto>(SkillErrors.AliasAlreadyExists);
        }

        var report = new SkillMigrationReportDto
        {
            CanonicalSkillId = canonicalSkill.Id,
            CanonicalSkillName = canonicalSkill.Name
        };

        // Execution pass
        foreach (var obsolete in obsoleteSkills)
        {
            // 1. Create Alias
            var newAlias = new SkillAlias
            {
                SkillId = canonicalSkill.Id,
                Alias = obsolete.Name
            };
            await _skillAliasRepository.AddAsync(newAlias);
            report.AliasesCreated++;

            // 2. Migrate UserSkills
            var obsoleteUserSkills = await _userSkillRepository.GetQueryable()
                .Where(us => us.SkillId == obsolete.Id)
                .ToListAsync(cancellationToken);

            foreach (var obsUserSkill in obsoleteUserSkills)
            {
                var existingCanonicalUserSkill = await _userSkillRepository.GetQueryable()
                    .FirstOrDefaultAsync(us => us.UserId == obsUserSkill.UserId && us.SkillId == canonicalSkill.Id, cancellationToken);

                if (existingCanonicalUserSkill != null)
                {
                    if (obsUserSkill.Level > existingCanonicalUserSkill.Level)
                        existingCanonicalUserSkill.Level = obsUserSkill.Level;
                    
                    if (obsUserSkill.ConfidenceScore > existingCanonicalUserSkill.ConfidenceScore)
                        existingCanonicalUserSkill.ConfidenceScore = obsUserSkill.ConfidenceScore;
                    
                    if (obsUserSkill.IsPrimary)
                        existingCanonicalUserSkill.IsPrimary = true;
                        
                    if (obsUserSkill.YearsOfExperience.HasValue)
                    {
                        if (!existingCanonicalUserSkill.YearsOfExperience.HasValue || obsUserSkill.YearsOfExperience > existingCanonicalUserSkill.YearsOfExperience)
                        {
                            existingCanonicalUserSkill.YearsOfExperience = obsUserSkill.YearsOfExperience;
                        }
                    }

                    _userSkillRepository.Update(existingCanonicalUserSkill);
                    _userSkillRepository.Delete(obsUserSkill);
                }
                else
                {
                    obsUserSkill.SkillId = canonicalSkill.Id;
                    _userSkillRepository.Update(obsUserSkill);
                }
                report.EmployeeSkillsMigrated++;
            }

            // 3. Migrate TaskRequiredSkills
            var obsoleteTaskSkills = await _taskRequiredSkillRepository.GetQueryable()
                .Where(trs => trs.SkillId == obsolete.Id)
                .ToListAsync(cancellationToken);

            foreach (var obsTaskSkill in obsoleteTaskSkills)
            {
                var existingCanonicalTaskSkill = await _taskRequiredSkillRepository.GetQueryable()
                    .FirstOrDefaultAsync(trs => trs.TaskId == obsTaskSkill.TaskId && trs.SkillId == canonicalSkill.Id, cancellationToken);

                if (existingCanonicalTaskSkill != null)
                {
                    if (obsTaskSkill.RequiredLevel > existingCanonicalTaskSkill.RequiredLevel)
                    {
                        existingCanonicalTaskSkill.RequiredLevel = obsTaskSkill.RequiredLevel;
                    }
                    _taskRequiredSkillRepository.Update(existingCanonicalTaskSkill);
                    _taskRequiredSkillRepository.Delete(obsTaskSkill);
                }
                else
                {
                    obsTaskSkill.SkillId = canonicalSkill.Id;
                    _taskRequiredSkillRepository.Update(obsTaskSkill);
                }
                report.TaskRequiredSkillsMigrated++;
            }

            // 4. Soft delete obsolete skill
            obsolete.IsDeleted = true;
            _skillRepository.Update(obsolete);
            
            report.ObsoleteSkillsProcessed++;
        }

        return Result.Success(report);
    }
}
