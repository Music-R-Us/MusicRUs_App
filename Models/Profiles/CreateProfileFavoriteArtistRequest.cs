namespace MusicRUs.Models;

public sealed class CreateProfileFavoriteArtistRequest
{
	public int ProfileId { get; set; }

	public int ArtistId { get; set; }

	public int DisplayOrder { get; set; }
}
