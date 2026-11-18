using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class ArtistSubscriptionService(BackendApiClient apiClient)
{
    /// <summary>
    /// Loads the signed-in user's artist subscriptions.
    /// </summary>
    public Task<List<ArtistSubscription>?> GetByUserAsync(UserSession user) =>
        apiClient.GetAsync<List<ArtistSubscription>>($"api/ArtistSubscription/user/me", user.AccessToken);

    /// <summary>
    /// Subscribes the user to an artist with the selected country for concert updates.
    /// </summary>
    public Task<bool> SubscribeAsync(UserSession user, int artistId, string countryCode) =>
        apiClient.PostAsync($"api/ArtistSubscription/user/me/artist/{artistId}",
            new SubscribeArtistRequest { CountryCode = countryCode }, user.AccessToken);

    /// <summary>
    /// Removes the user's subscription to an artist.
    /// </summary>
    public Task<bool> UnsubscribeAsync(UserSession user, int artistId) =>
        apiClient.DeleteAsync($"api/ArtistSubscription/user/me/artist/{artistId}", user.AccessToken);
}
