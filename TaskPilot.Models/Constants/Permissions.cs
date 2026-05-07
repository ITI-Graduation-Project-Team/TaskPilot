namespace TaskPilot.Models.Constants
{
    public static class Permissions
    {
        public static class Projects
        {
            public const string View = "Permissions.Projects.View";
            public const string Create = "Permissions.Projects.Create";
            public const string Edit = "Permissions.Projects.Edit";
            public const string Delete = "Permissions.Projects.Delete";
        }

        public static class Users
        {
            public const string View = "Permissions.Users.View";
            public const string Create = "Permissions.Users.Create";
            public const string Edit = "Permissions.Users.Edit";
            public const string Delete = "Permissions.Users.Delete";
        }

        public static class Roles
        {
            public const string View = "Permissions.Roles.View";
            public const string Create = "Permissions.Roles.Create";
            public const string Edit = "Permissions.Roles.Edit";
            public const string Delete = "Permissions.Roles.Delete";
        }

        public static class Sprints
        {
            public const string View = "Permissions.Sprints.View";
            public const string Create = "Permissions.Sprints.Create";
            public const string Edit = "Permissions.Sprints.Edit";
            public const string Delete = "Permissions.Sprints.Delete";
        }

        public static class Tasks
        {
            public const string View = "Permissions.Tasks.View";
            public const string Create = "Permissions.Tasks.Create";
            public const string Edit = "Permissions.Tasks.Edit";
            public const string Delete = "Permissions.Tasks.Delete";
        }
        
        public static List<string> GenerateAllPermissions()
        {
            return new List<string>
            {
                Projects.View, Projects.Create, Projects.Edit, Projects.Delete,
                Users.View, Users.Create, Users.Edit, Users.Delete,
                Roles.View, Roles.Create, Roles.Edit, Roles.Delete,
                Sprints.View, Sprints.Create, Sprints.Edit, Sprints.Delete,
                Tasks.View, Tasks.Create, Tasks.Edit, Tasks.Delete
            };
        }
    }
}
