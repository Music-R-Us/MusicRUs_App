using System.Text.Json;
using MusicRUs.Models;

namespace MusicRUs.Services;

internal static class PostFeedMapper
{
	public static List<PostItem> ReadPostList(JsonElement root)
	{
		var array = GetJsonArray(root, "items", "posts", "forumPosts", "$values") ?? root;
		if (array.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		return array.EnumerateArray()
			.Select(ReadPost)
			.Where(post => post is not null)
			.Select(post => post!)
			.ToList();
	}

	public static PostItem? ReadPost(JsonElement element)
	{
		if (element.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		var id = GetJsonGuid(element, "id", "guid") ?? Guid.NewGuid();
		var post = new PostItem
		{
			Id = id,
			PostId = JsonElementReader.GetInt(element, "postId", "forumPostId") ?? (JsonElementReader.GetInt(element, "id") ?? 0),
			UserId = JsonElementReader.GetInt(element, "userId") ?? 0,
			Title = JsonElementReader.GetString(element, "title", "subject") ?? string.Empty,
			Body = JsonElementReader.GetString(element, "body", "content", "post", "text") ?? string.Empty,
			Genre = JsonElementReader.GetString(element, "genre", "genreName") ?? string.Empty,
			Artist = JsonElementReader.GetString(element, "artist", "artistName") ?? string.Empty,
			Album = JsonElementReader.GetString(element, "album", "albumTitle", "albumName") ?? string.Empty,
			Song = JsonElementReader.GetString(element, "song", "songTitle", "trackName") ?? string.Empty,
			Author = JsonElementReader.GetString(element, "author", "username", "userName", "displayName") ?? string.Empty,
			CreatedAtUtc = GetJsonDate(element, "createdAtUtc", "createdAt", "createdDate") ?? DisplayTime.Now,
			CommentCount = JsonElementReader.GetInt(element, "commentCount", "commentsCount") ?? 0,
			Tags = GetJsonStringList(element, "tags")
		};

		var commentsElement = GetJsonArray(element, "comments", "$values");
		if (commentsElement is not null)
		{
			post.Comments = ReadCommentList(commentsElement.Value);
			if (post.CommentCount == 0)
			{
				post.CommentCount = post.Comments.Count;
			}
		}

		return post;
	}

	public static List<CommentItem> ReadCommentList(JsonElement root)
	{
		var array = GetJsonArray(root, "items", "comments", "$values") ?? root;
		if (array.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		return array.EnumerateArray()
			.Where(element => element.ValueKind == JsonValueKind.Object)
			.Select(element => new CommentItem
			{
				Id = GetJsonGuid(element, "id", "guid") ?? Guid.NewGuid(),
				CommentId = JsonElementReader.GetInt(element, "commentId", "id") ?? 0,
				PostId = JsonElementReader.GetInt(element, "postId", "forumPostId") ?? 0,
				UserId = JsonElementReader.GetInt(element, "userId") ?? 0,
				Author = JsonElementReader.GetString(element, "author", "username", "userName", "displayName") ?? string.Empty,
				Body = JsonElementReader.GetString(element, "body", "comment", "content", "text") ?? string.Empty,
				Comment = JsonElementReader.GetString(element, "comment", "body", "content", "text") ?? string.Empty,
				CreatedAtUtc = GetJsonDate(element, "createdAtUtc", "createdAt", "createdDate") ?? DisplayTime.Now
			})
			.ToList();
	}

	public static List<string> ReadPostTagList(JsonElement root)
	{
		var array = GetJsonArray(root, "items", "tags", "postTags", "$values") ?? root;
		if (array.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		return array.EnumerateArray()
			.Select(ReadPostTag)
			.Where(tag => !string.IsNullOrWhiteSpace(tag))
			.Select(tag => tag!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string? ReadPostTag(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Object => JsonElementReader.GetString(element, "tag", "genre", "name", "value"),
			_ => null
		};
	}

	private static JsonElement? GetJsonArray(JsonElement element, params string[] propertyNames)
	{
		if (element.ValueKind == JsonValueKind.Array)
		{
			return element;
		}

		if (element.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		foreach (var propertyName in propertyNames)
		{
			if (JsonElementReader.TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array)
			{
				return value;
			}
		}

		return null;
	}

	private static Guid? GetJsonGuid(JsonElement element, params string[] propertyNames)
	{
		foreach (var propertyName in propertyNames)
		{
			if (JsonElementReader.TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var guid))
			{
				return guid;
			}
		}

		return null;
	}

	private static DateTimeOffset? GetJsonDate(JsonElement element, params string[] propertyNames)
	{
		foreach (var propertyName in propertyNames)
		{
			if (JsonElementReader.TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var date))
			{
				return date;
			}
		}

		return null;
	}

	private static List<string> GetJsonStringList(JsonElement element, params string[] propertyNames)
	{
		foreach (var propertyName in propertyNames)
		{
			if (!JsonElementReader.TryGetProperty(element, propertyName, out var value))
			{
				continue;
			}

			if (value.ValueKind == JsonValueKind.Array)
			{
				return value.EnumerateArray()
					.Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
					.Where(item => !string.IsNullOrWhiteSpace(item))
					.Select(item => item!)
					.ToList();
			}

			if (value.ValueKind == JsonValueKind.String)
			{
				return SplitTags(value.GetString() ?? string.Empty);
			}
		}

		return [];
	}

	private static List<string> SplitTags(string value) => value
		.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		.Where(tag => !string.IsNullOrWhiteSpace(tag))
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.ToList();
}
