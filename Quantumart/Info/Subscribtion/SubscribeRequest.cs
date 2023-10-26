namespace Quantumart.QPublishing.Info.Subscribtion
{
    public class SubscribeRequest
    {
        public SubscribeRequestMode Action { get; set; }
        /// <summary>
        /// Адрес электронной почты
        /// </summary>
        public string Email { get; set; }
        /// <summary>
        /// Старые категории подписки
        /// </summary>
        public string[] OldCategories { get; set; }
        /// <summary>
        /// Новые категории подписки
        /// </summary>
        public string[] NewCategories { get; set; }
        /// <summary>
        /// Старые пользовательские данные
        /// Доступны в качестве контекста в шаблоне письма
        /// </summary>
        public string OldUserData { get; set; }
        /// <summary>
        /// Новые пользовательские данные
        /// Доступны в качестве контекста в шаблоне письма
        /// </summary>
        public string NewUserData { get; set; }
    }
}
