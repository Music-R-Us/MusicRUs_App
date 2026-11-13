namespace MusicRUs.Models;

// Response from POST api/2fa/setup
public sealed class TwoFactorSetup
{
	public string Secret { get; set; } = string.Empty;
	public string QrCodeDataUri { get; set; } = string.Empty;
}

// The part of GET api/User/me we need here
public sealed class CurrentUserStatus
{
	public bool TwoFactorEnabled { get; set; }
}