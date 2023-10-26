namespace Quantumart.QPublishing.Info.Subscribtion
{

    /// <summary>
    /// Результат подписки или отписки
    /// </summary>
    public enum SubscribeResultMode
    {
        /// <summary>
        /// Подписка
        /// </summary>
        Subscribe = 0,
        /// <summary>
        /// Отписка
        /// </summary>
        Unsubscribe = 1,
        /// <summary>
        /// Время подтверждения истекло
        /// </summary>
        ConfirmationDateExpared = 2,
        /// <summary>
        /// Код подтверждения не найден
        /// </summary>
        ConfirmationCodeNotFound = 3,
        /// <summary>
        /// Подписка уже активна
        /// </summary>
        Confirmed = 4,
        /// <summary>
        /// Подписка еще не активна
        /// </summary>
        NotComfirmed = 5
    }
}
