using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Services;
using PMS.Application.Features.Admin;
using PMS.Application.Features.Auth;
using PMS.Application.Features.Projects;
using PMS.Application.Features.Sprints;
using PMS.Application.Features.Tasks;

namespace PMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmployeeAdminService, EmployeeAdminService>();
        services.AddSingleton<EmployeeAdminMapper>();
        services.AddSingleton<EmployeeMapper>();
        services.AddScoped<IProjectAuthorizationService, ProjectAuthorizationService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();
        services.AddScoped<ISprintService, SprintService>();
        services.AddSingleton<SprintMapper>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ITaskStatusTransitionService, TaskStatusTransitionService>();
        services.AddSingleton<TaskMapper>();
        services.AddScoped<IActivityLogger, ActivityLogger>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<ProjectMapper>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}