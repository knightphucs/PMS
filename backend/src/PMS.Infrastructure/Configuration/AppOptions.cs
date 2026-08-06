namespace PMS.Infrastructure.Configuration;

/// <summary>
/// Cấu hình cấp ứng dụng không thuộc riêng một hạ tầng cụ thể nào (khác <see cref="Security.JwtOptions"/>,
/// <see cref="Storage.FileStorageOptions"/>). Hiện chỉ có <see cref="FrontendBaseUrl"/>, dùng để dựng
/// link trong email (ví dụ link chấp nhận lời mời project).
/// </summary>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Origin của frontend, KHÔNG có dấu <c>/</c> ở cuối — ví dụ <c>https://pms-six-gamma.vercel.app</c>.
    /// Production nạp qua biến môi trường <c>App__FrontendBaseUrl</c> (không có
    /// <c>appsettings.Production.json</c> trong repo).
    /// </summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;
}
