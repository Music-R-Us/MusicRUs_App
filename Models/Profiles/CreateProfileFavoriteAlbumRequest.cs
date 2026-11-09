namespace MusicRUs.Models;

public sealed class CreateProfileFavoriteAlbumRequest
{
	public int ProfileId { get; set; }

	public int AlbumId { get; set; }

	public int DisplayOrder { get; set; }
}
