using Microsoft.Extensions.Options;
using PMS.Application.Common.Interfaces;

namespace PMS.Infrastructure.Configuration;

public class AppLinkBuilder : IAppLinkBuilder
{
    private readonly AppOptions _options;

    public AppLinkBuilder(IOptions<AppOptions> options) => _options = options.Value;

    public string BuildInvitationLink(string rawToken)
        => $"{_options.FrontendBaseUrl.TrimEnd('/')}/invitations/{Uri.EscapeDataString(rawToken)}";
}
