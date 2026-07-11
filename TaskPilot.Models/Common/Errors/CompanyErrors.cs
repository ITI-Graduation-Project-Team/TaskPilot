using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Models.Common.Errors
{
    public class CompanyErrors
    {
        public static readonly Error CompanyAlreadyExists =
            new("COMPANY_ALREADY_EXISTS", ErrorType.Conflict);

        public static readonly Error InvalidOwner =
            new("INVALID_OWNER", ErrorType.Forbidden);

        public static readonly Error InvalidInvitation =
            new("INVALID_INVITATION", ErrorType.Validation);

        public static readonly Error InvitationExpired =
            new("INVITATION_EXPIRED", ErrorType.Validation);

        public static readonly Error InvitationAlreadyAccepted =
            new("INVITATION_ALREADY_ACCEPTED", ErrorType.Conflict);

        public static readonly Error NotFound = 
            new("COMPANY_NOT_FOUND", ErrorType.NotFound);

        public static readonly Error ProjectManagerAlreadyHasCompany =
           new("PROJECT_MANAGER_ALREADY_HAS_COMPANY", ErrorType.Conflict);

        public static readonly Error InvitationAlreadySent =
            new("INVITATION_ALREADY_SENT", ErrorType.Conflict);

        public static readonly Error EmployeeAlreadyInCompany =
            new("EMPLOYEE_ALREADY_IN_COMPANY", ErrorType.Conflict);

        public static readonly Error InvalidEmail =
            new("INVALID_EMAIL", ErrorType.Validation);

        public static readonly Error NoEmployeesSpecified =
            new("NO_EMPLOYEES_SPECIFIED", ErrorType.Validation);

        public static readonly Error InvalidInvitationStatus =
            new("INVALID_INVITATION_STATUS", ErrorType.Validation);

        public static readonly Error InvalidPageNumber =
            new("INVALID_PAGE_NUMBER", ErrorType.Validation);

        public static readonly Error InvalidPageSize =
            new("INVALID_PAGE_SIZE", ErrorType.Validation);
    }
}