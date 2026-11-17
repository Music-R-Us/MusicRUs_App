namespace MusicRUs.Models;

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

	public bool IsBlockedByUser { get; set; }

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
