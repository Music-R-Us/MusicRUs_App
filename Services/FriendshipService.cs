using MusicRUs.Models;

namespace MusicRUs.Services;

// Calls the API's friendship endpoints. The API reads the user ID from the token, so only the token is sent
public sealed class FriendshipService(BackendApiClient apiClient)
{
    // Null signals a failed request; an empty list means no friendships yet
    public async Task<List<FriendshipItem>?> GetAllAsync(UserSession user)
        => apiClient.CanUseBackend
            ? await apiClient.GetAsync<List<FriendshipItem>>("api/Friendship/FsList-By-RequesterId", user.AccessToken)
            : null;

    public Task<bool> SendRequestAsync(UserSession user, int addresseeId)
        => apiClient.PostAsync($"api/Friendship/CreateFs/Addressee/{addresseeId}", accessToken: user.AccessToken);

    // The PATCH endpoints take no body, so an empty object is sent
    public Task<bool> AcceptAsync(UserSession user, int friendshipId)
        => apiClient.PatchAsync($"api/Friendship/AcceptFs/{friendshipId}/me", new { }, user.AccessToken);

    public Task<bool> RejectAsync(UserSession user, int friendshipId)
        => apiClient.PatchAsync($"api/Friendship/RejectFs/{friendshipId}/me", new { }, user.AccessToken);

    // Removes a friend, or cancels a request you have sent
    public Task<bool> RemoveAsync(UserSession user, FriendshipItem friendship)
        => apiClient.DeleteAsync($"api/Friendship/DeleteFs/{friendship.Id}", user.AccessToken);

    public async Task<List<FriendshipUser>> SearchUsersAsync(UserSession user, string keyword)
        => await apiClient.GetAsync<List<FriendshipUser>>($"api/User/searchUsers?keyword={Uri.EscapeDataString(keyword.Trim())}", user.AccessToken) ?? [];
}