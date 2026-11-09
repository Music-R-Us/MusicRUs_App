namespace MusicRUs.Models;

public sealed class PostDraft
{
	public MusicLinkType MusicType { get; set; } = MusicLinkType.Album;

	public List<int> GenreIds { get; set; } = [];

	[System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter a post title.")]
	[System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "The title must be at least 8 characters long.")]
	public string Title { get; set; } = string.Empty;

	[System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter post content.")]
	[System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "The post content must be at least 8 characters long.")]
	public string Body { get; set; } = string.Empty;

	public string Genre { get; set; } = string.Empty;

	public string TagsText { get; set; } = string.Empty;

	public bool RequiresMusicSelection { get; set; } = true;

	public MusicSelection Selection { get; set; } = new();
}
