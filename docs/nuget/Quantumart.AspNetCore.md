# Quantumart.AspNetCore

## Назначение

Низкоуровневый API для работы с БД QP. Используется как напрямую, так и совместно с EF, EF.Core.

## Репозиторий

<https://nuget.qsupport.ru/packages/Quantumart.AspNetCore>

## Quantumart.AspNetCore 6.x

### Quantumart.AspNetCore.6.0.12beta1

* Рассылка уведомлений для получателей из контента
* Методы для управления получателями из контента
* Методы для рассылки уведомлений для подтверждения подписок

### Quantumart.AspNetCore.6.0.11

* Добавлено экранирование поля `Key` при чтении из таблицы `App_settings`

### Quantumart.AspNetCore.6.0.10

* Добавлен хинт WITH(NOLOCK) к некоторым запросам `DBConnector`

### Quantumart.AspNetCore.6.0.9

* Обновлена версия пакета [Quantumart.QP8.Assembling](Quantumart.QP8.Assembling) до 1.1.1

### Quantumart.AspNetCore.6.0.8

* Исправлена проблема с формированием https-ссылок (#172194)

### Quantumart.AspNetCore.6.0.7

* Исправлены PG-запросы для хелпера Status

### Quantumart.AspNetCore.6.0.6

* Поддержка скрытых получателей во внутренних уведомлениях

### Quantumart.AspNetCore.6.0.5

* Пакетная отправка для внутренних уведомлений

### Quantumart.AspNetCore.6.0.4

* Исправление ошибок внутренних уведомлений

### Quantumart.AspNetCore.6.0.3

* Поддержка загрузки связей для внутренних уведомлений

### Quantumart.AspNetCore.6.0.2

* Поддержка PostgreSQL во внутренних уведомлениях
* Поддержка движка Fluid во внутренних уведомлениях

### Quantumart.AspNetCore.6.0.1

* Исправлены настройки кэширования по умолчанию в DbConnector в соответствии со старым поведением (общий кэш для разных экземпляров)

### Quantumart.AspNetCore.6.0.0

* Переход на .NET6

## Quantumart.AspNetCore 5.x

### Quantumart.AspNetCore.5.0.1

* Обновлен NpgSql до 5 версии

### Quantumart.AspNetCore.5.0.0

* Выпущена версия под .NET5 без Assembling, версия для .NET Core 3 продолжает поддерживаться в рамках 4.x

## Quantumart.AspNetCore 4.x

Более старые версии описаны в пакете [Quantumart](Quantumart), с которым до 4-й версии включительно данный пакет выпускался совместно.
