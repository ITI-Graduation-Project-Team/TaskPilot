using Microsoft.AspNetCore.Http;
using System;

namespace TaskPilot.AI.Models.Ingestion
{
    public class DocumentUploadRequest
    {
        public Guid SessionId
        {
            get;
            set;
        }

        public Guid? ProjectId
        {
            get;
            set;
        }

        public bool IsAvailableToContextSummarizer
        {
            get;
            set;
        }
        =
            true;

        public IFormFile File
        {
            get;
            set;
        } = null!;
    }
}
