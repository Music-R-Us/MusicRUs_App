namespace MusicRUs.Models;

public sealed class FriendshipItem
{
    public int Id { get; set; }
    public int RequesterId { get; set; }
    public int AddresseeId { get; set; }
    public string Status { get; set; } = string.Empty; // "Pending", "Accepted" or "Rejected"
    public FriendshipUser Requester { get; set; } = new();
    public FriendshipUser Addressee { get; set; } = new();

    // The other person in the friendship, seen from the logged-in user
    public FriendshipUser OtherUser(int myUserId) => RequesterId == myUserId ? Addressee : Requester;
}

public sealed class FriendshipUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
}