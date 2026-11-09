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
