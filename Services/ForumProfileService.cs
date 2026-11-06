using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class ForumProfileService
{
	private readonly BackendApiClient apiClient;
	private readonly Dictionary<(int UserId, string? Token), (Task<ForumProfile?> Load, DateTimeOffset Expires)> profileReads = [];

	// Stores the API client used to read and save forum profiles.
	public ForumProfileService(BackendApiClient apiClient)
	{
		this.apiClient = apiClient;
	}

	// Finds an unambiguous exact username or display-name match and loads that user's profile.
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

	// Shares profile requests for ten seconds, separately for each user ID and access token.
	public async Task<ForumProfile?> GetForumProfileAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (userId <= 0 || !apiClient.CanUseBackend)
		{
			return null;
		}

		var key = (userId, currentUser?.AccessToken);
		if (!profileReads.TryGetValue(key, out var cached) || cached.Expires <= DateTimeOffset.UtcNow)
		{
			cached = (apiClient.GetAsync<ForumProfile>($"api/ForumProfile/user/{userId}", currentUser?.AccessToken, cancellationToken), DateTimeOffset.UtcNow.AddSeconds(10));
			profileReads[key] = cached;
		}
		var profile = await cached.Load;
		if (profile is null && profileReads.TryGetValue(key, out var latest) && latest.Load == cached.Load)
		{
			profileReads.Remove(key);
		}
		return profile;
	}

	// Checks whether the user has personalized their forum profile.
	public async Task<bool> IsForumProfileSetupCompleteAsync(UserSession user, CancellationToken cancellationToken = default)
	{
		if (user.UserId <= 0 || !apiClient.CanUseBackend)
		{
			return false;
		}

		var profileFromApi = await GetForumProfileAsync(user.UserId, user, cancellationToken);
		return IsConfiguredForumProfile(profileFromApi, user);
	}

	// Creates or updates the profile, then reloads it to confirm favorite artists were saved.
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

		var existing = await apiClient.GetWithStatusAsync<ForumProfile>($"api/ForumProfile/user/{profile.UserId}", currentUser?.AccessToken, cancellationToken);
		var backendProfile = existing.Value;
		// Only a confirmed missing profile should trigger creation.
		if (backendProfile is null && existing.StatusCode != System.Net.HttpStatusCode.NotFound)
		{
			return false;
		}
		if (backendProfile is not null)
		{
			profile.Id = backendProfile.Id;
			profile.UserId = backendProfile.UserId;
			profile.CreatedAt = backendProfile.CreatedAt;
		}

		var saved = backendProfile is null
			? await CreateForumProfileAsync(profile, currentUser, cancellationToken)
			: await UpdateForumProfileAsync(profile, currentUser, cancellationToken);
		if (!saved)
		{
			return false;
		}

		foreach (var key in profileReads.Keys.Where(key => key.UserId == profile.UserId).ToList())
		{
			profileReads.Remove(key);
		}
		var savedProfile = await GetForumProfileAsync(profile.UserId, currentUser, cancellationToken);
		if (savedProfile is null)
		{
			return false;
		}
		var expectedArtists = profile.FavoriteArtists.Select(artist => artist.Id).Where(id => id > 0).ToHashSet();
		var savedArtists = savedProfile.FavoriteArtists.Select(artist => artist.Id).Where(id => id > 0).ToHashSet();
		if (!expectedArtists.SetEquals(savedArtists))
		{
			Console.Error.WriteLine("Profile update returned success, but reloading the profile did not return the requested favorite artist IDs.");
			return false;
		}
		if (savedProfile is not null)
		{
			profile.Id = savedProfile.Id;
			profile.CreatedAt = savedProfile.CreatedAt;
			profile.UpdatedAt = savedProfile.UpdatedAt;
		}

		return true;
	}

	// Currently a no-op; profile creation is handled when the setup form is saved.
	public async Task EnsureForumProfileAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || userId <= 0)
		{
			return;
		}
	}

	// Builds the create-profile payload with favorites and genres, then posts it.
	private Task<bool> CreateForumProfileAsync(ForumProfile profile, UserSession? currentUser, CancellationToken cancellationToken)
	{
		var request = new
		{
			Id = 0,
			profile.UserId,
			DisplayName = profile.DisplayName.Trim(),
			Bio = profile.Bio.Trim(),
			AvatarUrl = profile.AvatarUrl.Trim(),
			Location = profile.Location.Trim(),
			WebsiteUrl = profile.WebsiteUrl.Trim(),
			profile.PrimaryColor,
			profile.AccentColor,
			profile.IsPublic,
			profile.CreatedAt,
			profile.UpdatedAt,
			FavoriteArtists = profile.FavoriteArtists.Where(artist => artist.Id > 0).DistinctBy(artist => artist.Id).ToList(),
			FavoriteAlbums = profile.FavoriteAlbums.Where(album => album.Id > 0).DistinctBy(album => album.Id)
				.Select(album => new
				{
					album.Id, album.SpotifyId, album.Title,
					// The create contract uses the numeric album-type enum.
					AlbumType = int.TryParse(album.AlbumType, out var albumType) ? albumType : 0,
					album.ReleaseDate, TotalTracks = album.TotalTracks ?? 0, album.ImageUrl, album.SpotifyUrl
				}).ToList(),
			Genres = profile.Genres.Where(genre => genre.Id > 0).DistinctBy(genre => genre.Id).ToList()
		};

		return apiClient.PostAsync($"api/ForumProfile/CreateForumProfile/{profile.UserId}", request, currentUser?.AccessToken, cancellationToken);
	}

	// Builds a profile update with favorite and genre IDs, then sends it as a PATCH.
	private Task<bool> UpdateForumProfileAsync(ForumProfile profile, UserSession? currentUser, CancellationToken cancellationToken)
	{
		var request = new UpdateForumProfileRequest
		{
			DisplayName = EmptyToNull(profile.DisplayName),
			Bio = EmptyToNull(profile.Bio),
			AvatarUrl = EmptyToNull(profile.AvatarUrl),
			Location = EmptyToNull(profile.Location),
			FavoriteArtistIds = profile.FavoriteArtists.Select(artist => artist.Id).Where(id => id > 0).Distinct().ToList(),
			FavoriteAlbumIds = profile.FavoriteAlbums.Select(album => album.Id).Where(id => id > 0).Distinct().ToList(),
			GenreIds = profile.Genres.Select(genre => genre.Id).Where(id => id > 0).Distinct().ToList(),
			WebsiteUrl = EmptyToNull(profile.WebsiteUrl),
			IsPublic = profile.IsPublic,
			PrimaryColor = EmptyToNull(profile.PrimaryColor),
			AccentColor = EmptyToNull(profile.AccentColor),
			UpdatedAt = profile.UpdatedAt
		};

		return apiClient.PatchAsync($"api/ForumProfile/user/{profile.UserId}", request, currentUser?.AccessToken, cancellationToken);
	}


	// Treats a profile as configured when at least one personal field differs from its defaults.
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

	// Rejects blank, placeholder, or default values when checking profile customization.
	private static bool IsMeaningfulProfileValue(string? value, params string[] defaultValues)
	{
		if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "string", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return !defaultValues.Any(defaultValue => string.Equals(value.Trim(), defaultValue, StringComparison.OrdinalIgnoreCase));
	}

	// Trims nonblank text and converts empty values to null.
	private static string? EmptyToNull(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
