using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.CompanyPolicies;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;
using Xunit;

namespace TaskPilot.Tests.CompanyPolicies
{
    public class CompanyPolicyServiceTests
    {
        private readonly Mock<IRepository<Company>> _companyRepositoryMock;
        private readonly Mock<IRepository<Policy>> _policyRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IVectorStore> _vectorStoreMock;
        private readonly Mock<ChunkingAgent> _chunkingAgentMock;
        private readonly Mock<DocumentCategorizationAgent> _categorizationAgentMock;
        private readonly Mock<IDocumentTextExtractor> _extractorMock;
        private readonly Mock<KnowledgeOrchestrator> _knowledgeOrchestratorMock;
        private readonly Mock<ILogger<CompanyPolicyService>> _loggerMock;
        private readonly Mock<IFileStorageService> _fileStorageMock;
        private readonly CompanyPolicyService _service;

        public CompanyPolicyServiceTests()
        {
            _companyRepositoryMock = new Mock<IRepository<Company>>();
            _policyRepositoryMock = new Mock<IRepository<Policy>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _vectorStoreMock = new Mock<IVectorStore>();
            _chunkingAgentMock = new Mock<ChunkingAgent>();
            _categorizationAgentMock = new Mock<DocumentCategorizationAgent>(MockBehavior.Loose, new object[] { null!, null! });
            _extractorMock = new Mock<IDocumentTextExtractor>();
            _knowledgeOrchestratorMock = new Mock<KnowledgeOrchestrator>(MockBehavior.Loose, new object[] { null!, null!, null!, null! });
            _loggerMock = new Mock<ILogger<CompanyPolicyService>>();
            _fileStorageMock = new Mock<IFileStorageService>();

            _extractorMock.Setup(e => e.CanHandle(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            _extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync("Extracted Text Content");

            _service = new CompanyPolicyService(
                new List<IDocumentTextExtractor> { _extractorMock.Object },
                _categorizationAgentMock.Object,
                new ChunkingAgent(), // Use real agent since it has no dependencies and cannot be mocked
                _vectorStoreMock.Object,
                _knowledgeOrchestratorMock.Object,
                _policyRepositoryMock.Object,
                _companyRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _fileStorageMock.Object,
                _loggerMock.Object);
        }

        private IngestCompanyPolicyRequest CreateRequest(Guid companyId, string fileName, string content)
        {
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(stream.Length);
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");

            return new IngestCompanyPolicyRequest
            {
                CompanyId = companyId,
                File = fileMock.Object,
                SkipCloudUpload = true // Prevent actual upload mocked out
            };
        }

        private T SetId<T>(T entity, Guid id) where T : class
        {
            typeof(TaskPilot.Models.Common.BaseEntity<Guid>).GetProperty("Id")!.SetValue(entity, id);
            return entity;
        }

        [Fact]
        public async Task UploadAsync_FirstUpload_IncrementsVersionNumberAndCallsSaveChanges()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            var request = CreateRequest(companyId, "Policy1.pdf", "Content 1");

            _companyRepositoryMock.Setup(c => c.GetByIdAsync(companyId)).ReturnsAsync(SetId(new Company(), companyId));
            _policyRepositoryMock.Setup(p => p.FindSingleAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Policy, bool>>>())).ReturnsAsync((Policy?)null);
            _policyRepositoryMock.Setup(p => p.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Policy, bool>>>()))
                .ReturnsAsync(new List<Policy>());

            bool saveChangesCalled = false;
            Func<CancellationToken, Task> saveChangesDelegate = ct => { saveChangesCalled = true; return Task.CompletedTask; };

            Policy? savedPolicy = null;
            _policyRepositoryMock.Setup(p => p.AddAsync(It.IsAny<Policy>())).Callback<Policy>(p => savedPolicy = p).Returns(Task.CompletedTask);

            // Act
            var result = await _service.IngestAsync(request, saveChangesDelegate, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(saveChangesCalled);
            Assert.NotNull(savedPolicy);
            Assert.Equal(1, savedPolicy!.VersionNumber);
            _vectorStoreMock.Verify(v => v.UpsertAsync(KnowledgeCollectionType.CompanyPolicies, It.IsAny<List<TaskPilot.AI.Models.Ingestion.KnowledgeChunk>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UploadAsync_DuplicateUpload_ReturnsExistingWithoutRegeneratingOrSaving()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            var request = CreateRequest(companyId, "Policy1.pdf", "Extracted Text Content");

            _companyRepositoryMock.Setup(c => c.GetByIdAsync(companyId)).ReturnsAsync(SetId(new Company(), companyId));

            using var md5Doc = System.Security.Cryptography.MD5.Create();
            var hashInput = $"{companyId}_Extracted Text Content";
            var documentId = new Guid(md5Doc.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashInput)));

            var existingPolicy = SetId(new Policy { DocumentPublicId = documentId.ToString() }, documentId);
            _policyRepositoryMock.Setup(p => p.FindSingleAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Policy, bool>>>()))
                .ReturnsAsync(existingPolicy);

            bool saveChangesCalled = false;
            Func<CancellationToken, Task> saveChangesDelegate = ct => { saveChangesCalled = true; return Task.CompletedTask; };

            // Act
            var result = await _service.IngestAsync(request, saveChangesDelegate, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(saveChangesCalled); // Should bypass!
            _vectorStoreMock.Verify(v => v.UpsertAsync(It.IsAny<KnowledgeCollectionType>(), It.IsAny<List<TaskPilot.AI.Models.Ingestion.KnowledgeChunk>>(), It.IsAny<CancellationToken>()), Times.Never);
            _policyRepositoryMock.Verify(p => p.AddAsync(It.IsAny<Policy>()), Times.Never);
            Assert.Equal(0, result.Value.ChunksCreated);
            Assert.Equal(documentId, result.Value.DocumentId);
        }

        [Fact]
        public async Task UploadAsync_SecondUploadDifferentDocument_IncrementsVersionNumber()
        {
            // Arrange
            var companyId = Guid.NewGuid();
            var request = CreateRequest(companyId, "Policy2.pdf", "Extracted Text Content");

            _companyRepositoryMock.Setup(c => c.GetByIdAsync(companyId)).ReturnsAsync(SetId(new Company(), companyId));
            _policyRepositoryMock.Setup(p => p.FindSingleAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Policy, bool>>>())).ReturnsAsync((Policy?)null);
            
            // Return an existing policy with Version = 1
            _policyRepositoryMock.Setup(p => p.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Policy, bool>>>()))
                .ReturnsAsync(new List<Policy> { new Policy { VersionNumber = 1 } });

            Policy? savedPolicy = null;
            _policyRepositoryMock.Setup(p => p.AddAsync(It.IsAny<Policy>())).Callback<Policy>(p => savedPolicy = p).Returns(Task.CompletedTask);

            // Act
            var result = await _service.IngestAsync(request, ct => Task.CompletedTask, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(savedPolicy);
            Assert.Equal(2, savedPolicy!.VersionNumber);
        }
    }
}
