using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class ForumProfileService
{
	private readonly BackendApiClient apiClient;

	public ForumProfileService(BackendApiClient apiClient)
	{
		this.apiClient = apiClient;
	}

	public async Task<ForumProfile?> GetProfileAsync(string username, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(username))
		{
			return null;
		}

		var normalizedUsername = username.Trim();
		if (currentUser is not null && string.Equals(currentUser.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase))
		{
			return await GetForumProfileAsync(currentUser.UserId, currentUser, cancellationToken);
		}

		if (apiClient.CanUseBackend)
		{
			var profiles = await apiClient.GetAsync<List<ForumProfile>>(
				$"api/ForumProfile/searchForumProfiles?keyword={Uri.EscapeDataString(normalizedUsername)}",
				currentUser?.AccessToken, cancellationToken);

			// Search can return partial matches, and display names are not unique.
			// Resolve only an unambiguous exact match to avoid showing another user.
			var matches = profiles?
				.Where(profile => profile.UserId > 0
					&& (string.Equals(profile.Username?.Trim(), normalizedUsername, StringComparison.OrdinalIgnoreCase)
						|| string.Equals(profile.DisplayName?.Trim(), normalizedUsername, StringComparison.OrdinalIgnoreCase)))
				.Select(profile => profile.UserId)
				.Distinct()
				.Take(2)
				.ToArray();
			if (matches is { Length: 1 })
			{
				return await GetForumProfileAsync(matches[0], currentUser, cancellationToken);
			}
		}

		return null;
	}

	public async Task<ForumProfile?> GetForumProfileAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (userId <= 0 || !apiClient.CanUseBackend)
		{
			return null;
		}

		var profileFromApi = await apiClient.GetAsync<ForumProfile>($"api/ForumProfile/user/{userId}", currentUser?.AccessToken, cancellationToken);
		if (profileFromApi is not null)
		{
            return profileFromApi;
        }

		return profileFromApi;
	}

	public async Task<bool> IsForumProfileSetupCompleteAsync(UserSession user, CancellationToken cancellationToken = default)
	{
		if (user.UserId <= 0 || !apiClient.CanUseBackend)
		{
			return false;
		}

		var profileFromApi = await apiClient.GetAsync<ForumProfile>($"api/ForumProfile/user/{user.UserId}", user.AccessToken, cancellationToken);
		return IsConfiguredForumProfile(profileFromApi, user);
	}

	public async Task<bool> SaveForumProfileAsync(ForumProfile profile, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (profile.UserId <= 0)
		{
			profile.UserId = currentUser?.UserId ?? 0;
		}

		if (profile.UserId <= 0 || !apiClient.CanUseBackend)
		{
			return false;
		}

		profile.UpdatedAt = DateTimeOffset.UtcNow;
		if (profile.CreatedAt == default)
		{
			profile.CreatedAt = profile.UpdatedAt;
		}

		var existingProfile = await apiClient.GetAsync<ForumProfile>($"api/ForumProfile/user/{profile.UserId}", currentUser?.AccessToken, cancellationToken);
		var backendProfile = existingProfile;
		if (backendProfile is not null)
		{
			profile.Id = backendProfile.Id;
			profile.UserId = backendProfile.UserId;
			profile.CreatedAt = backendProfile.CreatedAt;
		}

		var savedProfile = await UpdateForumProfileAsync(profile, currentUser, cancellationToken);
		if (savedProfile is null)
		{
			return false;
		}

		profile.Id = savedProfile.Id;
		profile.UserId = savedProfile.UserId;
		profile.CreatedAt = savedProfile.CreatedAt;
		profile.UpdatedAt = savedProfile.UpdatedAt;

		return true;
	}

	public async Task EnsureForumProfileAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || userId <= 0)
		{
			return;
		}
	}

	private async Task<ForumProfile?> UpdateForumProfileAsync(ForumProfile profile, UserSession? currentUser, CancellationToken cancellationToken)
	{
		var request = new UpdateForumProfileRequest
		{
			DisplayName = EmptyToNull(profile.DisplayName),
			Bio = EmptyToNull(profile.Bio),
			AvatarUrl = EmptyToNull(profile.AvatarUrl),
			Location = EmptyToNull(profile.Location),
			FavoriteGenre = profile.FavoriteGenre.Trim(),
			Tags = profile.Tags.ToList(),
			WebsiteUrl = EmptyToNull(profile.WebsiteUrl),
			IsPublic = profile.IsPublic,
			PrimaryColor = EmptyToNull(profile.PrimaryColor),
			AccentColor = EmptyToNull(profile.AccentColor),
			UpdatedAt = profile.UpdatedAt
		};

		if (!await apiClient.PatchAsync($"api/ForumProfile/user/{profile.UserId}", request, currentUser?.AccessToken, cancellationToken))
		{
			return null;
		}

		return await apiClient.GetAsync<ForumProfile>($"api/ForumProfile/user/{profile.UserId}", currentUser?.AccessToken, cancellationToken);
	}


	private static bool IsConfiguredForumProfile(ForumProfile? profile, UserSession user)
	{
		if (profile is null || profile.UserId <= 0)
		{
			return false;
		}

		return IsMeaningfulProfileValue(profile.DisplayName, user.DisplayName, user.Username)
			|| IsMeaningfulProfileValue(profile.Bio)
			|| IsMeaningfulProfileValue(profile.AvatarUrl, user.AvatarUrl)
			|| IsMeaningfulProfileValue(profile.Location)
			|| IsMeaningfulProfileValue(profile.WebsiteUrl);
	}

	private static bool IsMeaningfulProfileValue(string? value, params string[] defaultValues)
	{
		if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "string", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return !defaultValues.Any(defaultValue => string.Equals(value.Trim(), defaultValue, StringComparison.OrdinalIgnoreCase));
	}

	private static string? EmptyToNull(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
