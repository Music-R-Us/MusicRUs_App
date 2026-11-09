namespace MusicRUs.Models;

public sealed class CommentPage
{
	public IReadOnlyList<CommentItem> Comments { get; init; } = [];

	public int TotalCount { get; init; }

	public int Page { get; init; }

	public int PageSize { get; init; }

	public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

	// Creates an empty comment page with the requested page number and size.
	public static CommentPage Empty(int page, int pageSize) => new()
	{
		Page = page,
		PageSize = pageSize
	};
}
