namespace Quantumart.QPublishing.Info.Subscribtion
{
    /// <summary>
    /// Данные подписки
    /// </summary>
    public class SubscribeResult
    {
        /// <summary>
        /// Резальтат действия
        /// </summary>
        public SubscribeResultMode Action { get; set; }
        /// <summary>
        /// Старая подписка, которая была до смены подписки
        /// null если подписались впервые
        /// </summary>
        public NotificationSubscribtion OldSubscription { get; set; }
        /// <summary>
        /// Новая подписка
        /// </summary>
        public NotificationSubscribtion NewSubscription { get; set; }
    }
}
