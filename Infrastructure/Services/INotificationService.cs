namespace Infrastructure.Services
{
    public interface INotificationService
    {
        Task SendNotificationForSingleDevice(NotificationBody notificationBody);
        Task SendNotificationAsyncToMultipleDevices(NotificationBodyForMultipleDevices notificationBody);
    }

    public class NotificationBody
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string FireBaseToken { get; set; } = string.Empty;
        public Dictionary<string, string> PayLoad { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Custom sound file name without extension (Android res/raw, iOS bundle uses "{Sound}.caf").
        /// Null keeps the device default sound.
        /// </summary>
        public string? Sound { get; set; }

        /// <summary>Android notification channel; null keeps the default channel "1".</summary>
        public string? AndroidChannelId { get; set; }
    }

    public class NotificationBodyForMultipleDevices
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<string> FireBaseTokens { get; set; } = new List<string>();
        public Dictionary<string, string> PayLoad { get; set; } = new Dictionary<string, string>();
    }
}


