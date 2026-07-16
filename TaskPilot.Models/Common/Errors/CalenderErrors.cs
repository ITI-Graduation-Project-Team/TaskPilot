using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Models.Common.Errors
{
    public class CalenderErrors
    {
        public static readonly Error EventNotFoundOrUnauthorized = new (
            "CalendarNotFoundOrUnauthorized",
            ErrorType.NotFound);
            
        public static readonly Error CannotRescheduleAssignedTask = new (
            "Calendar.CannotRescheduleAssignedTask",
            ErrorType.Validation);

        public static readonly Error CannotDeleteAssignedTask = new (
            "Calendar.CannotDeleteAssignedTask",
            ErrorType.Validation);
        public static readonly Error CannotUpdateStatusOfAssignedTask = new(
            "Calendar.CannotUpdateStatusOfAssignedTask",
            ErrorType.Validation);

    }
}
