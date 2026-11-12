namespace MusicRUs.Models;

// Wanted albums are saved independently of owned copies and their media formats.
public sealed class CreateWantedMusicCollectionRequest
{
    public int UserId { get; set; }
    public int AlbumId { get; set; }
}
