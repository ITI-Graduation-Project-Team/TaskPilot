using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common;
using TaskPilot.Presentation.Controllers;
using TaskPilot.Services.Interfaces;
using Xunit;

namespace TaskPilot.Tests.Controllers
{
    public class ProjectsControllerLifecycleTests
    {
        private readonly Mock<IProjectService> _projectServiceMock;
        private readonly Mock<IProjectTeamService> _projectTeamServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly ProjectsController _controller;

        public ProjectsControllerLifecycleTests()
        {
            _projectServiceMock = new Mock<IProjectService>();
            _projectTeamServiceMock = new Mock<IProjectTeamService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _controller = new ProjectsController(
                _projectServiceMock.Object,
                _projectTeamServiceMock.Object,
                _unitOfWorkMock.Object
            );
        }

        [Fact]
        public async Task UpdateStatus_Success_CallsSaveChanges()
        {
            var projectId = Guid.NewGuid();
            var request = new ProjectStatusUpdateRequest { Status = Models.Enums.ProjectStatus.Active };
            
            _projectServiceMock.Setup(s => s.UpdateStatusAsync(projectId, request, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(new ProjectStatusDto()));

            var result = await _controller.UpdateStatus(projectId, request, CancellationToken.None);

            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task UpdateStatus_Failure_DoesNotCallSaveChanges()
        {
            var projectId = Guid.NewGuid();
            var request = new ProjectStatusUpdateRequest { Status = Models.Enums.ProjectStatus.Active };
            
            _projectServiceMock.Setup(s => s.UpdateStatusAsync(projectId, request, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Failure<ProjectStatusDto>(new Error("ERR", Models.Common.Errors.ErrorType.Validation, "err")));

            var result = await _controller.UpdateStatus(projectId, request, CancellationToken.None);

            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
