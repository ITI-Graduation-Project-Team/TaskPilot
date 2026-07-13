using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Models.Common
{
    public class SuccessCodes
    {
        public static class Auth
        {
            public const string ResendConfirmation = "RESEND_CONFIRMATION_SUCCESS";
            public const string GoogleLogin = "GOOGLE_LOGIN_SUCCESS";
            public const string TokenRefreshed = "TOKEN_REFRESHED_SUCCESS";
            public const string Register = "REGISTER_SUCCESS";
            public const string Login = "LOGIN_SUCCESS";
            public const string EmailConfirmed = "EMAIL_CONFIRMED_SUCCESS";
            public const string OtpSent = "OTP_SENT_SUCCESS";
            public const string ForgotPassword = "PASSWORD_RESET_OTP_SENT";
            public const string PasswordReset = "PASSWORD_RESET_SUCCESS";
            public const string Logout = "LOGOUT_SUCCESS";
            public const string InvitationCompleted = "INVITATION_COMPLETED_SUCCESS";
        }
        public static class Company
        {
            public const string Setup = "COMPANY_SETUP_SUCCESS";
            public const string EmployeesSearched = "COMPANY_EMPLOYEES_SEARCHED";
            public const string EmployeeInvitationsSent = "EMPLOYEE_INVITATIONS_SENT_SUCCESS";
        }

        public static class Project
        {
            public const string Created = "PROJECT_CREATED_SUCCESS";
            public const string Updated = "PROJECT_UPDATED_SUCCESS";
            public const string Deleted = "PROJECT_DELETED_SUCCESS";
            public const string Retrieved = "PROJECT_RETRIEVED_SUCCESS";
            public const string StatusRetrieved = "PROJECT_STATUS_RETRIEVED_SUCCESS";
            public const string StatusUpdated = "PROJECT_STATUS_UPDATED_SUCCESS";
            public const string StatusTransitionsRetrieved = "PROJECT_STATUS_TRANSITIONS_RETRIEVED_SUCCESS";
        }

        public static class Skill
        {
            public const string Created = "SKILL_CREATED_SUCCESS";
            public const string Deleted = "SKILL_DELETED_SUCCESS";
            public const string Retrieved = "SKILL_RETRIEVED_SUCCESS";
            public const string Migrated = "SKILL_MIGRATED_SUCCESS";
        }

        public static class Role
        {
            public const string PermissionsUpdated = "ROLE_PERMISSIONS_UPDATED";
            public const string Retrieved = "ROLE_RETRIEVED_SUCCESS";
        }

        public static class SubscriptionPlan
        {
            public const string Created = "PLAN_CREATED_SUCCESS";
            public const string Updated = "PLAN_UPDATED_SUCCESS";
            public const string Deleted = "PLAN_DELETED_SUCCESS";
            public const string Retrieved = "PLAN_RETRIEVED_SUCCESS";
        }

        public static class User
        {
            public const string Deleted = "USER_DELETED_SUCCESS";
            public const string Retrieved = "USER_RETRIEVED_SUCCESS";
        }

        public static class UserSubscription
        {
            public const string Created = "SUBSCRIPTION_CREATED_SUCCESS";
            public const string Updated = "SUBSCRIPTION_UPDATED_SUCCESS";
            public const string Deleted = "SUBSCRIPTION_DELETED_SUCCESS";
            public const string Retrieved = "SUBSCRIPTION_RETRIEVED_SUCCESS";
        }

        public static class Employee
        {
            public const string CvUploaded = "EMPLOYEE_CV_UPLOADED";
        }

        public static class Requirement
        {
            public const string DocumentUploaded = "REQUIREMENT_DOCUMENT_UPLOADED";
            public const string MessageSent = "REQUIREMENT_MESSAGE_SENT";
            public const string SessionRetrieved = "REQUIREMENT_SESSION_RETRIEVED";
        }

        public static class AiProject
        {
            public const string Generated = "AI_PROJECT_GENERATED";
            public const string Confirmed = "AI_PROJECT_CONFIRMED";
        }

        public static class Assignment
        {
            public const string ScoringCompleted = "ASSIGNMENT_SCORING_COMPLETED";
            public const string ExplanationsGenerated = "ASSIGNMENT_EXPLANATIONS_GENERATED";
            public const string ExplanationFallback = "ASSIGNMENT_EXPLANATION_FALLBACK";
            public const string AssignmentsConfirmed = "ASSIGNMENT_ASSIGNMENTS_CONFIRMED";
            public const string TaskUnassigned = "ASSIGNMENT_TASK_UNASSIGNED";
        }

        public static class Wbs
        {
            public const string SkillsEnriched = "WBS_SKILLS_ENRICHED";
            public const string SkillCreated = "WBS_SKILL_CREATED";
            public const string RequiredSkillsCompleted = "WBS_REQUIRED_SKILLS_COMPLETED";
        }

        public static class Sprint
        {
            public const string Started = "SPRINT_STARTED_SUCCESS";
            public const string Completed = "SPRINT_COMPLETED_SUCCESS";
            public const string ActiveRetrieved = "SPRINT_ACTIVE_RETRIEVED_SUCCESS";
        }
    }
}
