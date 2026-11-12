namespace MusicRUs.Services;

public class ToastService
{
    public event Action<string>? OnShow;

    /// <summary>
    /// Notifies toast subscribers to display the supplied message.
    /// </summary>
    public void Show(string message)
    {
        OnShow?.Invoke(message);
    }
}