using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.Tasks
{
    public class PmReopenTaskRequest : IValidatableObject
    {
        [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters.")]
        public string? ReasonEn { get; set; }

        [StringLength(1000, ErrorMessage = "Reason in Arabic cannot exceed 1000 characters.")]
        public string? ReasonAr { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(ReasonEn) && string.IsNullOrWhiteSpace(ReasonAr))
            {
                yield return new ValidationResult(
                    "You must provide a reason in either English or Arabic.",
                    new[] { nameof(ReasonEn), nameof(ReasonAr) }
                );
            }
        }
    }
}
