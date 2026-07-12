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
        
    }
}
