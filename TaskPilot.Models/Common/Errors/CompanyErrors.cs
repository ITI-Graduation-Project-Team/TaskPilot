namespace TaskPilot.Models.Common.Errors
{
    public static class CompanyErrors
    {
        public static readonly Error CompanyAlreadyExists =
            CommonErrors.Conflict(
                "Company already exists.");

        public static readonly Error InvalidOwner =
            CommonErrors.Forbidden(
                "Invalid company owner.");

        public static readonly Error InvalidInvitation =
            CommonErrors.InvalidInput(
                "Invitation is invalid.");

        public static readonly Error InvitationExpired =
            CommonErrors.InvalidInput(
                "Invitation has expired.");

        public static readonly Error InvitationAlreadyAccepted =
            CommonErrors.Conflict(
                "Invitation already accepted.");
    }
}
