namespace MusicRUs.Services;

public class ToastService
{
    public event Action<string>? OnShow;

    // Notifies toast subscribers to display the supplied message.
    public void Show(string message)
    {
        OnShow?.Invoke(message);
    }
}