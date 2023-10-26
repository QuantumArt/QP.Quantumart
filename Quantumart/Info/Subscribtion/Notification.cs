namespace Quantumart.QPublishing.Info.Subscribtion
{
    internal class Notification
    {
        public int NotificationId { get; set; }
        public string NotificationName { get; set; }
        public int ContentId { get; set; }
        public bool FromDefaultName { get; set; }
        public string FromUserName { get; set; }
        public string FromUserEmail { get; set; }
        public bool FromBackendUser { get; set; }
        public int FromBackendUserId { get; set; }
        public string FromBackendUserEmail { get; set; }
        public bool UseEmailFromContent { get; set; }
        public int? ConfirmationTemplateId { get; set; }
    }
}
