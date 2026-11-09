namespace MusicRUs.Models;

public sealed class MusicCollectionItem
{
	[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<MediaFormat>))]
	public MediaFormat Format { get; set; }

	public MusicAlbum? Album { get; set; }

	public DateTimeOffset? CreatedAt { get; set; }

	public int Id { get; set; }

	public int UserId { get; set; }

	public int AlbumId { get; set; }

	public DateTimeOffset? AddedAt { get; set; }

	public string AlbumTitle { get; set; } = string.Empty;

	public string ArtistNames { get; set; } = string.Empty;

	public string ImageUrl { get; set; } = string.Empty;
}
