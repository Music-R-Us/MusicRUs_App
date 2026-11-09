namespace MusicRUs.Models;

public sealed class CreatePostTagRequest
{
	public int PostId { get; set; }

	public string Genre { get; set; } = string.Empty;

	public string Tag { get; set; } = string.Empty;
}
