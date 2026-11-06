using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class MusicCatalogService
{
	private readonly BackendApiClient apiClient;
	private readonly Dictionary<int, MusicArtist> knownArtists = [];

	public string? CollectionError { get; private set; }

	// Stores the API client used for catalog searches and collections.
	public MusicCatalogService(BackendApiClient apiClient)
	{
		this.apiClient = apiClient;
	}

	// Returns the first artist matching the search text.
	public async Task<MusicArtist?> SearchArtistAsync(string name, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var results = await SearchArtistsAsync(name, currentUser, cancellationToken);
		return results.FirstOrDefault();
	}

	// Searches the catalog for artists matching the supplied text.
	public async Task<List<MusicArtist>> SearchArtistsAsync(string keywords, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || string.IsNullOrWhiteSpace(keywords))
		{
			return [];
		}

		var artists = await apiClient.GetAsync<List<MusicArtist>>(
			$"api/Artist/search?keyword={Uri.EscapeDataString(keywords.Trim())}",
			currentUser?.AccessToken, cancellationToken) ?? [];
		foreach (var artist in artists)
		{
			knownArtists[artist.Id] = artist;
		}
		return artists;
	}

	// Loads stored albums first and requests artist details if the discography is missing.
	public async Task<(MusicArtist? Artist, List<MusicAlbum> Albums)> GetArtistDiscographyAsync(
		int artistId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || artistId <= 0)
		{
			return (null, []);
		}

		var albums = await GetAlbumsByArtistAsync(artistId, currentUser, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (albums.Count > 0)
		{
			if (!knownArtists.TryGetValue(artistId, out var artist))
			{
				var info = albums.SelectMany(album => album.Artists)
					.FirstOrDefault(item => (item.ArtistId > 0 ? item.ArtistId : item.Id) == artistId);
				artist = new MusicArtist
				{
					Id = artistId,
					Name = info is null ? $"Artist {artistId}" :
						(string.IsNullOrWhiteSpace(info.ArtistName) ? info.Name : info.ArtistName)
				};
			}
			return (artist, albums);
		}

		// Artist details imports missing albums; wait before reading the stored discography again.
		var fetchedArtist = await GetArtistAsync(artistId, currentUser, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (fetchedArtist is null)
		{
			return (null, []);
		}
		knownArtists[artistId] = fetchedArtist;
		return (fetchedArtist, await GetAlbumsByArtistAsync(artistId, currentUser, cancellationToken));
	}

	// Returns the first album matching the search text.
	public async Task<MusicAlbum?> SearchAlbumAsync(string title, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var results = await SearchAlbumsAsync(title, currentUser, cancellationToken);
		return results.FirstOrDefault();
	}

	// Searches the catalog for albums matching the supplied text.
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

	// Loads catalog details for a valid artist ID.
	public async Task<MusicArtist?> GetArtistAsync(int artistId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || artistId <= 0)
		{
			return null;
		}

		return await apiClient.GetAsync<MusicArtist>($"api/Artist/{artistId}", currentUser?.AccessToken, cancellationToken);
	}

	// Accepts either a paged album response or a plain list from the artist endpoint.
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

	// Loads a track and uses search results to fill in missing album details.
	public async Task<MusicTrack?> GetTrackAsync(int trackId, UserSession? currentUser = null, CancellationToken cancellationToken = default, string? title = null)
	{
		if (!apiClient.CanUseBackend || trackId <= 0) return null;
		var track = await apiClient.GetAsync<MusicTrack>($"api/Track/{trackId}", currentUser?.AccessToken, cancellationToken);
		if (track?.Album is null && !string.IsNullOrWhiteSpace(title))
		{
			// Search includes the album even when the detail endpoint fails to load it.
			var matches = await apiClient.GetAsync<List<MusicTrack>>(
				$"api/Track/search?keyword={Uri.EscapeDataString(title.Trim())}", currentUser?.AccessToken, cancellationToken);
			track = matches?.FirstOrDefault(candidate => candidate.Id == trackId) ?? track;
		}
		return track;
	}

	// Loads the selected album's tracks.
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

	// Loads owned albums and flattens their details for display in collection views.
	public async Task<List<MusicCollectionItem>> GetCollectionAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		CollectionError = null;
		if (!apiClient.CanUseBackend || userId <= 0)
		{
			return [];
		}

		var result = await apiClient.GetWithStatusAsync<List<MusicCollectionItem>>($"api/MusicCollection/owned/{userId}", currentUser?.AccessToken, cancellationToken);
		if (result.Value is null)
		{
			CollectionError = "Could not load your collection. Please try again.";
			return [];
		}

		foreach (var item in result.Value)
		{
			item.AlbumTitle = item.Album?.Title ?? $"Album {item.AlbumId}";
			item.ArtistNames = item.Album?.ArtistName ?? string.Empty;
			item.ImageUrl = item.Album?.ImageUrl ?? string.Empty;
			item.AddedAt = item.CreatedAt;
		}

		return result.Value;
	}

	// Adds an album in the selected media format to the user's collection.
	public async Task<bool> AddToCollectionAsync(int userId, int albumId, UserSession? currentUser = null, CancellationToken cancellationToken = default, MediaFormat? format = null)
	{
		CollectionError = null;
		if (!apiClient.CanUseBackend || userId <= 0 || albumId <= 0)
		{
			return false;
		}

		if (format is null || !Enum.IsDefined(format.Value))
		{
			CollectionError = "Choose a media format before adding this album.";
			return false;
		}

		var saved = await apiClient.PostAsync("api/MusicCollection/owned",
			new CreateOwnedMusicCollectionRequest { UserId = userId, AlbumId = albumId, Format = format.Value },
			currentUser?.AccessToken, cancellationToken);
		if (!saved)
		{
			CollectionError = "Could not save this album to your collection. Please try again.";
		}
		return saved;
	}

	// Removes an owned collection entry for the user.
	public async Task<bool> RemoveFromCollectionAsync(int userId, int collectionId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		CollectionError = null;
		if (!apiClient.CanUseBackend || userId <= 0 || collectionId <= 0)
		{
			return false;
		}

		var removed = await apiClient.DeleteAsync($"api/MusicCollection/{userId}/{collectionId}", currentUser?.AccessToken, cancellationToken);
		if (!removed)
		{
			CollectionError = "Could not remove this album from your collection. Please try again.";
		}
		return removed;
	}

	// Applies editor additions and removals against a fresh collection, then verifies the result.
	public async Task<bool> SaveCollectionChangesAsync(int userId, IReadOnlyCollection<MusicCollectionItem> original,
		IReadOnlyCollection<MusicAlbum> desired, IReadOnlyDictionary<int, MediaFormat> formats,
		UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var current = await GetCollectionAsync(userId, currentUser, cancellationToken);
		if (CollectionError is not null || !apiClient.CanUseBackend || userId <= 0) return false;
		var desiredIds = desired.Select(album => album.Id).ToHashSet();
		var currentIds = current.Select(item => item.AlbumId).ToHashSet();
		var additions = new Dictionary<int, MediaFormat>();
		foreach (var albumId in desiredIds.Except(currentIds))
		{
			var previous = original.FirstOrDefault(item => item.AlbumId == albumId);
			var format = formats.TryGetValue(albumId, out var selected) ? selected : previous?.Format;
			if (albumId <= 0 || format is null || !Enum.IsDefined(format.Value))
			{
				CollectionError = "Choose a media format for each new collection album.";
				return false;
			}
			additions[albumId] = format.Value;
		}

		// Remove only entries the user actually loaded and removed in the editor.
		var removals = original.Where(item => !desiredIds.Contains(item.AlbumId)).Select(item => item.Id).ToHashSet();
		foreach (var entry in current.Where(item => removals.Contains(item.Id)))
		{
			if (!await RemoveFromCollectionAsync(userId, entry.Id, currentUser, cancellationToken)) return false;
		}
		foreach (var (albumId, format) in additions)
		{
			if (!await AddToCollectionAsync(userId, albumId, currentUser, cancellationToken, format)) return false;
		}
		if (additions.Count == 0 && !current.Any(item => removals.Contains(item.Id))) return true;

		var saved = await GetCollectionAsync(userId, currentUser, cancellationToken);
		if (CollectionError is not null) return false;
		if (!desiredIds.IsSubsetOf(saved.Select(item => item.AlbumId).ToHashSet()) || saved.Any(item => removals.Contains(item.Id)))
		{
			CollectionError = "Some collection changes could not be confirmed. Your edits are still here; please try again.";
			return false;
		}
		return true;
	}

	// Returns a placeholder with the requested ID if album details cannot be loaded.
	public async Task<MusicAlbum?> GetAlbumByIdAsync(int albumId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var album = await apiClient.GetAsync<MusicAlbum>($"api/Album/{albumId}", currentUser?.AccessToken, cancellationToken);
		return album ?? new MusicAlbum
		{
			Id = albumId,
			Title = $"Album {albumId}"
		};
	}
}
