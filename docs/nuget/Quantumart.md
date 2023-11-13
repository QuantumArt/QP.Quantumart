# Quantumart

## Назначение

Низкоуровневый API для работы с БД QP. Используется как напрямую, так и совместно с LINQ-to-SQL, EF.

## Репозиторий

<https://nuget.qsupport.ru/packages/Quantumart>

## Quantumart 4.x

### Quantumart.4.0.8

* Обновлена версия пакета [Quantumart.QP8.Assembling](Quantumart.QP8.Assembling) до 1.0.7

### Quantumart.4.0.7

* Добавлена поддержка плагинов QP (класс `Plugins`)

### Quantumart.4.0.6

* Обновлен NpgSql до 5 версии

### Quantumart.4.0.5

* Обновлены Build Constants

### Quantumart.4.0.4

* Исправлена логика вызова SqlMetal под Windows

### Quantumart.4.0.3

* Исправлена ошибка с отсутствующей колонкой ROWS_COUNT

### Quantumart.4.0.2

* Исправлен запрос на получение статусов из PostgreSQL

### Quantumart.4.0.1

* Исправлены пути при сборке под Linux

### Quantumart.4.0.0

* Добавлена поддержка PostgreSQL

## Quantumart 3.x

### Quantumart.3.4.0

* Добавлена поддержка опции disable_replace_urls уровня сайта.

### Quantumart.3.3.5

* Исправлена ошибка с пробелами в путях при вызове Assemble Contents из QP.

### Quantumart.3.3.4

* Исправлен возможный InvalidOperationException в CheckCustomTabAuthentication.

### Quantumart.3.3.3

* Исправлена ошибка в настройках для Quantumart.AspNetCore, из-за которой генерировалась debug-версия dll.

### Quantumart.3.3.2

* Разные алгоритмы генерации динамических изображений для .NET Framework 4.7.1 и .NET Standard 2.0

### Quantumart.3.3.1

* Добавлена поддержка генерации динамических изображений в .NET Standard 2.0

### Quantumart.3.3.0

* Добавлена поддержка .NET Standard 2.0

### Quantumart.3.2.2

* Исправлен дефект #24200, связанный с обработкой полей типа Dynamic Image в операциях MassUpdate

### Quantumart.3.2.0

* Добавлена асинхронная версия MassUpdate

### Quantumart.3.1.3

* Исправлена SQL-ошибка во внутренних нотификациях.

### Quantumart.3.1.2

* Добавлена возможность аутентификации с использованием access-токенов.

### Quantumart.3.1.1

* Выполнен переход на .NET Framework 4.7.1.
* Перенесены интеграционные тесты

### Quantumart.3.0.1

* Исправлены xslt для генерации LINQ-to-SQL классов

### Quantumart.3.0.0

* Мультиплатформенная версия Nuget-пакета (.NET Framework 4.7, NETCoreApp 2.0)
* Разделение на Quantumart и Quantumart.AspNetCore

## Quantumart 2.x

### Quantumart.2.2.0

* Выполнен переход на .NET Framework 4.7

### Quantumart.2.1.6

* Исправлен дефект #106010: свойства сайта кэшируются на несколько часов вместо 10 минут.

### Quantumart.2.1.5

* Исправлен дефект #101563: при нехватке памяти на машине в LINQ-to-SQL классах постоянно возникал NullReferenceException

### Quantumart.2.1.4

* Исправлен дефект #105127: тройной слэш в ссылках на изображения

### Quantumart.2.1.3

* В рамках задач #98150 добавлен класс *SystemColumnMemberNames* со служебными именами полей контента.

### Quantumart.2.1.2

* Исправлено поведение метода GetDefaultMapFileContents. Теперь новая логика по генерации файла (из 2.1.0) включается только если файла не существует.

### Quantumart.2.1.1

Реализовано в рамках #97031:

* путь к глобальному конфигу QP теперь ищется и в 32-, и в 64-битных ветках реестра.

### Quantumart.2.1.0

В рамках задачи #95388 реализовано:

* возможность генерации LINQ-to-SQL классов на лету с помощью T4-шаблона из nuget-пакета без использовании внешних утилит
* GetDefaultMapFileContents теперь генерирует маппинг на лету
