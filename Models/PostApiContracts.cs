namespace MusicRUs.Models;

public sealed class CreatePostRequest
{
	public string Title { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public string Genre { get; set; } = string.Empty;

	public string Artist { get; set; } = string.Empty;

	public string Album { get; set; } = string.Empty;

	public string Song { get; set; } = string.Empty;

	public List<string> Tags { get; set; } = [];

	public string Author { get; set; } = string.Empty;
}

public sealed class CreateForumPostRequest
{
	public int UserId { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Content { get; set; } = string.Empty;

	public int? TrackId { get; set; }

	public int? AlbumId { get; set; }

	public int? ArtistId { get; set; }
}

public sealed class CreatePostTagRequest
{
	public int PostId { get; set; }

	public string Genre { get; set; } = string.Empty;

	public string Tag { get; set; } = string.Empty;
}

public sealed class CreateCommentRequest
{
	public string Author { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;
}

public sealed class CreateForumCommentRequest
{
	public int PostId { get; set; }

	public int UserId { get; set; }

	public string Content { get; set; } = string.Empty;
}

public sealed class CommentPage
{
	public IReadOnlyList<CommentItem> Comments { get; init; } = [];

	public int TotalCount { get; init; }

	public int Page { get; init; }

	public int PageSize { get; init; }

	public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

	public static CommentPage Empty(int page, int pageSize) => new()
	{
		Page = page,
		PageSize = pageSize
	};
}
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

	public string DisplayName { get; set; } = string.Empty;
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

	public string FavoriteGenre { get; set; } = string.Empty;

	public string WebsiteUrl { get; set; } = string.Empty;

	public bool IsPublic { get; set; } = true;

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	public List<string> Tags { get; set; } = [];
	public string PrimaryColor { get; set; } = "#2E0B05";
	public string AccentColor { get; set; } = "#B82A14";

	public List<MusicArtist> FavoriteArtists { get; set; } = [];

	public List<MusicAlbum> FavoriteAlbums { get; set; } = [];

	public List<GenreOption> Genres { get; set; } = [];

	public List<MusicAlbum> MusicCollection { get; set; } = [];

	public int PostCount { get; set; }
}

public sealed class MusicArtist
{
	public int Id { get; set; }

	public string SpotifyId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string ImageUrl { get; set; } = string.Empty;

	public string SpotifyUrl { get; set; } = string.Empty;
}

public sealed class MusicAlbum
{
	public int Id { get; set; }

	public string SpotifyId { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	[System.Text.Json.Serialization.JsonConverter(typeof(AlbumTypeJsonConverter))]
	public string AlbumType { get; set; } = string.Empty;

	public string ReleaseDate { get; set; } = string.Empty;

	public int? TotalTracks { get; set; }

	public string ImageUrl { get; set; } = string.Empty;

	public string SpotifyUrl { get; set; } = string.Empty;

	public List<AlbumArtistInfo> Artists { get; set; } = [];

	// Convenience helpers populated from Artists list
	public int ArtistId => Artists.FirstOrDefault()?.ArtistId ?? 0;

	public string ArtistName => string.Join(", ", Artists.Select(a => a.ArtistName).Where(n => !string.IsNullOrWhiteSpace(n)));
}
public sealed class GenreOption
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;
}
public sealed class AlbumArtistInfo
{
	public int ArtistId { get; set; }

	public string ArtistName { get; set; } = string.Empty;
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

public sealed class CreateProfileFavoriteAlbumRequest
{
	public int ProfileId { get; set; }

	public int AlbumId { get; set; }

	public int DisplayOrder { get; set; }
}

public sealed class ProfileFavoriteArtistResponse
{
	public int ProfileId { get; set; }

	public int ArtistId { get; set; }

	public int DisplayOrder { get; set; }
}

public sealed class ProfileFavoriteAlbumResponse
{
	public int ProfileId { get; set; }

	public int AlbumId { get; set; }

	public int DisplayOrder { get; set; }
}

public sealed class UpdateForumProfileRequest
{
	public string? DisplayName { get; set; }

	public string? Bio { get; set; }

	public string? AvatarUrl { get; set; }

	public string? Location { get; set; }

	public string? WebsiteUrl { get; set; }

	public bool? IsPublic { get; set; }
	public string? PrimaryColor { get; set; }
	public string? AccentColor { get; set; }

	public DateTimeOffset? UpdatedAt { get; set; }

	public List<int> FavoriteArtistIds { get; set; } = [];

	public List<int> FavoriteAlbumIds { get; set; } = [];

	public List<int> GenreIds { get; set; } = [];
}

public sealed class BackendApiOptions
{
	public bool UseBackendApi { get; set; }

	public string BaseUrl { get; set; } = string.Empty;
}

public sealed class MusicPagedResult<T>
{
	public List<T> Items { get; set; } = [];

	public int TotalCount { get; set; }

	public int Page { get; set; }

	public int PageSize { get; set; }

	public int TotalPages { get; set; }
}

public sealed class MusicTrack
{
	public int Id { get; set; }

	public string SpotifyId { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public int? AlbumId { get; set; }

	public int? TrackNumber { get; set; }

	public int? DiscNumber { get; set; }

	public int? DurationMs { get; set; }

	public bool? Explicit { get; set; }

	public string PreviewUrl { get; set; } = string.Empty;

	public string SpotifyUrl { get; set; } = string.Empty;

	public string DurationFormatted => DurationMs is > 0
		? $"{DurationMs.Value / 60000}:{(DurationMs.Value % 60000) / 1000:D2}"
		: string.Empty;
}

public sealed class MusicCollectionItem
{
	public int Id { get; set; }

	public int UserId { get; set; }

	public int AlbumId { get; set; }

	public DateTimeOffset? AddedAt { get; set; }

	public string AlbumTitle { get; set; } = string.Empty;

	public string ArtistNames { get; set; } = string.Empty;

	public string ImageUrl { get; set; } = string.Empty;
}

public sealed class MusicAlbumPage
{
	public List<MusicAlbum> Items { get; set; } = [];

	public int TotalCount { get; set; }

	public int Page { get; set; }

	public int PageSize { get; set; }

	public int TotalPages { get; set; }
}

public sealed class CreateProfileFavoriteArtistRequest
{
	public int ProfileId { get; set; }

	public int ArtistId { get; set; }

	public int DisplayOrder { get; set; }
}
