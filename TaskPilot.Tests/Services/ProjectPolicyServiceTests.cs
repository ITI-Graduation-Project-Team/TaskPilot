using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.ProjectPolicies;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class ProjectPolicyServiceTests
    {
        private readonly Mock<IEnumerable<IDocumentTextExtractor>> _extractorsMock;
        private readonly Mock<DocumentCategorizationAgent> _categorizationAgentMock;
        private readonly Mock<ChunkingAgent> _chunkingAgentMock;
        private readonly Mock<IVectorStore> _vectorStoreMock;
        private readonly Mock<KnowledgeOrchestrator> _knowledgeOrchestratorMock;
        private readonly Mock<IRepository<Policy>> _policyRepoMock;
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IFileStorageService> _fileStorageMock;
        private readonly Mock<ILogger<ProjectPolicyService>> _loggerMock;

        public ProjectPolicyServiceTests()
        {
            _extractorsMock = new Mock<IEnumerable<IDocumentTextExtractor>>();
            _categorizationAgentMock = new Mock<DocumentCategorizationAgent>(null, null, null);
            _chunkingAgentMock = new Mock<ChunkingAgent>(null, null);
            _vectorStoreMock = new Mock<IVectorStore>();
            _knowledgeOrchestratorMock = new Mock<KnowledgeOrchestrator>(null, null, null, null);
            _policyRepoMock = new Mock<IRepository<Policy>>();
            _projectRepoMock = new Mock<IRepository<Project>>();
            _fileStorageMock = new Mock<IFileStorageService>();
            _loggerMock = new Mock<ILogger<ProjectPolicyService>>();
        }

        private ProjectPolicyService CreateService()
        {
            return new ProjectPolicyService(
                _extractorsMock.Object,
                _categorizationAgentMock.Object,
                _chunkingAgentMock.Object,
                _vectorStoreMock.Object,
                _knowledgeOrchestratorMock.Object,
                _policyRepoMock.Object,
                _projectRepoMock.Object,
                _fileStorageMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task AskAsync_WithAmbiguousIdentifier_ReturnsFailure()
        {
            // Arrange
            var service = CreateService();
            var request = new ProjectPolicyQuestionRequest
            {
                ProjectId = Guid.NewGuid(),
                RequirementSessionId = Guid.NewGuid(),
                Question = "What is the policy?"
            };

            // Act
            var result = await service.AskAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(KnowledgeErrors.AmbiguousTenantIdentifier, result.Error);
        }

        [Fact]
        public async Task AskAsync_WithNoIdentifier_ReturnsFailure()
        {
            // Arrange
            var service = CreateService();
            var request = new ProjectPolicyQuestionRequest
            {
                Question = "What is the policy?"
            };

            // Act
            var result = await service.AskAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(KnowledgeErrors.MissingProjectPolicyIdentifier, result.Error);
        }

        [Fact]
        public async Task AskAsync_WithEmptyQuestion_ReturnsFailure()
        {
            // Arrange
            var service = CreateService();
            var request = new ProjectPolicyQuestionRequest
            {
                ProjectId = Guid.NewGuid(),
                Question = "   "
            };

            // Act
            var result = await service.AskAsync(request);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
        }
        
        [Fact]
        public async Task DeleteAsync_QdrantFails_DoesNotDeleteSQL()
        {
            // Arrange
            var service = CreateService();
            var projectId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var policy = new Policy { ProjectId = projectId, DocumentId = docId, Scope = PolicyScope.Project, CloudinaryPublicId = "test" };

            _projectRepoMock.Setup(x => x.GetByIdAsync(projectId)).ReturnsAsync(new Project());
            _policyRepoMock.Setup(x => x.FindSingleAsync(It.IsAny<Expression<Func<Policy, bool>>>(), It.IsAny<string[]>())).ReturnsAsync(policy);

            _fileStorageMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>())).ReturnsAsync(TaskPilot.Models.Common.Results.Result.Success());
            
            // Simulate Qdrant failure
            _vectorStoreMock.Setup(x => x.DeleteAsync(It.IsAny<KnowledgeCollectionType>(), docId, null, projectId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TaskPilot.Models.Common.Results.Result.Failure(CommonErrors.ServerError("Qdrant failed")));

            // Act
            var result = await service.DeleteAsync(docId, projectId);

            // Assert
            Assert.True(result.IsFailure);
            _policyRepoMock.Verify(x => x.Delete(It.IsAny<Policy>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_CloudinaryFails_DeletesSQL()
        {
            // Arrange
            var service = CreateService();
            var projectId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var policy = new Policy { ProjectId = projectId, DocumentId = docId, Scope = PolicyScope.Project, CloudinaryPublicId = "test" };

            _projectRepoMock.Setup(x => x.GetByIdAsync(projectId)).ReturnsAsync(new Project());
            _policyRepoMock.Setup(x => x.FindSingleAsync(It.IsAny<Expression<Func<Policy, bool>>>(), It.IsAny<string[]>())).ReturnsAsync(policy);

            // Simulate Cloudinary failure
            _fileStorageMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>())).ReturnsAsync(TaskPilot.Models.Common.Results.Result.Failure(CommonErrors.ServerError("Cloudinary failed")));
            
            // Simulate Qdrant success
            _vectorStoreMock.Setup(x => x.DeleteAsync(It.IsAny<KnowledgeCollectionType>(), docId, null, projectId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TaskPilot.Models.Common.Results.Result.Success());

            // Act
            var result = await service.DeleteAsync(docId, projectId);

            // Assert
            Assert.True(result.IsSuccess);
            _policyRepoMock.Verify(x => x.Delete(policy), Times.Once);
        }
    }
}
