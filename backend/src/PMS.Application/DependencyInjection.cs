using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Services;
using PMS.Application.Features.ActivityLogs;
using PMS.Application.Features.Admin;
using PMS.Application.Features.Attachments;
using PMS.Application.Features.BoardColumns;
using PMS.Application.Features.Auth;
using PMS.Application.Features.Comments;
using PMS.Application.Features.Employees;
using PMS.Application.Features.Labels;
using PMS.Application.Features.Notifications;
using PMS.Application.Features.Projects;
using PMS.Application.Features.Sprints;
using PMS.Application.Features.Statistics;
using PMS.Application.Features.TaskLinks;
using PMS.Application.Features.Tasks;
using PMS.Application.Features.Watchers;

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
        services.AddScoped<IBoardColumnService, BoardColumnService>();
        services.AddScoped<ITaskStatusTransitionService, TaskStatusTransitionService>();
        services.AddScoped<ITaskAssignmentService, TaskAssignmentService>();
        services.AddSingleton<TaskMapper>();
        services.AddScoped<IActivityLogger, ActivityLogger>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationFeedService, NotificationFeedService>();
        services.AddSingleton<NotificationMapper>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddSingleton<CommentMapper>();
        services.AddSingleton<ProjectMapper>();

        // Đợt 2026-08-03: Label / Watcher / TaskLink / ActivityLog đọc.
        // ⚠️ Đăng ký ở đây là THỦ CÔNG — service và mapper mới quên dòng này thì hỏng lúc
        // chạy chứ không phải lúc biên dịch. Validator thì không cần: dòng
        // AddValidatorsFromAssembly bên dưới quét cả assembly.
        services.AddScoped<ILabelService, LabelService>();
        services.AddSingleton<LabelMapper>();
        services.AddScoped<IWatcherService, WatcherService>();
        services.AddSingleton<WatcherMapper>();
        services.AddScoped<ITaskLinkService, TaskLinkService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddSingleton<ActivityLogMapper>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddSingleton<AttachmentMapper>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IDueDateNotifier, DueDateNotifier>();

        // Đợt 2026-08-04: phân quyền tầng 1 lưu trong DB (ADR-045).
        services.AddScoped<IRolePermissionAdminService, RolePermissionAdminService>();
        services.AddScoped<IEmployeeLookupService, EmployeeLookupService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}