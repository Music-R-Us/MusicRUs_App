using System.Net.Http.Json;
using MusicRUs.Models;

namespace MusicRUs.Services;

// Turns two-factor authentication on and off for the logged-in user (the API's /api/2fa endpoints)
public sealed class TwoFactorService(HttpClient httpClient, BackendApiClient apiClient, AuthSessionService authSession)
{
	private string? AccessToken => authSession.CurrentUser?.AccessToken;

	// Asks the API whether 2FA is on. Null if the status could not be loaded
	public async Task<bool?> IsEnabledAsync(CancellationToken ct = default)
	{
		var me = await apiClient.GetAsync<CurrentUserStatus>("api/User/me", AccessToken, ct);
		return me?.TwoFactorEnabled;
	}

	// Creates a new secret and returns the QR code to scan. Null if the request fails
	public async Task<TwoFactorSetup?> SetupAsync(CancellationToken ct = default)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, "api/2fa/setup");
			BackendApiClient.AddAuthorizationHeader(request, AccessToken);

			using var response = await httpClient.SendAsync(request, ct);
			return response.IsSuccessStatusCode
				? await response.Content.ReadFromJsonAsync<TwoFactorSetup>(cancellationToken: ct)
				: null;
		}
		catch
		{
			return null;
		}
	}

	// Turns 2FA on. The first code proves the app was set up correctly
	public Task<bool> EnableAsync(string code, CancellationToken ct = default) =>
		apiClient.PostAsync("api/2fa/enable", new { code = code.Trim() }, AccessToken, ct);

	// Turns 2FA off. The API requires a valid code, so a stolen token alone is not enough
	public Task<bool> DisableAsync(string code, CancellationToken ct = default) =>
		apiClient.PostAsync("api/2fa/disable", new { code = code.Trim() }, AccessToken, ct);
}