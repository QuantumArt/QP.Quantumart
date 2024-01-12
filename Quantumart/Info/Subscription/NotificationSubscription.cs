using System;

namespace Quantumart.QPublishing.Info.Subscription;

/// <summary>
/// Получатель из контента (подписка)
/// </summary>
public class NotificationSubscription
{
    /// <summary>
    /// Идентификатор подписки
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Идентификатор рассылки
    /// </summary>
    public int NotificationId { get; set; }
    /// <summary>
    /// Адрес электронной почты
    /// </summary>
    public string Email { get; set; }
    /// <summary>
    /// Идентификатор отшошения с категориями
    /// </summary>
    public int CategoryLinkId { get; set; }
    /// <summary>
    /// Категории подписки
    /// </summary>
    public SubscriptionCategory[] Categories { get; set; }
    /// <summary>
    /// Пользовательские данные
    /// Доступны в качестве контекста в шаблоне письма
    /// </summary>
    public string UserData { get; set; }
    /// <summary>
    /// Флаг подтвержденной подписки
    /// в рассылке участвуют только подтвержденные подписки
    /// </summary>
    public bool Confirmed { get; set; }
    /// <summary>
    /// Код подтверждения подписки
    /// </summary>
    public string ConfirmationCode { get; set; }
    /// <summary>
    /// Время, до которого действителен код подтверждения
    /// </summary>
    public DateTime ConfirmationDate { get; set; }
}
