using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Features.Auth;

namespace PMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<EmployeeMapper>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}