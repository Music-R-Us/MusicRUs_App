using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class ArtistSubscriptionService(BackendApiClient apiClient)
{
    public Task<List<ArtistSubscription>?> GetByUserAsync(UserSession user) =>
        apiClient.GetAsync<List<ArtistSubscription>>($"api/ArtistSubscription/user/{user.UserId}", user.AccessToken);

    public Task<bool> SubscribeAsync(UserSession user, int artistId, string countryCode) =>
        apiClient.PostAsync($"api/ArtistSubscription/subscribe/{user.UserId}/{artistId}",
            new SubscribeArtistRequest { CountryCode = countryCode }, user.AccessToken);

    public Task<bool> UnsubscribeAsync(UserSession user, int artistId) =>
        apiClient.DeleteAsync($"api/ArtistSubscription/user/{user.UserId}/artist/{artistId}", user.AccessToken);
}
