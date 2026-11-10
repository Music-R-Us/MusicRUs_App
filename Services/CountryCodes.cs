using System.Globalization;

namespace MusicRUs.Services;

public static class CountryCodes
{
    public sealed record Country(string Code, string Name, string NativeName, string ThreeLetterCode);

    public static IReadOnlyList<Country> Countries { get; } = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(culture => new RegionInfo(culture.Name))
        .DistinctBy(region => region.TwoLetterISORegionName)
        .Select(region => new Country(region.TwoLetterISORegionName, region.EnglishName, region.NativeName, region.ThreeLetterISORegionName))
        .OrderBy(country => country.Name)
        .ToArray();

    // Accept exact country names and codes; never guess a country from a city name.
    public static string? FromLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        var value = location.Trim();
        if (value.Equals("UK", StringComparison.OrdinalIgnoreCase)) return "GB";
        if (value.Equals("Danmark", StringComparison.OrdinalIgnoreCase)) return "DK";
        var match = Countries.FirstOrDefault(country =>
            new[] { country.Code, country.Name, country.NativeName, country.ThreeLetterCode }
                .Any(name => name.Equals(value, StringComparison.OrdinalIgnoreCase)));
        if (match is not null) return match.Code;
        // Also support profile locations such as "Copenhagen, Denmark".
        var separator = value.LastIndexOf(',');
        return separator >= 0 ? FromLocation(value[(separator + 1)..]) : null;
    }
}
