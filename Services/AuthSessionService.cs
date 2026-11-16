using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using MusicRUs.Models;
using System.Net;

namespace MusicRUs.Services;

/// <summary>
/// Manages the browser session and passes the current user to profile and music services.
/// </summary>
public sealed class AuthSessionService
{
	private readonly IJSRuntime jsRuntime;
	private readonly HttpClient httpClient;
	private readonly BackendApiClient apiClient;
	private readonly ForumProfileService forumProfiles;
	private readonly UserLookupService userLookup;
	private readonly MusicCatalogService musicCatalog;
	private bool initialized;

	/// <summary>
	/// Connects browser session storage with authentication, profile, user, and catalog services.
	/// </summary>
	public AuthSessionService(
		IJSRuntime jsRuntime,
		HttpClient httpClient,
		BackendApiClient apiClient,
		ForumProfileService forumProfiles,
		UserLookupService userLookup,
		MusicCatalogService musicCatalog
	)
	{
		this.jsRuntime = jsRuntime;
		this.httpClient = httpClient;
		this.apiClient = apiClient;
		this.forumProfiles = forumProfiles;
		this.userLookup = userLookup;
		this.musicCatalog = musicCatalog;
	}

	/// <summary>
	/// Notifies components when the session or a saved forum profile changes.
	/// </summary>
	public event Action? Changed;

	/// <summary>
	/// Holds the current browser session, or null when signed out.
	/// </summary>
	public UserSession? CurrentUser { get; private set; }

	/// <summary>
	/// Reports whether a user session is currently loaded.
	/// </summary>
	public bool IsAuthenticated => CurrentUser is not null;

	/// <summary>
	/// Restores the browser session once; returns false if JavaScript is not available yet.
	/// </summary>
	public async Task<bool> InitializeAsync()
	{
		if (initialized)
		{
			return true;
		}

		try
		{
			CurrentUser = await jsRuntime.InvokeAsync<UserSession?>("MusicRUsAuth.loadSession");
		}
		catch (InvalidOperationException exception) when (IsPrerenderInteropException(exception))
		{
			return false;
		}

		initialized = true;
		Changed?.Invoke();
		return true;
	}

	/// <summary>
	/// Uses backend credentials when enabled, otherwise creates a local browser session.
	/// </summary>
	public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();

		var request = new LoginRequest
		{
			Username = username.Trim(),
			Password = password
		};

		if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
		{
			return AuthResult.Failure("Enter both username and password.");
		}

		if (apiClient.CanUseBackend)
		{
			var result = await TryLoginUserAsync(request, cancellationToken);
			if (!result.IsSuccess || result.User is null)
			{
				return result; // An error, or a 2FA code is required
			}

			return await CompleteLoginAsync(result.User, cancellationToken);
		}

		await SignInAsync(request.Username);
		return AuthResult.Success(CurrentUser!);
	}

	/// <summary>
	/// Creates an account and signs in after a successful response.
	/// </summary>
	public async Task<AuthResult> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();

		request.Username = request.Username.Trim();
		request.Email = request.Email.Trim();

		if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
		{
			return AuthResult.Failure("Complete username, email, and password before creating an account.");
		}

		if (apiClient.CanUseBackend)
		{
			var result = await TryCreateUserAsync(request, cancellationToken);
			if (!result.IsSuccess || result.User is null)
			{
				return result;
			}

			return await CompleteLoginAsync(result.User, cancellationToken);
		}

		await SignInAsync(request.Username);
		return AuthResult.Success(CurrentUser!);
	}

	/// <summary>
	/// Validates the Google token and exchanges it for a signed-in backend session.
	/// </summary>
	public async Task<AuthResult> GoogleLoginAsync(string idToken, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();

		if (string.IsNullOrWhiteSpace(idToken))
		{
			return AuthResult.Failure("Google login did not return a token.");
		}

		if (!apiClient.CanUseBackend)
		{
			return AuthResult.Failure("Google login requires the backend API.");
		}

		var result = await TryGoogleLoginAsync(idToken.Trim(), cancellationToken);
		if (!result.IsSuccess || result.User is null)
		{
			return result;
		}

		return await CompleteLoginAsync(result.User, cancellationToken);
	}

	/// <summary>
	/// Second step of login when 2FA is enabled: sends the code and completes the login.
	/// </summary>
	public async Task<AuthResult> VerifyTwoFactorAsync(string twoFactorToken, string code, CancellationToken cancellationToken = default)
	{
		try
		{
			using var response = await httpClient.PostAsJsonAsync("api/2fa/verify", new { twoFactorToken, code = code.Trim() }, cancellationToken);
			var result = await ReadLoginResponseAsync(response, "user", cancellationToken);
			return result.IsSuccess && result.User is not null
				? await CompleteLoginAsync(result.User, cancellationToken)
				: result;
		}
		catch
		{
			return AuthResult.Failure("Could not reach the login service. Please try again in a moment.");
		}
	}

	/// <summary>
	/// Creates and saves a local session for the supplied username.
	/// </summary>
	public async Task SignInAsync(string username, string? displayName = null)
	{
		await InitializeAsync();

		await SaveSessionAsync(UserSession.Create(username, displayName));
	}

	/// <summary>
	/// Clears the browser session and notifies components that the user signed out.
	/// </summary>
	public async Task SignOutAsync()
	{
		await InitializeAsync();
		CurrentUser = null;
		await jsRuntime.InvokeVoidAsync("MusicRUsAuth.clearSession");
		Changed?.Invoke();
	}

	/// <summary>
	/// Loads a forum profile by username using the current session.
	/// </summary>
	public async Task<ForumProfile?> GetProfileAsync(string username, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await forumProfiles.GetProfileAsync(username, CurrentUser, cancellationToken);
	}

	/// <summary>
	/// Loads a user's forum profile using the current session.
	/// </summary>
	public async Task<ForumProfile?> GetForumProfileAsync(int userId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await forumProfiles.GetForumProfileAsync(userId, CurrentUser, cancellationToken);
	}

	/// <summary>
	/// Checks whether the user has personalized their forum profile.
	/// </summary>
	public async Task<bool> IsForumProfileSetupCompleteAsync(UserSession user, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await forumProfiles.IsForumProfileSetupCompleteAsync(user, cancellationToken);
	}
	/// <summary>
	/// Loads public user details using the current session.
	/// </summary>
	public async Task<UserSession?> GetUserAsync(int userId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await userLookup.GetUserAsync(userId, CurrentUser, cancellationToken);
	}

	/// <summary>
	/// Saves the forum profile and notifies subscribers after a successful save.
	/// </summary>
	public async Task<bool> SaveForumProfileAsync(ForumProfile profile, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		var saved = await forumProfiles.SaveForumProfileAsync(profile, CurrentUser, cancellationToken);
		if (saved)
		{
			Changed?.Invoke();
		}

		return saved;
	}

	/// <summary>
	/// Loads the available genres using the current session.
	/// </summary>
	public async Task<List<GenreOption>> GetGenresAsync(CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await apiClient.GetAsync<List<GenreOption>>("api/Genre", CurrentUser?.AccessToken, cancellationToken) ?? [];
	}

	/// <summary>
	/// Searches the catalog for artists matching the supplied text.
	/// </summary>
	public async Task<List<MusicArtist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		if (query.Trim().Length < 2) return [];
		return await musicCatalog.SearchArtistsAsync(query, CurrentUser, cancellationToken);
	}

	/// <summary>
	/// Searches the catalog for albums matching the supplied text.
	/// </summary>
	public async Task<List<MusicAlbum>> SearchAlbumsAsync(string query, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		if (query.Trim().Length < 2) return [];
		return await musicCatalog.SearchAlbumsAsync(query, CurrentUser, cancellationToken);
	}

	/// <summary>
	/// Loads the selected album's tracks.
	/// </summary>
	public async Task<List<MusicTrack>> GetAlbumTracksAsync(int albumId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await musicCatalog.GetAlbumTracksAsync(albumId, CurrentUser, cancellationToken);
	}
	/// <summary>
	/// Exposes the latest collection operation's error message from the catalog service.
	/// </summary>
	public string? CollectionError => musicCatalog.CollectionError;

	/// <summary>
	/// Saves the collection additions and removals made in the editor.
	/// </summary>
	/// <param name="wanted">True selects the wanted list; false selects the owned list.</param>
	public async Task<bool> SaveCollectionChangesAsync(int userId, IReadOnlyCollection<MusicCollectionItem> original,
		IReadOnlyCollection<MusicAlbum> desired, IReadOnlyDictionary<int, MediaFormat> formats, CancellationToken cancellationToken = default, bool wanted = false)
	{
		await InitializeAsync();
		return await musicCatalog.SaveCollectionChangesAsync(userId, original, desired, formats, CurrentUser, cancellationToken, wanted);
	}

	/// <summary>
	/// Loads the user's selected owned or wanted collection using the current session.
	/// </summary>
	/// <param name="wanted">True selects the wanted list; false selects the owned list.</param>
	public async Task<List<MusicCollectionItem>> GetCollectionAsync(int userId, CancellationToken cancellationToken = default, bool wanted = false)
	{
		await InitializeAsync();
		return await musicCatalog.GetCollectionAsync(userId, CurrentUser, cancellationToken, wanted);
	}

	/// <summary>
	/// Restores the session, then asks the catalog service to save an album to the selected list.
	/// Returns true when saved; CollectionError contains details when the catalog rejects the request.
	/// </summary>
	/// <param name="wanted">True selects the wanted list; false selects the owned list.</param>
	public async Task<bool> AddToCollectionAsync(int userId, int albumId, CancellationToken cancellationToken = default, MediaFormat? format = null, bool wanted = false)
	{
		await InitializeAsync();
		return await musicCatalog.AddToCollectionAsync(userId, albumId, CurrentUser, cancellationToken, format, wanted);
	}

	/// <summary>
	/// Removes an owned or wanted collection entry for the user.
	/// </summary>
	public async Task<bool> RemoveFromCollectionAsync(int userId, int collectionId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await musicCatalog.RemoveFromCollectionAsync(userId, collectionId, CurrentUser, cancellationToken);
	}

	/// <summary>
	/// Loads the artist and their albums using the current session.
	/// </summary>
	public async Task<(MusicArtist? Artist, List<MusicAlbum> Albums)> GetArtistDiscographyAsync(int artistId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await musicCatalog.GetArtistDiscographyAsync(artistId, CurrentUser, cancellationToken);
	}
	/// <summary>
	/// Returns the first album matching the search text.
	/// </summary>
	public async Task<MusicAlbum?> SearchAlbumAsync(string title, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await musicCatalog.SearchAlbumAsync(title, CurrentUser, cancellationToken);
	}

	/// <summary>
	/// Recognizes the error raised when browser interop is called before it is available.
	/// </summary>
	private static bool IsPrerenderInteropException(InvalidOperationException exception)
	{
		return exception.Message.Contains("JavaScript interop calls cannot be issued at this time", StringComparison.Ordinal);
	}

	/// <summary>
	/// Saves the authenticated session, calls the profile hook, and returns a successful login result.
	/// </summary>
	private async Task<AuthResult> CompleteLoginAsync(UserSession session, CancellationToken cancellationToken)
	{
		await SaveSessionAsync(session);
		return AuthResult.Success(CurrentUser!);
	}

	/// <summary>
	/// Stores a normalized session in the browser and notifies subscribed components.
	/// </summary>
	private async Task SaveSessionAsync(UserSession session)
	{
		if (string.IsNullOrWhiteSpace(session.Username))
		{
			throw new InvalidOperationException("The login API returned a session without a username.");
		}

		CurrentUser = UserSession.Create(session.Username, session.DisplayName, session.AvatarUrl, session.AccessToken, session.UserId);
		await jsRuntime.InvokeVoidAsync("MusicRUsAuth.saveSession", CurrentUser);
		Changed?.Invoke();
	}

	/// <summary>
	/// Sends username or e-mail login. The API expects "email" for e-mail login and "username" otherwise.
	/// </summary>
	private async Task<AuthResult> TryLoginUserAsync(LoginRequest request, CancellationToken cancellationToken)
	{
		try
		{
			var isEmail = request.Username.Contains('@', StringComparison.Ordinal);
			var loginRoute = isEmail ? "api/User/login/email" : "api/User/login/username";
			object body = isEmail
				? new { email = request.Username, password = request.Password }
				: new { username = request.Username, password = request.Password };

			using var response = await httpClient.PostAsJsonAsync(loginRoute, body, cancellationToken);
			return await ReadLoginResponseAsync(response, request.Username, cancellationToken);
		}
		catch
		{
			return AuthResult.Failure("Could not reach the login service. Please try again in a moment.");
		}
	}

	/// <summary>
	/// Reads a login, Google or 2FA response: 200 = logged in, 202 = code needed, otherwise the server's error.
	/// </summary>
	private static async Task<AuthResult> ReadLoginResponseAsync(HttpResponseMessage response, string fallbackUsername, CancellationToken cancellationToken)
	{
		if (response.StatusCode == HttpStatusCode.Accepted)
		{
			using var challenge = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
			var token = JsonElementReader.GetString(challenge.RootElement, "twoFactorToken");
			return token is null
				? AuthResult.Failure("Login failed. Please try again.")
				: AuthResult.TwoFactorRequired(token);
		}

		if (response.IsSuccessStatusCode)
		{
			using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
			return AuthResult.Success(CreateSessionFromLoginJson(document.RootElement, fallbackUsername));
		}

		if (response.StatusCode == HttpStatusCode.TooManyRequests)
		{
			return AuthResult.Failure("Too many attempts. Wait a minute and try again.");
		}

		// 401, 409 and 423 come with a message from the API (e.g. "Account temporarily locked ...")
		return AuthResult.Failure(await ReadServerMessageAsync(response, cancellationToken)
			?? $"Login failed (HTTP {(int)response.StatusCode}).");
	}

	/// <summary>
	/// Submits signup details and returns either the new session or a readable error.
	/// </summary>
	private async Task<AuthResult> TryCreateUserAsync(SignupRequest request, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await httpClient.PostAsJsonAsync("api/User", request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return AuthResult.Failure(await ReadServerMessageAsync(response, cancellationToken)
					?? $"Signup failed (HTTP {(int)response.StatusCode}). Please check your details and try again.");
			}

			var content = await response.Content.ReadAsStringAsync(cancellationToken);
			if (string.IsNullOrWhiteSpace(content))
			{
				return AuthResult.Success(UserSession.Create(request.Username));
			}

			using var document = JsonDocument.Parse(content);
			return AuthResult.Success(CreateSessionFromLoginJson(document.RootElement, request.Username));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (HttpRequestException)
		{
			return AuthResult.Failure("Could not reach the signup service. Please try again in a moment.");
		}
		catch (JsonException)
		{
			return AuthResult.Failure("The signup service returned an unexpected response. Try logging in before creating another account.");
		}
	}

	/// <summary>
	/// Exchanges a Google ID token for a session and translates backend errors.
	/// </summary>
	private async Task<AuthResult> TryGoogleLoginAsync(string idToken, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await httpClient.PostAsJsonAsync("api/google-auth/login", new GoogleLoginRequest { IdToken = idToken }, cancellationToken);
			return await ReadLoginResponseAsync(response, "google-user", cancellationToken);
		}
		catch
		{
			return AuthResult.Failure("Google login failed. Try again in a moment.");
		}
	}

	/// <summary>
	/// Accepts both nested and flat login responses, including alternate field names.
	/// </summary>
	private static UserSession CreateSessionFromLoginJson(JsonElement root, string fallbackUsername)
	{
		var userElement = root.TryGetProperty("user", out var nestedUser) ? nestedUser : root;
		var userId = JsonElementReader.GetInt(userElement, "userId", "id")
			?? JsonElementReader.GetInt(root, "userId", "id");
		var email = JsonElementReader.GetString(userElement, "email", "emailAddress")
			?? JsonElementReader.GetString(root, "email", "emailAddress");
		var username = JsonElementReader.GetString(userElement, "username", "name")
			?? JsonElementReader.GetString(root, "username", "name");
		var displayName = JsonElementReader.GetString(userElement, "displayName", "fullName", "username", "name")
			?? JsonElementReader.GetString(root, "displayName", "fullName", "username", "name");
		var avatarUrl = JsonElementReader.GetString(userElement, "avatarUrl", "profilePictureUrl", "picture")
			?? JsonElementReader.GetString(root, "avatarUrl", "profilePictureUrl", "picture");
		var accessToken = JsonElementReader.GetString(root, "accessToken", "token", "jwt")
			?? JsonElementReader.GetString(userElement, "accessToken", "token", "jwt");

		return UserSession.Create(
			ResolveUsername(username, email, fallbackUsername),
			displayName,
			avatarUrl,
			accessToken,
			userId ?? 0);
	}

	/// <summary>
	/// Uses the returned username, the email's local part, or the supplied fallback.
	/// </summary>
	private static string ResolveUsername(string? username, string? email, string fallbackUsername)
	{
		if (!string.IsNullOrWhiteSpace(username))
		{
			return username;
		}

		if (!string.IsNullOrWhiteSpace(email))
		{
			var atIndex = email.IndexOf('@');
			return atIndex > 0 ? email[..atIndex] : email;
		}

		return fallbackUsername;
	}

	/// <summary>
	/// Combines field validation errors or falls back to the server's general message.
	/// </summary>
	private static async Task<string?> ReadServerMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		if (string.IsNullOrWhiteSpace(content))
		{
			return null;
		}

		try
		{
			using var document = JsonDocument.Parse(content);
			var root = document.RootElement;
			if (JsonElementReader.TryGetProperty(root, "errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
			{
				var messages = new List<string>();
				foreach (var field in errors.EnumerateObject())
				{
					if (field.Value.ValueKind == JsonValueKind.Array)
					{
						messages.AddRange(field.Value.EnumerateArray()
							.Where(value => value.ValueKind == JsonValueKind.String)
							.Select(value => value.GetString()!)
							.Where(value => !string.IsNullOrWhiteSpace(value)));
					}
					else if (field.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(field.Value.GetString()))
					{
						messages.Add(field.Value.GetString()!);
					}
				}

				if (messages.Count > 0)
				{
					return string.Join(" ", messages.Distinct());
				}
			}

			return root.ValueKind == JsonValueKind.String
				? root.GetString()
				: JsonElementReader.GetString(root, "message", "error", "detail", "title");
		}
		catch
		{
			return content;
		}
	}
}

public sealed record AuthResult(bool IsSuccess, UserSession? User, string? ErrorMessage, string? TwoFactorToken = null)
{
	public bool RequiresTwoFactor => TwoFactorToken is not null;

	public static AuthResult Success(UserSession user) => new(true, user, null);

	public static AuthResult Failure(string errorMessage) => new(false, null, errorMessage);

	// Correct password, but the user has 2FA enabled and must enter a code
	public static AuthResult TwoFactorRequired(string twoFactorToken) => new(false, null, null, twoFactorToken);
}
