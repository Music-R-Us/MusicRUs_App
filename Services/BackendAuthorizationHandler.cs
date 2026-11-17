using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MusicRUs.Services;

/// <summary>Attaches the session token and expires rejected backend sessions.</summary>
public sealed class BackendAuthorizationHandler(IServiceProvider services, Uri backendBaseAddress) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Never send a session token to another origin or outside the configured API path.
        if (request.RequestUri is not { } uri || !backendBaseAddress.IsBaseOf(uri))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var path = backendBaseAddress.MakeRelativeUri(uri).OriginalString.Split('?', '#')[0].Trim('/');
        if (!path.StartsWith("api/", StringComparison.OrdinalIgnoreCase) || IsLoginRequest(path))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Resolve at send time: the session service itself depends on this HttpClient.
        var session = services.GetRequiredService<AuthSessionService>();
        await session.InitializeAsync();
        if (request.Headers.Authorization is null && !string.IsNullOrWhiteSpace(session.CurrentUser?.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.CurrentUser.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            request.Headers.Authorization is { Scheme: "Bearer", Parameter: { } token } &&
            await session.ExpireSessionAsync(token))
        {
            services.GetRequiredService<NavigationManager>().NavigateTo("/loginpick");
        }

        return response;
    }

    private static bool IsLoginRequest(string path) =>
        path.Equals("api/User", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("api/User/login/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("api/google-auth/login", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("api/2fa/verify", StringComparison.OrdinalIgnoreCase);
}
