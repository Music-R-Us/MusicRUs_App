using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class ConcertService(BackendApiClient apiClient)
{
    /// <summary>
    /// Null signals a failed request; an empty list means no matching concerts.
    /// </summary>
    public async Task<List<Concert>?> GetByArtistAsync(int artistId, string? countryCode, UserSession? user = null)
    {
        if (!apiClient.CanUseBackend || artistId <= 0) return null;
        var code = CountryCodes.FromLocation(countryCode);
        var query = code is null ? string.Empty : $"?countryCode={Uri.EscapeDataString(code)}";
        return await apiClient.GetAsync<List<Concert>>($"api/Concert/artist/{artistId}{query}", user?.AccessToken);
    }
}
