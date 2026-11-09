namespace MusicRUs.Models;

public sealed class BackendApiOptions
{
	public bool UseBackendApi { get; set; }

	public string BaseUrl { get; set; } = string.Empty;
}
