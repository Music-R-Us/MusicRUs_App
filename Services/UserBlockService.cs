using MusicRUs.Models;

namespace MusicRUs.Services;

/// <summary>
/// Loads block relationships and manages blocking for the signed-in user.
/// </summary>
public sealed class UserBlockService(BackendApiClient apiClient)
{
    /// <summary>
    /// Gets user IDs hidden by a block in either direction, or null if the lookup fails.
    /// Returns an empty set when signed out or backend use is disabled.
    /// </summary>
    public async Task<HashSet<int>?> GetHiddenUserIdsAsync(UserSession? user, CancellationToken cancellationToken = default)
    {
        if (user is null || !apiClient.CanUseBackend) return [];
        var blocks = await apiClient.GetAsync<List<UserBlock>>("api/UserBlock/user", user.AccessToken, cancellationToken);
        return blocks?.Where(block => block.BlockerId == user.UserId || block.BlockedId == user.UserId)
            .Select(block => block.BlockerId == user.UserId ? block.BlockedId : block.BlockerId)
            .ToHashSet();
    }

    /// <summary>
    /// Loads incoming and outgoing blocks, preserving who blocked whom.
    /// Returns null when backend use is disabled or the request fails.
    /// </summary>
    public Task<List<UserBlock>?> GetAsync(UserSession user)
        => apiClient.CanUseBackend
            ? apiClient.GetAsync<List<UserBlock>>("api/UserBlock/user", user.AccessToken)
            : Task.FromResult<List<UserBlock>?>(null);

    /// <summary>
    /// Blocks the specified user and reports whether the backend request succeeded.
    /// </summary>
    public Task<bool> BlockAsync(UserSession user, int blockedId)
        => apiClient.PostAsync($"api/UserBlock/Block/{blockedId}", accessToken: user.AccessToken);

    /// <summary>
    /// Removes the signed-in user's block on the specified user and reports whether the request succeeded.
    /// </summary>
    public Task<bool> UnblockAsync(UserSession user, int blockedId)
        => apiClient.DeleteAsync($"api/UserBlock/Unblock/{blockedId}", user.AccessToken);
}
