using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class AuthSessionService
{
	private readonly IJSRuntime jsRuntime;
	private readonly HttpClient httpClient;
	private readonly BackendApiClient apiClient;
	private readonly ForumProfileService forumProfiles;
	private readonly UserLookupService userLookup;
	private readonly MusicCatalogService musicCatalog;
	private bool initialized;

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

	public event Action? Changed;

	public UserSession? CurrentUser { get; private set; }

	public bool IsAuthenticated => CurrentUser is not null;

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
			var session = await TryLoginUserAsync(request, cancellationToken);
			if (session is null)
			{
				return AuthResult.Failure("Login failed. Check your credentials and try again.");
			}

			await SaveSessionAsync(session);
			await forumProfiles.EnsureForumProfileAsync(CurrentUser?.UserId ?? 0, CurrentUser, cancellationToken);
			return AuthResult.Success(CurrentUser!);
		}

		await SignInAsync(request.Username);
		return AuthResult.Success(CurrentUser!);
	}

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

			await SaveSessionAsync(result.User);
			await forumProfiles.EnsureForumProfileAsync(CurrentUser?.UserId ?? 0, CurrentUser, cancellationToken);
			return AuthResult.Success(CurrentUser!);
		}

		await SignInAsync(request.Username);
		return AuthResult.Success(CurrentUser!);
	}

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

		await SaveSessionAsync(result.User);
		await forumProfiles.EnsureForumProfileAsync(CurrentUser?.UserId ?? 0, CurrentUser, cancellationToken);
		return AuthResult.Success(CurrentUser!);
	}

	public async Task SignInAsync(string username, string? displayName = null)
	{
		await InitializeAsync();

		var session = UserSession.Create(username, displayName);
		CurrentUser = session;
		await jsRuntime.InvokeVoidAsync("MusicRUsAuth.saveSession", session);
		Changed?.Invoke();
	}

	public async Task SignOutAsync()
	{
		await InitializeAsync();
		CurrentUser = null;
		await jsRuntime.InvokeVoidAsync("MusicRUsAuth.clearSession");
		Changed?.Invoke();
	}

	public async Task<ForumProfile?> GetProfileAsync(string username, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await forumProfiles.GetProfileAsync(username, CurrentUser, cancellationToken);
	}

	public async Task<ForumProfile?> GetForumProfileAsync(int userId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await forumProfiles.GetForumProfileAsync(userId, CurrentUser, cancellationToken);
	}

	public async Task<bool> IsForumProfileSetupCompleteAsync(UserSession user, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await forumProfiles.IsForumProfileSetupCompleteAsync(user, cancellationToken);
	}
	public async Task<UserSession?> GetUserAsync(int userId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await userLookup.GetUserAsync(userId, CurrentUser, cancellationToken);
	}

	public async Task<List<MusicAlbum>> GetAlbumsByArtistAsync(int artistId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await musicCatalog.GetAlbumsByArtistAsync(artistId, CurrentUser, cancellationToken);
	}

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

	public async Task<List<GenreOption>> GetGenresAsync(CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await apiClient.GetAsync<List<GenreOption>>("api/Genre", CurrentUser?.AccessToken, cancellationToken) ?? [];
	}

	public async Task<List<MusicArtist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		if (query.Trim().Length < 2) return [];
		return await musicCatalog.SearchArtistsAsync(query, CurrentUser, cancellationToken);
	}

	public async Task<List<MusicAlbum>> SearchAlbumsAsync(string query, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		if (query.Trim().Length < 2) return [];
		return await musicCatalog.SearchAlbumsAsync(query, CurrentUser, cancellationToken);
	}

	public async Task<List<MusicTrack>> GetAlbumTracksAsync(int albumId, CancellationToken cancellationToken = default)
	{
		await InitializeAsync();
		return await musicCatalog.GetAlbumTracksAsync(albumId, CurrentUser, cancellationToken);
	}

	private static bool IsPrerenderInteropException(InvalidOperationException exception)
	{
		return exception.Message.Contains("JavaScript interop calls cannot be issued at this time", StringComparison.Ordinal);
	}

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

	private async Task<UserSession?> TryLoginUserAsync(LoginRequest request, CancellationToken cancellationToken)
	{
		try
		{
			var loginRoute = request.Username.Contains('@', StringComparison.Ordinal)
				? "api/User/login/email"
				: "api/User/login/username";
			using var response = await httpClient.PostAsJsonAsync(loginRoute, request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			var content = await response.Content.ReadAsStringAsync(cancellationToken);
			if (string.IsNullOrWhiteSpace(content))
			{
				return UserSession.Create(request.Username);
			}

			using var document = JsonDocument.Parse(content);
			return CreateSessionFromLoginJson(document.RootElement, request.Username);
		}
		catch
		{
			return null;
		}
	}

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

	private async Task<AuthResult> TryGoogleLoginAsync(string idToken, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await httpClient.PostAsJsonAsync("api/google-auth/login", new GoogleLoginRequest { IdToken = idToken }, cancellationToken);
			if (response.IsSuccessStatusCode)
			{
				var content = await response.Content.ReadAsStringAsync(cancellationToken);
				if (string.IsNullOrWhiteSpace(content))
				{
					return AuthResult.Failure("Google login succeeded, but no user was returned.");
				}

				using var document = JsonDocument.Parse(content);
				var session = CreateSessionFromLoginJson(document.RootElement, "google-user");
				return AuthResult.Success(session);
			}

			return (int)response.StatusCode switch
			{
				401 => AuthResult.Failure("Invalid Google token."),
				404 => AuthResult.Failure("Google login is unavailable right now."),
				409 => AuthResult.Failure(await ReadServerMessageAsync(response, cancellationToken) ?? "Google login could not link this account."),
				_ => AuthResult.Failure(await ReadServerMessageAsync(response, cancellationToken) ?? "Google login failed.")
			};
		}
		catch
		{
			return AuthResult.Failure("Google login failed. Try again in a moment.");
		}
	}

	private static UserSession CreateSessionFromLoginJson(JsonElement root, string fallbackUsername)
	{
		var userElement = root.TryGetProperty("user", out var nestedUser) ? nestedUser : root;
		var userId = JsonElementReader.GetInt(userElement, "userId", "userid", "id")
			?? JsonElementReader.GetInt(root, "userId", "userid", "id");
		var email = JsonElementReader.GetString(userElement, "email", "emailAddress")
			?? JsonElementReader.GetString(root, "email", "emailAddress");
		var username = JsonElementReader.GetString(userElement, "username", "userName", "name")
			?? JsonElementReader.GetString(root, "username", "userName", "name");
		var displayName = JsonElementReader.GetString(userElement, "displayName", "fullName", "username", "userName", "name")
			?? JsonElementReader.GetString(root, "displayName", "fullName", "username", "userName", "name");
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

public sealed record AuthResult(bool IsSuccess, UserSession? User, string? ErrorMessage)
{
	public static AuthResult Success(UserSession user) => new(true, user, null);

	public static AuthResult Failure(string errorMessage) => new(false, null, errorMessage);
}
