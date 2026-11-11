using Microsoft.JSInterop;

namespace MusicRUs.Services;

public sealed class ProfileColorService(IJSRuntime js)
{
    public const string DefaultColor = "#66706C";
    public event Action? Changed;

    // Loads the user's browser-stored profile color and normalizes it.
    public async Task<string> GetAsync(int userId)
    {
        var color = await js.InvokeAsync<string?>("MusicRUsProfileColor.load", userId);
        return Normalize(color);
    }

    // Persists the user's color in browser storage and refreshes subscribed components.
    public async Task SaveAsync(int userId, string color)
    {
        await js.InvokeVoidAsync("MusicRUsProfileColor.save", userId, Normalize(color));
        Changed?.Invoke();
    }

    // Accepts a six-digit hex color and otherwise returns the default profile color.
    public static string Normalize(string? color)
    {
        if (color is null || color.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit))
        {
            return DefaultColor;
        }

        return color.ToUpperInvariant();
    }

    // Chooses black or white text using the background's relative luminance.
    public static string TextColor(string color)
    {
        var hex = Normalize(color);
        var red = ChannelBrightness(hex.Substring(1, 2));
        var green = ChannelBrightness(hex.Substring(3, 2));
        var blue = ChannelBrightness(hex.Substring(5, 2));

        // Use WCAG relative luminance and select the higher contrast (at least 4.5:1).
        var luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
        var blackContrast = (luminance + 0.05) / 0.05;
        var whiteContrast = 1.05 / (luminance + 0.05);
        return blackContrast >= whiteContrast ? "#000000" : "#FFFFFF";
    }

    // Converts a two-digit hex channel to linear brightness for comparing text contrast.
    private static double ChannelBrightness(string hexChannel)
    {
        var channel = Convert.ToByte(hexChannel, 16) / 255d;
        if (channel <= 0.04045)
        {
            return channel / 12.92;
        }

        return Math.Pow((channel + 0.055) / 1.055, 2.4);
    }
}
