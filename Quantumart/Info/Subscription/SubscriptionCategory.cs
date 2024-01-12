namespace Quantumart.QPublishing.Info.Subscription
{
    /// <summary>
    /// Категория подписки
    /// </summary>
    public class SubscriptionCategory
    {
        /// <summary>
        /// Идентификатор связи подписки и категории
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Идентификатор статьи категории
        /// </summary>
        public int CategoryId { get; set; }
        /// <summary>
        /// Название категории
        /// </summary>
        public string Name { get; set; }
    }
}
