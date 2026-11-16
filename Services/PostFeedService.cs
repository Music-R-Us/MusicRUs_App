using System.Text.Json;
using Microsoft.Extensions.Options;
using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class PostFeedService
{
	private readonly HttpClient httpClient;
	private readonly BackendApiClient apiClient;
	private readonly BackendApiOptions backendOptions;
	private readonly List<PostItem> posts = [];
	private readonly object gate = new();

	/// <summary>
	/// Stores the HTTP client and settings used for backend and local posts.
	/// </summary>
	public PostFeedService(HttpClient httpClient, IOptions<BackendApiOptions> backendApiOptions, BackendApiClient apiClient)
	{
		this.httpClient = httpClient;
		this.apiClient = apiClient;
		backendOptions = backendApiOptions.Value;
	}

	/// <summary>
	/// Loads the backend feed, falling back to in-memory posts when it is unavailable.
	/// </summary>
	public async Task<IReadOnlyList<PostItem>> GetRecentPostsAsync(UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (CanUseBackend)
		{
			var postsFromApi = await TryGetForumPostsAsync(cancellationToken);
			if (postsFromApi is not null)
			{
				return SortPosts(postsFromApi, currentUser);
			}
		}

		lock (gate)
		{
			return SortPosts(posts, currentUser);
		}
	}

	/// <summary>
	/// Looks up a post in the backend, then falls back to the in-memory list.
	/// </summary>
	public async Task<PostItem?> GetPostAsync(string postKey, CancellationToken cancellationToken = default)
	{
		if (CanUseBackend)
		{
			var postFromApi = await TryGetForumPostAsync(postKey, cancellationToken);
			if (postFromApi is not null)
			{
				return postFromApi;
			}
		}

		lock (gate)
		{
			return posts.FirstOrDefault(post => IsPostMatch(post, postKey));
		}
	}

	/// <summary>
	/// Validates the draft and saves a backend post, or an in-memory post when backend use is disabled.
	/// </summary>
	public async Task<PostItem> CreatePostAsync(PostDraft draft, UserSession user, CancellationToken cancellationToken = default)
	{
		if (draft.Title.Trim().Length < 8)
		{
			throw new InvalidOperationException("The title must be at least 8 characters long, excluding leading and trailing spaces.");
		}

		if (draft.Body.Trim().Length < 8)
		{
			throw new InvalidOperationException("The post content must be at least 8 characters long, excluding leading and trailing spaces.");
		}

		var request = new CreatePostRequest
		{
			Title = draft.Title.Trim(),
			Body = draft.Body.Trim(),
			Genre = draft.Genre.Trim(),
			Artist = draft.RequiresMusicSelection ? draft.Selection.Artist.Trim() : string.Empty,
			Album = draft.RequiresMusicSelection ? draft.Selection.Album.Trim() : string.Empty,
			Song = draft.RequiresMusicSelection && draft.MusicType == MusicLinkType.Track ? draft.Selection.Track.Trim() : string.Empty,
			Tags = SplitTags(draft.TagsText),
			Author = string.IsNullOrWhiteSpace(user.Username) ? "anonymous" : user.Username.Trim()
		};

		if (CanUseBackend)
		{
			if (user.UserId <= 0 || string.IsNullOrWhiteSpace(user.AccessToken))
			{
				throw new InvalidOperationException("Please sign out and log in again before posting.");
			}

			var forumRequest = new CreateForumPostRequest
			{
				UserId = user.UserId,
				Title = request.Title,
				Content = request.Body,
				TrackId = draft.RequiresMusicSelection && draft.MusicType == MusicLinkType.Track && draft.Selection.TrackId > 0 ? draft.Selection.TrackId : null,
				AlbumId = draft.RequiresMusicSelection && draft.MusicType == MusicLinkType.Album && draft.Selection.AlbumId > 0 ? draft.Selection.AlbumId : null,
				ArtistId = draft.RequiresMusicSelection && draft.MusicType == MusicLinkType.Artist && draft.Selection.ArtistId > 0 ? draft.Selection.ArtistId : null,
				GenreIds = draft.GenreIds.Where(id => id > 0).Distinct().ToList()
			};
			var postFromApi = await TryPostForumPostAsync(forumRequest, request.Author, user.AccessToken, cancellationToken);
			if (postFromApi is not null)
			{
				if (draft.RequiresMusicSelection && draft.MusicType == MusicLinkType.Track)
				{
					postFromApi.TrackAlbumId ??= draft.Selection.AlbumId > 0 ? draft.Selection.AlbumId : null;
					if (string.IsNullOrWhiteSpace(postFromApi.TrackImageUrl)) postFromApi.TrackImageUrl = draft.Selection.AlbumImageUrl;
				}
				return postFromApi;
			}
		}

		var post = new PostItem
		{
			Id = Guid.NewGuid(),
			UserId = user.UserId,
			Title = request.Title,
			Body = request.Body,
			Genre = request.Genre,
			Artist = request.Artist,
			Album = request.Album,
			Song = request.Song,
			TrackId = draft.RequiresMusicSelection && draft.MusicType == MusicLinkType.Track ? draft.Selection.TrackId : null,
			TrackAlbumId = draft.RequiresMusicSelection && draft.MusicType == MusicLinkType.Track ? draft.Selection.AlbumId : null,
			TrackImageUrl = draft.RequiresMusicSelection && draft.MusicType == MusicLinkType.Track ? draft.Selection.AlbumImageUrl : string.Empty,
			Tags = request.Tags,
			Author = request.Author,
			CreatedAtUtc = DisplayTime.Now
		};

		lock (gate)
		{
			posts.Insert(0, post);
		}

		return post;
	}

	/// <summary>
	/// Attempts the backend write when possible, then adds the comment to the local post.
	/// </summary>
	public async Task AddCommentAsync(PostItem post, UserSession? user, string body, CancellationToken cancellationToken = default)
	{
		if (user is null || user.UserId <= 0 || string.IsNullOrWhiteSpace(user.AccessToken))
		{
			throw new InvalidOperationException("Please log in again before commenting.");
		}

		var request = new CreateCommentRequest
		{
			Author = ResolveDisplayName(user),
			Body = body.Trim()
		};

		if (CanUseBackend && post.PostId > 0)
		{
			var forumRequest = new CreateForumCommentRequest
			{
				Content = request.Body
			};
			if (!await apiClient.PostAsync($"api/Comment/forumpost/{post.PostId}", forumRequest, user.AccessToken, cancellationToken))
			{
				throw new InvalidOperationException("Could not save your comment. Please try again.");
			}
		}

		AddLocalComment(post, request.Author, request.Body, user);
	}

	/// <summary>
	/// Deletes an owned post from the backend when enabled, then removes it locally.
	/// </summary>
	public async Task<bool> DeletePostAsync(PostItem post, UserSession? user, CancellationToken cancellationToken = default)
	{
		if (!IsOwnPost(post, user) || string.IsNullOrWhiteSpace(user?.AccessToken))
		{
			return false;
		}

		if (CanUseBackend && post.PostId > 0 && user is not null)
		{
			if (await apiClient.DeleteAsync($"api/ForumPost/deleteForumPost/{post.PostId}", user.AccessToken, cancellationToken))
			{
				RemoveLocalPost(post);
				return true;
			}
			return false;
		}

		return RemoveLocalPost(post);
	}

	/// <summary>
	/// Deletes an owned comment from the backend when enabled, then removes it locally.
	/// </summary>
	public async Task<bool> DeleteCommentAsync(PostItem post, CommentItem comment, UserSession? user, CancellationToken cancellationToken = default)
	{
		if (!IsOwnComment(comment, user) || string.IsNullOrWhiteSpace(user?.AccessToken))
		{
			return false;
		}

		if (CanUseBackend && comment.CommentId > 0 && user is not null)
		{
			if (await apiClient.DeleteAsync($"api/Comment/{comment.CommentId}", user.AccessToken, cancellationToken))
			{
				RemoveLocalComment(post, comment);
				return true;
			}
			return false;
		}

		return RemoveLocalComment(post, comment);
	}

	/// <summary>
	/// Adds a comment to the local post and updates its comment count.
	/// </summary>
	private void AddLocalComment(PostItem post, string author, string body, UserSession? user)
	{
		lock (gate)
		{
			var savedPost = posts.FirstOrDefault(item => IsPostMatch(item, post.RouteId));
			var targetPost = savedPost ?? post;

			targetPost.Comments.Add(new CommentItem
			{
				Author = author,
				Body = body,
				Comment = body,
				PostId = targetPost.PostId,
				UserId = user?.UserId ?? 0,
				CreatedAtUtc = DisplayTime.Now
			});
			targetPost.CommentCount = targetPost.Comments.Count;
		}
	}

	/// <summary>
	/// Removes the matching post from the in-memory feed.
	/// </summary>
	private bool RemoveLocalPost(PostItem post)
	{
		lock (gate)
		{
			return posts.RemoveAll(item => IsPostMatch(item, post.RouteId)) > 0;
		}
	}

	/// <summary>
	/// Removes the matching local comment and updates the post's comment count.
	/// </summary>
	private bool RemoveLocalComment(PostItem post, CommentItem comment)
	{
		lock (gate)
		{
			var savedPost = posts.FirstOrDefault(item => IsPostMatch(item, post.RouteId)) ?? post;
			var removed = savedPost.Comments.RemoveAll(item => IsCommentMatch(item, comment)) > 0;
			if (removed)
			{
				savedPost.CommentCount = savedPost.Comments.Count;
				post.CommentCount = savedPost.CommentCount;
			}

			return removed;
		}
	}

	/// <summary>
	/// Uses a display name, username, or anonymous label for the comment author.
	/// </summary>
	private static string ResolveDisplayName(UserSession? user)
	{
		if (user is null)
		{
			return "anonymous";
		}

		if (!string.IsNullOrWhiteSpace(user.DisplayName))
		{
			return user.DisplayName.Trim();
		}

		return string.IsNullOrWhiteSpace(user.Username)
			? "anonymous"
			: user.Username.Trim();
	}

	/// <summary>
	/// Pages backend comments when available, otherwise uses comments already held locally.
	/// </summary>
	public async Task<CommentPage> GetCommentsAsync(PostItem post, int page, int pageSize = 10, CancellationToken cancellationToken = default)
	{
		page = Math.Max(1, page);
		pageSize = Math.Max(1, pageSize);

		if (CanUseBackend && post.PostId > 0)
		{
			var commentsFromApi = await TryGetCommentsForPostAsync(post.PostId, cancellationToken);
			if (commentsFromApi is not null && (commentsFromApi.Count > 0 || post.Comments.Count == 0))
			{
				return CreateCommentPage(commentsFromApi, page, pageSize);
			}
		}

		lock (gate)
		{
			var savedPost = posts.FirstOrDefault(item => IsPostMatch(item, post.RouteId)) ?? post;
			return CreateCommentPage(savedPost.Comments, page, pageSize);
		}
	}

	private bool CanUseBackend => backendOptions.UseBackendApi && httpClient.BaseAddress is not null;

	/// <summary>
	/// Loads backend posts and their comment counts, returning null if loading fails.
	/// </summary>
	private async Task<List<PostItem>?> TryGetForumPostsAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var response = await httpClient.GetAsync("api/ForumPost", cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			var content = await response.Content.ReadAsStringAsync(cancellationToken);
			using var document = JsonDocument.Parse(content);
			var posts = PostFeedMapper.ReadPostList(document.RootElement);
			await PopulateCommentCountsAsync(posts, cancellationToken);
			return posts;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Loads one backend post and its comment count, returning null if loading fails.
	/// </summary>
	private async Task<PostItem?> TryGetForumPostAsync(string postKey, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await httpClient.GetAsync($"api/ForumPost/{Uri.EscapeDataString(postKey)}", cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			var content = await response.Content.ReadAsStringAsync(cancellationToken);
			using var document = JsonDocument.Parse(content);
			var post = PostFeedMapper.ReadPost(document.RootElement);
			if (post is not null)
			{
				await PopulateCommentCountsAsync([post], cancellationToken);
			}

			return post;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Creates a backend post and reports server validation errors without discarding the draft.
	/// </summary>
	private async Task<PostItem> TryPostForumPostAsync(CreateForumPostRequest requestBody, string author, string accessToken, CancellationToken cancellationToken)
	{
		var (content, statusCode) = await apiClient.PostWithStatusAsync("api/ForumPost", requestBody, accessToken, cancellationToken);

		if ((int)statusCode is < 200 or >= 300)
		{
			throw new InvalidOperationException($"Could not save the post (HTTP {(int)statusCode}). Your draft has been kept.");
		}

		if (!string.IsNullOrWhiteSpace(content))
		{
			try
			{
				using var document = JsonDocument.Parse(content);
				var post = PostFeedMapper.ReadPost(document.RootElement);
				if (post is not null) return post;
			}
			catch (JsonException) { }
		}

		return new PostItem
		{
			UserId = requestBody.UserId,
			Title = requestBody.Title,
			Body = requestBody.Content,
			Author = author,
			CreatedAtUtc = DisplayTime.Now
		};
	}

	/// <summary>
	/// Tries both supported comment routes and filters out comments belonging to other posts.
	/// </summary>
	private async Task<List<CommentItem>?> TryGetCommentsForPostAsync(int postId, CancellationToken cancellationToken)
	{
		var urls = new[]
		{
			$"api/Comment/forumpost/{postId}",
			$"api/Comment/{postId}"
		};

		foreach (var url in urls)
		{
			try
			{
				using var response = await httpClient.GetAsync(url, cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					continue;
				}

				var content = await response.Content.ReadAsStringAsync(cancellationToken);
				using var document = JsonDocument.Parse(content);
				return PostFeedMapper.ReadCommentList(document.RootElement)
					.Where(comment => comment.PostId == 0 || comment.PostId == postId)
					.ToList();
			}
			catch
			{
				continue;
			}
		}

		return null;
	}

	/// <summary>
	/// Sorts comments newest first and returns the requested page with its total count.
	/// </summary>
	private static CommentPage CreateCommentPage(IEnumerable<CommentItem> comments, int page, int pageSize)
	{
		var orderedComments = comments
			.OrderByDescending(comment => comment.CreatedAtUtc)
			.ToList();

		return new CommentPage
		{
			Comments = orderedComments
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToList(),
			TotalCount = orderedComments.Count,
			Page = page,
			PageSize = pageSize
		};
	}

	/// <summary>
	/// Splits comma-separated tags, trims them, and removes blanks and duplicates.
	/// </summary>
	private static List<string> SplitTags(string value) => value
		.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		.Where(tag => !string.IsNullOrWhiteSpace(tag))
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.ToList();

	/// <summary>
	/// Places the current user's posts from the last three days first, then sorts by newest.
	/// </summary>
	private static IReadOnlyList<PostItem> SortPosts(IEnumerable<PostItem> sourcePosts, UserSession? currentUser)
	{
		var highlightCutoff = DisplayTime.Now.AddDays(-3);
		return sourcePosts
			.OrderByDescending(post => IsOwnPost(post, currentUser) && post.CreatedAtUtc >= highlightCutoff)
			.ThenByDescending(post => post.CreatedAtUtc)
			.ToList();
	}

	/// <summary>
	/// Loads each backend post's comments to refresh its comments and count.
	/// </summary>
	private async Task PopulateCommentCountsAsync(IEnumerable<PostItem> postsToPopulate, CancellationToken cancellationToken)
	{
		foreach (var post in postsToPopulate)
		{
			if (post.PostId <= 0)
			{
				continue;
			}

			var comments = await TryGetCommentsForPostAsync(post.PostId, cancellationToken);
			if (comments is null)
			{
				continue;
			}

			post.Comments = comments;
			post.CommentCount = comments.Count;
		}
	}

	/// <summary>
	/// Checks whether the current user wrote the post within the last three days.
	/// </summary>
	public static bool IsRecentOwnPost(PostItem post, UserSession? currentUser)
	{
		return IsOwnPost(post, currentUser) && post.CreatedAtUtc >= DisplayTime.Now.AddDays(-3);
	}

	/// <summary>
	/// Checks post ownership using the user's ID.
	/// </summary>
	public static bool IsOwnPost(PostItem post, UserSession? currentUser)
	{
		if (currentUser is null)
		{
			return false;
		}

		return currentUser.UserId > 0 && post.UserId == currentUser.UserId;
	}

	/// <summary>
	/// Checks comment ownership using the user's ID.
	/// </summary>
	public static bool IsOwnComment(CommentItem comment, UserSession? currentUser)
	{
		if (currentUser is null)
		{
			return false;
		}

		return currentUser.UserId > 0 && comment.UserId == currentUser.UserId;
	}

	/// <summary>
	/// Supports numeric backend IDs and GUIDs used by locally created posts.
	/// </summary>
	private static bool IsPostMatch(PostItem post, string postKey)
	{
		return string.Equals(post.RouteId, postKey, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(post.Id.ToString(), postKey, StringComparison.OrdinalIgnoreCase)
			|| (int.TryParse(postKey, out var postId) && post.PostId == postId);
	}

	/// <summary>
	/// Matches comments by backend comment ID or local GUID.
	/// </summary>
	private static bool IsCommentMatch(CommentItem comment, CommentItem target)
	{
		return comment.CommentId > 0 && comment.CommentId == target.CommentId
			|| comment.Id == target.Id;
	}
}
