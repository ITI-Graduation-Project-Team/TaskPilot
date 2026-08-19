using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.ProjectPolicies;
using TaskPilot.Models.Common.Results;
using TaskPilot.Presentation.Controllers;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Tests;

public sealed class ProjectPoliciesAuthorizationTests
{
    [Theory]
    [InlineData("ProjectManager", true)]
    [InlineData("Employee", false)]
    public async Task Ask_PassesUploadPermissionFromAuthenticatedRole(
        string role,
        bool expectedCanUpload)
    {
        var service = new Mock<IProjectPolicyService>();
        service
            .Setup(x => x.AskAsync(
                It.IsAny<ProjectPolicyQuestionRequest>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ProjectPolicyAnswerResponse
            {
                Answer = "answer"
            }));

        var controller = new ProjectPoliciesController(
            service.Object,
            Mock.Of<IUnitOfWork>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Role, role) },
                        authenticationType: "Test"))
                }
            }
        };

        await controller.Ask(
            new ProjectPolicyQuestionRequest
            {
                ProjectId = Guid.NewGuid(),
                Question = "What is the policy?"
            },
            CancellationToken.None);

        service.Verify(x => x.AskAsync(
            It.IsAny<ProjectPolicyQuestionRequest>(),
            expectedCanUpload,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Upload_IsRestrictedToProjectManagers()
    {
        var authorize = typeof(ProjectPoliciesController)
            .GetMethod(nameof(ProjectPoliciesController.Upload))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("ProjectManager", authorize.Roles);
    }
}
