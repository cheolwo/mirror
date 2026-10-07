namespace 살뜰.Services.Notifications
{
    public sealed record FcmPushMessage(
        string Token,
        string Title,
        string Body,
        IReadOnlyDictionary<string, string> Data,
        string? ImageUrl = null,
        bool HighPriority = false,
        bool DataOnly = false);

    public interface IFcmPushService
    {
        Task<bool> SendAsync(
            FcmPushMessage message,
            CancellationToken cancellationToken = default);

        Task<bool> SendToTokenAsync(
            string token,
            string title,
            string body,
            IReadOnlyDictionary<string, string> data,
            CancellationToken cancellationToken = default);
    }
}
