using Microsoft.JSInterop;

namespace MusicRUs.Services;

public sealed class ProfileColorService(IJSRuntime js)
{
    public const string DefaultColor = "#66706C";
    public event Action? Changed;

    public async Task<string> GetAsync(int userId)
    {
        var color = await js.InvokeAsync<string?>("MusicRUsProfileColor.load", userId);
        return Normalize(color);
    }

    public async Task SaveAsync(int userId, string color)
    {
        await js.InvokeVoidAsync("MusicRUsProfileColor.save", userId, Normalize(color));
        Changed?.Invoke();
    }

    public static string Normalize(string? color) => color is { Length: 7 }
        && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit)
            ? color.ToUpperInvariant() : DefaultColor;

    public static string TextColor(string color)
    {
        var hex = Normalize(color);
        static double Linear(byte value)
        {
            var channel = value / 255d;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        var luminance = 0.2126 * Linear(Convert.ToByte(hex.Substring(1, 2), 16))
            + 0.7152 * Linear(Convert.ToByte(hex.Substring(3, 2), 16))
            + 0.0722 * Linear(Convert.ToByte(hex.Substring(5, 2), 16));
        return luminance > 0.179 ? "#000000" : "#FFFFFF";
    }
}
