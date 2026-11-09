namespace MusicRUs.Models;

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
