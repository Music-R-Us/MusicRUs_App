namespace MusicRUs.Models;

public sealed class LoginRequest
{
	public string Username { get; set; } = string.Empty;

	public string Password { get; set; } = string.Empty;
}

public sealed class SignupRequest
{
	public string Username { get; set; } = string.Empty;

	public string Email { get; set; } = string.Empty;

	public string Password { get; set; } = string.Empty;

}

public sealed class GoogleLoginRequest
{
	public string IdToken { get; set; } = string.Empty;
}

public sealed class AuthResponse
{
	public UserSession User { get; set; } = new();

	public string AccessToken { get; set; } = string.Empty;
}
public sealed class ForumProfile
{
	public int Id { get; set; }
	public int UserId { get; set; }
	public string Username { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Bio { get; set; } = string.Empty;
	public string AvatarUrl { get; set; } = string.Empty;
	public string Location { get; set; } = string.Empty;
	public string WebsiteUrl { get; set; } = string.Empty;
	public bool IsPublic { get; set; } = true;
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserSession
{
	public int UserId { get; set; }

	public string Username { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string AvatarUrl { get; set; } = string.Empty;

	public string AvatarInitial { get; set; } = string.Empty;

	public string LastActivityUtc { get; set; } = string.Empty;

	public string AccessToken { get; set; } = string.Empty;

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


public sealed class UpdateForumProfileRequest
{
	public string? DisplayName { get; set; }

	public string? Bio { get; set; }

	public string? AvatarUrl { get; set; }

	public string? Location { get; set; }

	public string? FavoriteGenre { get; set; }

	public string? WebsiteUrl { get; set; }

	public bool? IsPublic { get; set; }

	public DateTimeOffset? UpdatedAt { get; set; }

	public List<string>? Tags { get; set; }
}

public sealed class BackendApiOptions
{
	public bool UseBackendApi { get; set; }

	public string BaseUrl { get; set; } = string.Empty;
}
