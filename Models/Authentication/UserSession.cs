namespace MusicRUs.Models;

public sealed class UserSession
{
	public int UserId { get; set; }

	public string Username { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string AvatarUrl { get; set; } = string.Empty;

	public string AvatarInitial { get; set; } = string.Empty;

	public string LastActivityUtc { get; set; } = string.Empty;

	public string AccessToken { get; set; } = string.Empty;

	/// <summary>
	/// Normalizes session details and supplies display-name and avatar defaults.
	/// </summary>
	public static UserSession Create(string username, string? displayName = null, string? avatarUrl = null, string? accessToken = null, int userId = 0)
	{
		var normalizedUsername = username.Trim();
		var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedUsername : displayName.Trim();
		if (string.IsNullOrWhiteSpace(resolvedDisplayName))
		{
			resolvedDisplayName = "profile";
		}

		return new UserSession
		{
			UserId = userId,
			Username = normalizedUsername,
			DisplayName = resolvedDisplayName,
			AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? "https://png.pngtree.com/png-vector/20250512/ourmid/pngtree-default-avatar-profile-icon-vector-png-image_16213769.png" : avatarUrl.Trim(),
			AvatarInitial = resolvedDisplayName[0].ToString().ToUpperInvariant(),
			LastActivityUtc = DateTimeOffset.UtcNow.ToString("O"),
			AccessToken = accessToken ?? string.Empty
		};
	}
}
