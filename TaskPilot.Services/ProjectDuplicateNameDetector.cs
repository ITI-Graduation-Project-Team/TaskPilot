using Microsoft.EntityFrameworkCore;

namespace TaskPilot.Services;

public static class ProjectDuplicateNameDetector
{
    private const string CurrentIndexName = "IX_Projects_CompanyId_NormalizedNameEn";
    private const string LegacyIndexName = "IX_Projects_CompanyId_NameEn";

    public static bool IsDuplicateNameViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains(CurrentIndexName, StringComparison.OrdinalIgnoreCase) ||
               message.Contains(LegacyIndexName, StringComparison.OrdinalIgnoreCase);
    }
}
