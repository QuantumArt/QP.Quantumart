namespace Quantumart.QPublishing.Info.Subscribtion
{
    /// <summary>
    /// Категория подписки
    /// </summary>
    public class SubscribtionCategory
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
