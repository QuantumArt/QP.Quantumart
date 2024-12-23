# Quantumart.AspNetCore

## Назначение

Низкоуровневый API для работы с БД QP. Используется как напрямую, так и совместно с EF, EF.Core.

## Репозиторий

<https://nuget.qsupport.ru/packages/Quantumart.AspNetCore>

## Quantumart.AspNetCore 6.x

### Quantumart.AspNetCore.6.3.0

* Переход с `System.Data.SqlClient` на `Microsoft.Data.SqlClient`

### Quantumart.AspNetCore.6.2.2

* Обновление nuget-пакетов

### Quantumart.AspNetCore.6.2.1

* Обновлён nuget-пакет `QP.ConfigurationService.Client`

### Quantumart.AspNetCore.6.2.0

* Переход на .NET8

### Quantumart.AspNetCore.6.1.2

* Обновлены nuget-пакеты `Npgsql`, `SixLabors.ImageSharp` и `Microsoft.Extensions.Caching.Memory` в связи с найденными уязвимостями (#176358)

### Quantumart.AspNetCore.6.1.1

* Исправлена работа режима returnAll в GetContentItemLinkCommand (#175470)

### Quantumart.AspNetCore.6.1.0

* Добавлена новая реализация IFileSystem с поддержкой MinIO (#174303)
* Исправлены ошибки с уведомлениями:
  * При выборе пользователя в качестве получателя возникает SQL-ошибка (#174698)
  * Aттачменты не поддерживают Use Site Library и Subfolder (#174699)
  * Исправлено имя файла аттачмента (#174700)

### Quantumart.AspNetCore.6.0.15

* Обновлены nuget-пакеты `System.Data.SqlClient` и `SixLabors.ImageSharp` в связи с найденными уязвимостями (#174238)

### Quantumart.AspNetCore.6.0.14

* Добавлена поддержка нативных типов EF для PostgreSQL (#173019)

### Quantumart.AspNetCore.6.0.13

* Удалена схема public из запросов к PostgreSQL (#173717)

### Quantumart.AspNetCore.6.0.12

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
