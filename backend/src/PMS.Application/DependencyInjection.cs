using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Common.Authorization;
using PMS.Application.Features.Auth;
using PMS.Application.Features.Projects;

namespace PMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<EmployeeMapper>();
        services.AddScoped<IProjectAuthorizationService, ProjectAuthorizationService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddSingleton<ProjectMapper>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}