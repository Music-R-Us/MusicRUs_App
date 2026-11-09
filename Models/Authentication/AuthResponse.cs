namespace MusicRUs.Models;

public sealed class AuthResponse
{
	public UserSession User { get; set; } = new();

	public string AccessToken { get; set; } = string.Empty;
}
