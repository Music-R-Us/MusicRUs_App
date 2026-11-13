using System.Text.Json;
using MusicRUs.Models;

namespace MusicRUs.Services;

internal static class PostFeedMapper
{
	/// <summary>
	/// Accepts a bare post array or one wrapped in a supported API list property.
	/// </summary>
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

	/// <summary>
	/// Maps alternate API field names and nested music details into the UI post model.
	/// </summary>
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
			TrackId = JsonElementReader.GetInt(element, "trackId"),
			AlbumId = JsonElementReader.GetInt(element, "albumId"),
			ArtistId = JsonElementReader.GetInt(element, "artistId"),
			UpdatedAtUtc = GetJsonDate(element, "updatedAt", "updatedAtUtc"),
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

		if (JsonElementReader.TryGetProperty(element, "genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
		{
			post.Genres = genres.EnumerateArray()
				.Where(genre => genre.ValueKind == JsonValueKind.Object)
				.Select(genre => new GenreOption
				{
					Id = JsonElementReader.GetInt(genre, "id") ?? 0,
					Name = JsonElementReader.GetString(genre, "name") ?? string.Empty
				})
				.ToList();
			post.Tags = post.Genres.Select(genre => genre.Name)
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			post.Genre = string.Join(", ", post.Tags);
		}

		post.Artist = ReadMusicName(element, "artist", "name", "artistName") ?? post.Artist;
		post.ArtistImageUrl = ReadMusicName(element, "artist", "imageUrl") ?? string.Empty;
		post.Album = ReadMusicName(element, "album", "title", "name") ?? post.Album;
		post.AlbumImageUrl = ReadMusicName(element, "album", "imageUrl") ?? string.Empty;
		post.Song = ReadMusicName(element, "track", "title", "name") ?? post.Song;
		if (JsonElementReader.TryGetProperty(element, "track", out var track) && track.ValueKind == JsonValueKind.Object)
		{
			post.TrackId ??= JsonElementReader.GetInt(track, "id");
			post.TrackAlbumId = JsonElementReader.GetInt(track, "albumId");
			post.TrackImageUrl = JsonElementReader.GetString(track, "imageUrl") ?? string.Empty;
			if (JsonElementReader.TryGetProperty(track, "album", out var album) && album.ValueKind == JsonValueKind.Object)
			{
				post.TrackAlbumId ??= JsonElementReader.GetInt(album, "id");
				if (string.IsNullOrWhiteSpace(post.TrackImageUrl))
					post.TrackImageUrl = JsonElementReader.GetString(album, "imageUrl") ?? string.Empty;
			}
			if (JsonElementReader.TryGetProperty(track, "artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
			{
				post.TrackArtistName = string.Join(", ", artists.EnumerateArray()
					.Select(artist => JsonElementReader.GetString(artist, "name", "artistName"))
					.Where(name => !string.IsNullOrWhiteSpace(name)));
			}
		}

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

	/// <summary>
	/// Reads a named field from a nested artist, album, or track object.
	/// </summary>
	private static string? ReadMusicName(JsonElement element, string property, params string[] names)
	{
		return JsonElementReader.TryGetProperty(element, property, out var music)
			&& music.ValueKind == JsonValueKind.Object
			? JsonElementReader.GetString(music, names)
			: null;
	}

	/// <summary>
	/// Normalizes comment response shapes and field names for display.
	/// </summary>
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

	/// <summary>
	/// Reads tags from a supported list response and removes blank or duplicate names.
	/// </summary>
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

	/// <summary>
	/// Reads a tag supplied as either a string or an object.
	/// </summary>
	private static string? ReadPostTag(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Object => JsonElementReader.GetString(element, "tag", "genre", "name", "value"),
			_ => null
		};
	}

	/// <summary>
	/// Finds a bare array or an array stored under one of the supplied property names.
	/// </summary>
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

	/// <summary>
	/// Returns the first valid GUID found under the supplied property names.
	/// </summary>
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

	/// <summary>
	/// Returns the first valid timestamp found under the supplied property names.
	/// </summary>
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

	/// <summary>
	/// Reads a string array or splits a comma-separated string into tags.
	/// </summary>
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

	/// <summary>
	/// Splits comma-separated tags, trims them, and removes blanks and duplicates.
	/// </summary>
	private static List<string> SplitTags(string value) => value
		.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		.Where(tag => !string.IsNullOrWhiteSpace(tag))
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.ToList();
}
