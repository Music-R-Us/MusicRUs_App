using System.Text.Json;
using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class MusicCatalogService
{
	private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new(JsonSerializerDefaults.Web);
	private readonly BackendApiClient apiClient;

	public MusicCatalogService(BackendApiClient apiClient)
	{
		this.apiClient = apiClient;
	}

	public async Task<MusicArtist?> SearchArtistAsync(string name, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var results = await SearchArtistsAsync(name, currentUser, cancellationToken);
		return results.FirstOrDefault();
	}

	public async Task<List<MusicArtist>> SearchArtistsAsync(string keywords, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || string.IsNullOrWhiteSpace(keywords))
		{
			return [];
		}

		return await apiClient.GetAsync<List<MusicArtist>>(
			$"api/Artist/search?keyword={Uri.EscapeDataString(keywords.Trim())}",
			currentUser?.AccessToken, cancellationToken) ?? [];
	}

	public async Task<MusicAlbum?> SearchAlbumAsync(string title, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var results = await SearchAlbumsAsync(title, currentUser, cancellationToken);
		return results.FirstOrDefault();
	}

	public async Task<List<MusicAlbum>> SearchAlbumsAsync(string keywords, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || string.IsNullOrWhiteSpace(keywords))
		{
			return [];
		}

		return await apiClient.GetAsync<List<MusicAlbum>>(
			$"api/Album/search?keyword={Uri.EscapeDataString(keywords.Trim())}",
			currentUser?.AccessToken, cancellationToken) ?? [];
	}

	public async Task<MusicArtist?> GetArtistAsync(int artistId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || artistId <= 0)
		{
			return null;
		}

		return await apiClient.GetAsync<MusicArtist>($"api/Artist/{artistId}", currentUser?.AccessToken, cancellationToken);
	}

	public Task<List<MusicAlbum>> GetArtistAlbumsAsync(int artistId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		return GetAlbumsByArtistAsync(artistId, currentUser, cancellationToken);
	}

	public async Task<List<MusicAlbum>> GetAlbumsByArtistAsync(int artistId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || artistId <= 0)
		{
			return [];
		}

		var page = await apiClient.GetAsync<MusicAlbumPage>($"api/Album/artist/{artistId}?page=1&pageSize=50", currentUser?.AccessToken, cancellationToken);
		if (page?.Items is { Count: > 0 })
		{
			return page.Items;
		}

		return await apiClient.GetAsync<List<MusicAlbum>>($"api/Album/artist/{artistId}", currentUser?.AccessToken, cancellationToken) ?? [];
	}

	public async Task<List<MusicTrack>> GetAlbumTracksAsync(int albumId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || albumId <= 0)
		{
			return [];
		}

		var album = await apiClient.GetAsync<MusicAlbum>(
			$"api/Album/{albumId}", currentUser?.AccessToken, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (album is null)
		{
			throw new HttpRequestException("Could not load the album's tracks.");
		}

		return album.Tracks ?? [];
	}

	public async Task<List<MusicCollectionItem>> GetCollectionAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || userId <= 0)
		{
			return [];
		}

		return await apiClient.GetAsync<List<MusicCollectionItem>>($"api/MusicCollection/user/{userId}", currentUser?.AccessToken, cancellationToken) ?? [];
	}

	public async Task<bool> AddToCollectionAsync(int userId, int albumId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || userId <= 0 || albumId <= 0)
		{
			return false;
		}

		return await apiClient.PostAsync($"api/MusicCollection/user/{userId}/album/{albumId}", currentUser?.AccessToken, cancellationToken);
	}

	public async Task<bool> RemoveFromCollectionAsync(int userId, int albumId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || userId <= 0 || albumId <= 0)
		{
			return false;
		}

		return await apiClient.DeleteAsync($"api/MusicCollection/user/{userId}/album/{albumId}", currentUser?.AccessToken, cancellationToken);
	}

	public async Task<List<MusicAlbum>> GetCollectionAlbumsAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || userId <= 0)
		{
			return [];
		}

		var content = await apiClient.GetStringAsync($"api/MusicCollection/user/{userId}", currentUser?.AccessToken, cancellationToken);
		if (string.IsNullOrWhiteSpace(content))
		{
			return [];
		}

		try
		{
			using var document = JsonDocument.Parse(content);
			return await ReadMusicCollectionAlbumsAsync(document.RootElement, currentUser, cancellationToken);
		}
		catch
		{
			return [];
		}
	}

	public async Task<MusicAlbum?> GetAlbumByIdAsync(int albumId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var album = await apiClient.GetAsync<MusicAlbum>($"api/Album/{albumId}", currentUser?.AccessToken, cancellationToken);
		return album ?? new MusicAlbum
		{
			Id = albumId,
			Title = $"Album {albumId}"
		};
	}


	private async Task<List<MusicAlbum>> ReadMusicCollectionAlbumsAsync(JsonElement element, UserSession? currentUser, CancellationToken cancellationToken)
	{
		if (element.ValueKind == JsonValueKind.Array)
		{
			var albums = new List<MusicAlbum>();
			foreach (var item in element.EnumerateArray())
			{
				var album = await ReadMusicCollectionAlbumAsync(item, currentUser, cancellationToken);
				if (album is not null && album.Id > 0 && albums.All(existing => existing.Id != album.Id))
				{
					albums.Add(album);
				}
			}

			return albums;
		}

		if (element.ValueKind == JsonValueKind.Object)
		{
			foreach (var propertyName in new[] { "albums", "musicCollection", "items", "data" })
			{
				if (JsonElementReader.TryGetProperty(element, propertyName, out var nested) && nested.ValueKind == JsonValueKind.Array)
				{
					return await ReadMusicCollectionAlbumsAsync(nested, currentUser, cancellationToken);
				}
			}

			var album = await ReadMusicCollectionAlbumAsync(element, currentUser, cancellationToken);
			return album is null ? [] : [album];
		}

		return [];
	}

	private async Task<MusicAlbum?> ReadMusicCollectionAlbumAsync(JsonElement element, UserSession? currentUser, CancellationToken cancellationToken)
	{
		if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var albumIdFromNumber))
		{
			return await GetAlbumByIdAsync(albumIdFromNumber, currentUser, cancellationToken);
		}

		if (element.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		if (element.TryGetProperty("album", out var nestedAlbum) && nestedAlbum.ValueKind == JsonValueKind.Object)
		{
			var nested = JsonSerializer.Deserialize<MusicAlbum>(nestedAlbum.GetRawText(), CaseInsensitiveJsonOptions);
			if (nested is not null && !string.IsNullOrWhiteSpace(nested.Title))
			{
				return nested;
			}

			var nestedAlbumId = JsonElementReader.GetInt(nestedAlbum, "albumId", "id");
			if (nestedAlbumId is > 0)
			{
				return await GetAlbumByIdAsync(nestedAlbumId.Value, currentUser, cancellationToken);
			}
		}

		var albumId = JsonElementReader.GetInt(element, "albumId", "albumID");
		if (albumId is > 0)
		{
			return await GetAlbumByIdAsync(albumId.Value, currentUser, cancellationToken);
		}

		var album = JsonSerializer.Deserialize<MusicAlbum>(element.GetRawText(), CaseInsensitiveJsonOptions);
		if (album is not null && !string.IsNullOrWhiteSpace(album.Title))
		{
			return album;
		}

		return album?.Id is > 0
			? await GetAlbumByIdAsync(album.Id, currentUser, cancellationToken)
			: null;
	}
}
