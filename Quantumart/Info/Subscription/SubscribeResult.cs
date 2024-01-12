namespace Quantumart.QPublishing.Info.Subscription
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
        public NotificationSubscription OldSubscription { get; set; }
        /// <summary>
        /// Новая подписка
        /// </summary>
        public NotificationSubscription NewSubscription { get; set; }
    }
}
