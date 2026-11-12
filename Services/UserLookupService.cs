using System.Text.Json;
using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class UserLookupService
{
	private readonly BackendApiClient apiClient;

	/// <summary>
	/// Stores the API client used to look up user details.
	/// </summary>
	public UserLookupService(BackendApiClient apiClient)
	{
		this.apiClient = apiClient;
	}

	/// <summary>
	/// Maps public user details into a session-shaped model without assigning a login token.
	/// </summary>
	public async Task<UserSession?> GetUserAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || userId <= 0)
		{
			return null;
		}

		var content = await apiClient.GetStringAsync($"api/User/{userId}", currentUser?.AccessToken, cancellationToken);
		if (string.IsNullOrWhiteSpace(content))
		{
			return null;
		}

		try
		{
			using var document = JsonDocument.Parse(content);
			var root = document.RootElement;
			var username = JsonElementReader.GetString(root, "username", "userName");
			var displayName = JsonElementReader.GetString(root, "displayName", "fullName", "name");
			var avatarUrl = JsonElementReader.GetString(root, "avatarUrl", "profilePictureUrl", "picture");
			var resolvedUserId = JsonElementReader.GetInt(root, "userId", "id") ?? userId;
			return string.IsNullOrWhiteSpace(username)
				? null
				: UserSession.Create(username, displayName, avatarUrl, userId: resolvedUserId);
		}
		catch
		{
			return null;
		}
	}
}
