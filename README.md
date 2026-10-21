# FreeTeachClass

Нативное Windows-приложение — «умный» клиент Google Classroom для ученика и учителя:
загружает курсы и задания через официальный Google Classroom API, кэширует их локально
и показывает в удобном интерфейсе (WinUI 3). Сейчас — первый MVP: OAuth-вход,
синхронизация, дашборд, список предметов и заданий. AI-функции, XP и достижения
на этом этапе намеренно не реализованы.

Это не сайт и не Electron: обычный .NET/WinUI 3 процесс, запускающийся как
обычная Windows-программа (self-contained, отдельная установка .NET не нужна).

## Архитектура

Четыре слоя, зависимость всегда «внутрь», UI никогда не ходит в Google API напрямую:

```
src/
├── FreeTeachClass.Domain          # Модели (User, Course, Assignment, CourseMaterial)
│                                  # и интерфейсы: IClassroomDataSource, ILocalDataStore,
│                                  # ISecureTokenStore. Никаких зависимостей.
├── FreeTeachClass.Application     # Сервисы и сценарии: ClassroomSyncService,
│                                  # DashboardService, AuthenticationService.
│                                  # Определяет контракты IGoogleAuthGateway и др.
├── FreeTeachClass.Infrastructure  # Реализации: GoogleAuthGateway (OAuth 2.0 +
│                                  # PKCE), GoogleClassroomDataSource (Classroom API),
│                                  # GoogleClassroomMapper, JsonLocalDataStore,
│                                  # DpapiTokenStore (DPAPI Windows).
└── FreeTeachClass.App             # WinUI 3 / MVVM: страницы, ViewModel’и, DI,
                                   # навигация, тема, лог-файл, crash-лог.
tests/
├── FreeTeachClass.Application.Tests   # Юнит-тесты синхронизации и сервисов
└── FreeTeachClass.Infrastructure.Tests # Маппинг API, JSON-хранилище, DPAPI-токены
```

Поток данных синхронизации:

```
Google Classroom API → IClassroomDataSource → ClassroomSyncService (Application)
    → ILocalDataStore (JSON-кэш в %LOCALAPPDATA%) → ViewModel → UI
```

Экраны читают кэш, а не API: Google вызывается только при ручной/фоновой
синхронизации, поэтому открытие страницы не «долгое» и не упирается в квоты API.
Все внешние зависимости подключены через интерфейсы — реализации взаимозаменяемы.

## Стек

| Компонент | Выбор |
|---|---|
| Рантайм | .NET 8 (приложение self-contained, включая Windows App SDK) |
| UI | WinUI 3 + Windows App SDK 2.5.1, MVVM |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| DI / логирование | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging |
| Google API | `Google.Apis.Classroom.v1` (официальный клиент) |
| OAuth 2.0 | Системный браузер + loopback-редирект + PKCE (для installed apps) |
| Тесты | xunit (30 тестов) |
| Сборка | .NET SDK 10 (нужен для сборки), решение `FreeTeachClass.slnx` |

## Сборка и запуск

Требования: Windows 10 (1809+), .NET SDK 10.x (для сборки; для запуска SDK не нужен).

```powershell
# Сборка всего решения
dotnet build FreeTeachClass.slnx -c Debug -p:Platform=x64

# Запуск (проверенный путь сборки решения)
.\src\FreeTeachClass.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\FreeTeachClass.App.exe
```

> Замечание: при сборке проекта напрямую
> (`dotnet build src\FreeTeachClass.App\FreeTeachClass.App.csproj -c Debug -p:Platform=x64`)
> выход лежит в `bin\x64\Debug\...`. Оба каталога — валидная сборка; убедиться, что
> exe свежий, можно по времени файла (и в случае сомнений просто пересоберите решение).

Тесты:

```powershell
dotnet test FreeTeachClass.slnx -c Debug -p:Platform=x64
```

Ожидаемый результат: `Failed: 0` для обоих тестовых проектов (12 + 18 = 30 тестов).

## Настройка Google Cloud и OAuth

Приложение без OAuth-креденшелов запускается и показывает экран входа с подсказкой;
вход в Google доступен после одноразовой настройки.

### 1. Проект Google Cloud

1. Откройте <https://console.cloud.google.com/> и создайте новый проект.
2. **APIs & Services → Library** → включите **Google Classroom API**.
   (Google People API не нужен: почта и имя берутся из OpenID Connect userinfo.)

### 2. OAuth-консентент

1. **APIs & Services → OAuth consent screen**:
   тип **External**, заполните имя приложения, e-mail поддержки.
2. **Test users** → добавьте Google-аккаунты, которыми будете пользоваться
   (пока приложение в статусе Testing, другие аккаунты не смогут войти).
3. **Scopes → Add or remove scopes** — добавьте ровно эти (минимальный набор,
   только чтение; проверен по официальной документации Google, 2026-10):

   | Scope | Зачем |
   |---|---|
   | `openid` | Идентификация (OpenID Connect) |
   | `https://www.googleapis.com/auth/userinfo.profile` | Имя пользователя |
   | `https://www.googleapis.com/auth/userinfo.email` | E-mail пользователя |
   | `https://www.googleapis.com/auth/classroom.courses.readonly` | Список курсов |
   | `https://www.googleapis.com/auth/classroom.coursework.me.readonly` | Задания, назначенные пользователю |

   Никаких write-скоупов не используется — приложение ничего не меняет в Classroom.

### 3. OAuth-клиент

1. **APIs & Services → Credentials → Create credentials → OAuth client ID**.
2. Тип приложения: **Desktop app**.
3. Скопируйте Client ID и Client secret.

Официальная документация:
[OAuth 2.0 for installed apps](https://developers.google.com/identity/protocols/oauth2/native-app),
[Google Classroom API](https://developers.google.com/classroom/reference/rest).

## Передача OAuth-креденшелов приложению

Секреты не хранятся в репозитории и не входят в код. Два способа (порядок загрузки:
сначала переменные окружения, затем файл):

**Вариант A — переменные окружения:**

```powershell
$env:FREETEACHCLASS_GOOGLE_CLIENT_ID = "<client-id>.apps.googleusercontent.com"
$env:FREETEACHCLASS_GOOGLE_CLIENT_SECRET = "<client-secret>"
```

**Вариант B — файл** `%LOCALAPPDATA%\FreeTeachClass\oauth-client.json` (формат
Google-клиента):

```json
{
  "installed": {
    "client_id": "<client-id>.apps.googleusercontent.com",
    "client_secret": "<client-secret>"
  }
}
```

Этот файл (и каталог `%LOCALAPPDATA%\FreeTeachClass\` целиком) исключён из git через
`.gitignore`.

### Что происходит при входе

- Открывается системный браузер (учётные данные Google вводятся только на сайте
  Google — пароль приложение никогда не запрашивает и не хранит).
- Redirect идёт на локальный loopback-адрес (`http://127.0.0.1:<порт>/`),
  авторизация — по OAuth 2.0 для installed apps с PKCE.
- Токены сохраняются только в защищённом хранилище Windows (DPAPI, текущий
  пользователь); токены и личные данные никогда не пишутся в логи в открытом виде.

## Локальные данные

Каталог `%LOCALAPPDATA%\FreeTeachClass\`:

| Файл/папка | Назначение |
|---|---|
| `cache\` | JSON-кэш курсов/заданий (заполняется синхронизацией) |
| `tokens.dat` | Токены OAuth, зашифрованные DPAPI |
| `ui-settings.json` | Настройки интерфейса (тема и т.п.) |
| `crash.log` | Последний шанс лога: необработанные исключения (без токенов и PII) |
| `app-ГГГГММДД.log` | Обычный ход работы (уровень Information и выше, без чувствительных данных) |
| `oauth-client.json` | OAuth-клиент (создаётся вами, в репозиторий не попадает) |

## Структура экранов (MVP)

- **Welcome** — вход через Google или подсказка по настройке OAuth.
- **Dashboard** — статус синхронизации, ближайшие задания, сводка по курсам.
- **Courses** — список предметов.
- **Assignments** — задания с фильтром по предмету.
- **Settings** — тема, повторная синхронизация, выход.
- **Plan / AI-помощник / Achievements** — заглушки (этапы следующих версий).

## Ограничения MVP

- Только чтение данных Google Classroom (нет создания/изменения заданий).
- AI-функции, XP и достижения не реализованы (следующие этапы).
- Синхронизация курсов и заданий публикуемых в курсах; черновики не отображаются.
