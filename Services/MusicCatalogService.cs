using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class MusicCatalogService
{
	private readonly BackendApiClient apiClient;
	private readonly Dictionary<int, MusicArtist> knownArtists = [];

	public string? CollectionError { get; private set; }

	/// <summary>
	/// Stores the API client used for catalog searches and collections.
	/// </summary>
	public MusicCatalogService(BackendApiClient apiClient)
	{
		this.apiClient = apiClient;
	}

	/// <summary>
	/// Returns the first artist matching the search text.
	/// </summary>
	public async Task<MusicArtist?> SearchArtistAsync(string name, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var results = await SearchArtistsAsync(name, currentUser, cancellationToken);
		return results.FirstOrDefault();
	}

	/// <summary>
	/// Searches the catalog for artists matching the supplied text.
	/// </summary>
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

	/// <summary>
	/// Loads stored albums and fills in artist details when the cached image is missing.
	/// </summary>
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
			knownArtists.TryGetValue(artistId, out var artist);
			if (string.IsNullOrWhiteSpace(artist?.ImageUrl))
			{
				var details = await GetArtistAsync(artistId, currentUser, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (details is not null)
				{
					artist = details;
					knownArtists[artistId] = details;
				}
			}
			if (artist is null)
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

	/// <summary>
	/// Returns the first album matching the search text.
	/// </summary>
	public async Task<MusicAlbum?> SearchAlbumAsync(string title, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		var results = await SearchAlbumsAsync(title, currentUser, cancellationToken);
		return results.FirstOrDefault();
	}

	/// <summary>
	/// Searches the catalog for albums matching the supplied text.
	/// </summary>
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

	/// <summary>
	/// Loads catalog details for a valid artist ID.
	/// </summary>
	public async Task<MusicArtist?> GetArtistAsync(int artistId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		if (!apiClient.CanUseBackend || artistId <= 0)
		{
			return null;
		}

		return await apiClient.GetAsync<MusicArtist>($"api/Artist/{artistId}", currentUser?.AccessToken, cancellationToken);
	}

	/// <summary>
	/// Accepts either a paged album response or a plain list from the artist endpoint.
	/// </summary>
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

	/// <summary>
	/// Loads a track and uses search results to fill in missing album details.
	/// </summary>
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

	/// <summary>
	/// Loads the selected album's tracks.
	/// </summary>
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

	/// <summary>
	/// Loads the selected owned or wanted list and flattens their details for display in collection views.
	/// </summary>
	/// <param name="wanted">True selects the wanted list; false selects the owned list.</param>
	public async Task<List<MusicCollectionItem>> GetCollectionAsync(int userId, UserSession? currentUser = null, CancellationToken cancellationToken = default, bool wanted = false)
	{
		CollectionError = null;
		if (!apiClient.CanUseBackend || userId <= 0)
		{
			return [];
		}

		var result = await apiClient.GetWithStatusAsync<List<MusicCollectionItem>>($"api/MusicCollection/{(wanted ? "wanted" : "owned")}/{userId}", currentUser?.AccessToken, cancellationToken);
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

	/// <summary>
	/// Adds an album to owned with a media format, or to wanted without requiring a format.
	/// </summary>
	/// <param name="wanted">True selects the wanted list; false selects the owned list.</param>
	public async Task<bool> AddToCollectionAsync(int userId, int albumId, UserSession? currentUser = null, CancellationToken cancellationToken = default, MediaFormat? format = null, bool wanted = false)
	{
		CollectionError = null;
		if (!apiClient.CanUseBackend || userId <= 0 || albumId <= 0
			|| currentUser is null || currentUser.UserId != userId || string.IsNullOrWhiteSpace(currentUser.AccessToken))
		{
			return false;
		}

		if (!wanted && (format is null || !Enum.IsDefined(format.Value)))
		{
			CollectionError = "Choose a media format before adding this album.";
			return false;
		}

		object request = wanted
			? new CreateWantedMusicCollectionRequest { UserId = userId, AlbumId = albumId }
			: new CreateOwnedMusicCollectionRequest { UserId = userId, AlbumId = albumId, Format = format!.Value };
		var saved = await apiClient.PostAsync($"api/MusicCollection/{(wanted ? "wanted" : "owned")}",
			request,
			currentUser.AccessToken, cancellationToken);
		if (!saved)
		{
			CollectionError = "Could not save this album to your collection. Please try again.";
		}
		return saved;
	}

	/// <summary>
	/// Removes an owned or wanted collection entry for the user.
	/// </summary>
	public async Task<bool> RemoveFromCollectionAsync(int userId, int collectionId, UserSession? currentUser = null, CancellationToken cancellationToken = default)
	{
		CollectionError = null;
		if (!apiClient.CanUseBackend || userId <= 0 || collectionId <= 0
			|| currentUser is null || currentUser.UserId != userId || string.IsNullOrWhiteSpace(currentUser.AccessToken))
		{
			return false;
		}

		var removed = await apiClient.DeleteAsync($"api/MusicCollection/{collectionId}", currentUser.AccessToken, cancellationToken);
		if (!removed)
		{
			CollectionError = "Could not remove this album from your collection. Please try again.";
		}
		return removed;
	}

	/// <summary>
	/// Applies editor additions and removals against a fresh collection, then verifies the result.
	/// </summary>
	/// <param name="wanted">True selects the wanted list; false selects the owned list.</param>
	public async Task<bool> SaveCollectionChangesAsync(int userId, IReadOnlyCollection<MusicCollectionItem> original,
		IReadOnlyCollection<MusicAlbum> desired, IReadOnlyDictionary<int, MediaFormat> formats,
		UserSession? currentUser = null, CancellationToken cancellationToken = default, bool wanted = false)
	{
		CollectionError = null;
		if (currentUser is null || string.IsNullOrWhiteSpace(currentUser.AccessToken))
		{
			CollectionError = "Please log in again before updating your collection.";
			return false;
		}
		if (!apiClient.CanUseBackend || userId <= 0 || currentUser.UserId != userId)
		{
			return false;
		}

		var current = await GetCollectionAsync(userId, currentUser, cancellationToken, wanted);
		if (CollectionError is not null || !apiClient.CanUseBackend || userId <= 0) return false;
		var desiredIds = desired.Select(album => album.Id).ToHashSet();
		var currentIds = current.Select(item => item.AlbumId).ToHashSet();
		var additions = new Dictionary<int, MediaFormat>();
		foreach (var albumId in desiredIds.Except(currentIds))
		{
			var previous = original.FirstOrDefault(item => item.AlbumId == albumId);
			var format = wanted ? MediaFormat.Other : formats.TryGetValue(albumId, out var selected) ? selected : previous?.Format;
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
			if (!await AddToCollectionAsync(userId, albumId, currentUser, cancellationToken, format, wanted)) return false;
		}
		if (additions.Count == 0 && !current.Any(item => removals.Contains(item.Id))) return true;

		var saved = await GetCollectionAsync(userId, currentUser, cancellationToken, wanted);
		if (CollectionError is not null) return false;
		if (!desiredIds.IsSubsetOf(saved.Select(item => item.AlbumId).ToHashSet()) || saved.Any(item => removals.Contains(item.Id)))
		{
			CollectionError = "Some collection changes could not be confirmed. Your edits are still here; please try again.";
			return false;
		}
		return true;
	}

	/// <summary>
	/// Returns a placeholder with the requested ID if album details cannot be loaded.
	/// </summary>
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
