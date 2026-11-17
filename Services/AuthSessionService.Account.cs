using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MusicRUs.Services;

public sealed partial class AuthSessionService
{
    /// <summary>
    /// Updates the account username and persists it in the browser session after success.
    /// </summary>
    public async Task<AuthResult> UpdateUsernameAsync(string username)
    {
        var result = await UpdateAccountAsync("updateUsername", new { NewUsername = username.Trim() });
        if (result.IsSuccess && result.User is { } user)
        {
            user.Username = username.Trim();
            await SaveSessionAsync(user);
        }
        return result;
    }

    /// <summary>
    /// Updates the email using the current email and password for confirmation.
    /// </summary>
    public Task<AuthResult> UpdateEmailAsync(string currentEmail, string newEmail, string currentPassword)
        => UpdateAccountAsync("updateEmail", new { CurrentEmail = currentEmail.Trim(), NewEmail = newEmail.Trim(), CurrentPassword = currentPassword });

    /// <summary>
    /// Updates the password after the backend verifies the current password.
    /// </summary>
    public Task<AuthResult> UpdatePasswordAsync(string currentPassword, string newPassword)
        => UpdateAccountAsync("updatePassword", new { CurrentPassword = currentPassword, NewPassword = newPassword });

    /// <summary>
    /// Sends an authenticated account update and returns validation or connection errors.
    /// </summary>
    private async Task<AuthResult> UpdateAccountAsync<T>(string action, T body)
    {
        await InitializeAsync();
        var user = CurrentUser;

        if (!apiClient.CanUseBackend || user is null || string.IsNullOrWhiteSpace(user.AccessToken))
        {
            return AuthResult.Failure("Sign in to update your account.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/User/{action}/me")
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

            using var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return AuthResult.Success(user);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return AuthResult.Failure("Your session has expired. Sign in again.");
            }

            if ((int)response.StatusCode >= 500)
            {
                return AuthResult.Failure("Could not update your account. Please try again later.");
            }

            var errorMessage = await ReadServerMessageAsync(response, default);
            return AuthResult.Failure(errorMessage ?? "Could not update your account. Please try again.");
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return AuthResult.Failure("Could not reach the account service. Please try again.");
        }
    }
}
