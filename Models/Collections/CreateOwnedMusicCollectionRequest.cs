namespace MusicRUs.Models;

public sealed class CreateOwnedMusicCollectionRequest
{
	public int UserId { get; set; }
	public int AlbumId { get; set; }
	public MediaFormat Format { get; set; }
}
