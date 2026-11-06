using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class PostFeedService
{
	private readonly HttpClient httpClient;
	private readonly BackendApiOptions backendOptions;
	private readonly List<PostItem> posts = [];
	private readonly object gate = new();

	// Stores the HTTP client and settings used for backend and local posts.
	public PostFeedService(HttpClient httpClient, IOptions<BackendApiOptions> backendApiOptions)
	{
		this.httpClient = httpClient;
		backendOptions = backendApiOptions.Value;
	}

	// Loads the backend feed, falling back to in-memory posts when it is unavailable.
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

	// Looks up a post in the backend, then falls back to the in-memory list.
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

	// Validates the draft and saves a backend post, or an in-memory post when backend use is disabled.
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
			if (user.UserId <= 0)
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

	// Attempts the backend write when possible, then adds the comment to the local post.
	public async Task AddCommentAsync(PostItem post, UserSession? user, string body, CancellationToken cancellationToken = default)
	{
		var request = new CreateCommentRequest
		{
			Author = ResolveDisplayName(user),
			Body = body.Trim()
		};

		if (CanUseBackend && post.PostId > 0 && user?.UserId > 0)
		{
			var forumRequest = new CreateForumCommentRequest
			{
				PostId = post.PostId,
				UserId = user.UserId,
				Content = request.Body
			};
			await TryPostToApiAsync("api/Comment", forumRequest, user.AccessToken, cancellationToken);
		}

		AddLocalComment(post, request.Author, request.Body, user);
	}

	// Deletes an owned post from the backend when enabled, then removes it locally.
	public async Task<bool> DeletePostAsync(PostItem post, UserSession? user, CancellationToken cancellationToken = default)
	{
		if (!IsOwnPost(post, user))
		{
			return false;
		}

		if (CanUseBackend && post.PostId > 0 && user is not null)
		{
			var urls = new[]
			{
				$"api/ForumPost/{post.PostId}",
				$"api/ForumPost/deleteForumPost/{post.PostId}",
				$"api/ForumPost/user/{user.UserId}/post/{post.PostId}"
			};

			if (await TryDeleteFromAnyApiAsync(urls, user.AccessToken, cancellationToken))
			{
				RemoveLocalPost(post);
				return true;
			}
		}

		return RemoveLocalPost(post);
	}

	// Deletes an owned comment from the backend when enabled, then removes it locally.
	public async Task<bool> DeleteCommentAsync(PostItem post, CommentItem comment, UserSession? user, CancellationToken cancellationToken = default)
	{
		if (!IsOwnComment(comment, user))
		{
			return false;
		}

		if (CanUseBackend && comment.CommentId > 0 && user is not null)
		{
			var urls = new[]
			{
				$"api/Comment/{comment.CommentId}",
				$"api/Comment/{post.PostId}/{comment.CommentId}",
				$"api/Comment/post/{post.PostId}/comment/{comment.CommentId}"
			};

			if (await TryDeleteFromAnyApiAsync(urls, user.AccessToken, cancellationToken))
			{
				RemoveLocalComment(post, comment);
				return true;
			}
		}

		return RemoveLocalComment(post, comment);
	}

	// Adds a comment to the local post and updates its comment count.
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

	// Removes the matching post from the in-memory feed.
	private bool RemoveLocalPost(PostItem post)
	{
		lock (gate)
		{
			return posts.RemoveAll(item => IsPostMatch(item, post.RouteId)) > 0;
		}
	}

	// Removes the matching local comment and updates the post's comment count.
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

	// Uses a display name, username, or anonymous label for the comment author.
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

	// Pages backend comments when available, otherwise uses comments already held locally.
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

	// Loads backend posts and their comment counts, returning null if loading fails.
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

	// Loads one backend post and its comment count, returning null if loading fails.
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

	// Creates a backend post and reports server validation errors without discarding the draft.
	private async Task<PostItem> TryPostForumPostAsync(CreateForumPostRequest requestBody, string author, string accessToken, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, "api/ForumPost")
		{
			Content = JsonContent.Create(requestBody)
		};
		BackendApiClient.AddAuthorizationHeader(request, accessToken);
		using var response = await httpClient.SendAsync(request, cancellationToken);
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			Console.Error.WriteLine($"POST api/ForumPost failed (HTTP {(int)response.StatusCode}): {content}");
			var message = $"Could not save the post (HTTP {(int)response.StatusCode}). Your draft has been kept.";
			try
			{
				using var error = JsonDocument.Parse(content);
				var detail = JsonElementReader.GetString(error.RootElement, "error", "detail", "message", "title");
				if (!string.IsNullOrWhiteSpace(detail)) message += $" {detail}";
				if (JsonElementReader.TryGetProperty(error.RootElement, "errors", out var errors)
					&& errors.ValueKind == JsonValueKind.Object)
				{
					foreach (var field in errors.EnumerateObject())
					{
						if (field.Value.ValueKind != JsonValueKind.Array) continue;
						foreach (var validationError in field.Value.EnumerateArray())
						{
							if (validationError.ValueKind == JsonValueKind.String)
								message += $" {field.Name}: {validationError.GetString()}";
						}
					}
				}
			}
			catch (JsonException) { }
			throw new InvalidOperationException(message);
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

	// Tries both supported comment routes and filters out comments belonging to other posts.
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

	// Sends an authenticated JSON POST and returns false if it fails.
	private async Task<bool> TryPostToApiAsync<TRequest>(string relativeUrl, TRequest body, string accessToken, CancellationToken cancellationToken)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
			{
				Content = JsonContent.Create(body)
			};
			BackendApiClient.AddAuthorizationHeader(request, accessToken);
			using var response = await httpClient.SendAsync(request, cancellationToken);
			return response.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	// Tries the supplied DELETE routes until one succeeds.
	private async Task<bool> TryDeleteFromAnyApiAsync(IEnumerable<string> relativeUrls, string accessToken, CancellationToken cancellationToken)
	{
		foreach (var relativeUrl in relativeUrls)
		{
			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Delete, relativeUrl);
				BackendApiClient.AddAuthorizationHeader(request, accessToken);
				using var response = await httpClient.SendAsync(request, cancellationToken);
				if (response.IsSuccessStatusCode)
				{
					return true;
				}
			}
			catch
			{
				continue;
			}
		}

		return false;
	}

	// Sorts comments newest first and returns the requested page with its total count.
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

	// Splits comma-separated tags, trims them, and removes blanks and duplicates.
	private static List<string> SplitTags(string value) => value
		.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		.Where(tag => !string.IsNullOrWhiteSpace(tag))
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.ToList();

	// Places the current user's posts from the last three days first, then sorts by newest.
	private static IReadOnlyList<PostItem> SortPosts(IEnumerable<PostItem> sourcePosts, UserSession? currentUser)
	{
		var highlightCutoff = DisplayTime.Now.AddDays(-3);
		return sourcePosts
			.OrderByDescending(post => IsOwnPost(post, currentUser) && post.CreatedAtUtc >= highlightCutoff)
			.ThenByDescending(post => post.CreatedAtUtc)
			.ToList();
	}

	// Loads each backend post's comments to refresh its comments and count.
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

	// Checks whether the current user wrote the post within the last three days.
	public static bool IsRecentOwnPost(PostItem post, UserSession? currentUser)
	{
		return IsOwnPost(post, currentUser) && post.CreatedAtUtc >= DisplayTime.Now.AddDays(-3);
	}

	// Checks ownership using the user's ID or matching username.
	public static bool IsOwnPost(PostItem post, UserSession? currentUser)
	{
		if (currentUser is null)
		{
			return false;
		}

		return currentUser.UserId > 0 && post.UserId == currentUser.UserId
			|| !string.IsNullOrWhiteSpace(currentUser.Username) && string.Equals(post.Author, currentUser.Username, StringComparison.OrdinalIgnoreCase);
	}

	// Checks comment ownership using the user's ID or matching username.
	public static bool IsOwnComment(CommentItem comment, UserSession? currentUser)
	{
		if (currentUser is null)
		{
			return false;
		}

		return currentUser.UserId > 0 && comment.UserId == currentUser.UserId
			|| !string.IsNullOrWhiteSpace(currentUser.Username) && string.Equals(comment.Author, currentUser.Username, StringComparison.OrdinalIgnoreCase);
	}

	// Supports numeric backend IDs and GUIDs used by locally created posts.
	private static bool IsPostMatch(PostItem post, string postKey)
	{
		return string.Equals(post.RouteId, postKey, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(post.Id.ToString(), postKey, StringComparison.OrdinalIgnoreCase)
			|| (int.TryParse(postKey, out var postId) && post.PostId == postId);
	}

	// Matches comments by backend comment ID or local GUID.
	private static bool IsCommentMatch(CommentItem comment, CommentItem target)
	{
		return comment.CommentId > 0 && comment.CommentId == target.CommentId
			|| comment.Id == target.Id;
	}
}
