using FreeTeachClass.Application.Ai;
using FreeTeachClass.Application.Services;
using FreeTeachClass.Application.Submissions;
using Microsoft.Extensions.DependencyInjection;

namespace FreeTeachClass.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers Application-layer services. Infrastructure/UI register their own parts.</summary>
    public static IServiceCollection AddFreeTeachClassApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IClassroomSyncService, ClassroomSyncService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IAiAssistantService, AiAssistantService>();
        services.AddSingleton<IAssignmentSubmissionService, AssignmentSubmissionService>();
        return services;
    }
}
