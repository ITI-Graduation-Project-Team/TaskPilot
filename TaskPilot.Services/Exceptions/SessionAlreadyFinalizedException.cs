using System;

namespace TaskPilot.Services.Exceptions
{
    public class SessionAlreadyFinalizedException : Exception
    {
        public Guid? ProjectId { get; }
        
        public SessionAlreadyFinalizedException(Guid? projectId) 
            : base("This session has already been finalized into a project.")
        {
            ProjectId = projectId;
        }
    }
}
