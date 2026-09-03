# arma-reforger-hz

Сервис получает BI access token Arma Reforger через Steam и отдаёт его по HTTP другим сервисам (мониторинг, telegram notifier).

```text
steam-auth.json (refresh token)
  -> вход в Steam CM без пароля
  -> Steam Web API ticket для AppID 1874880
  -> BI Identity API -> BI access token (~1 час)
  -> GET /token
```

Не требует запущенной игры, Steam Desktop и клиентских модов.

## Структура

```text
ArmaReforger.Identity/   библиотека: Steam-аутентификация, билет, BI Identity client
ArmaReforger.Service/    веб-сервис: фоновое обновление токена + GET /token
ArmaReforger.Cli/        интерактивный Steam-логин (один раз, руками)
deploy/                  Dockerfile, docker-compose, systemd unit, скрипты
.github/workflows/       сборка образа и публикация в GHCR
```

## Как это работает

1. **CLI** (один раз): логин, пароль, код Steam Guard → сохраняет `steam-auth.json` с refresh token.
2. **Сервис** (постоянно): при старте и далее по таймеру читает файл, входит в Steam по refresh token, берёт билет, обменивает его в BI на access token, кладёт в память.
3. **`GET /token`** отдаёт то, что лежит в памяти. Пусто — `404`.

Расписание обновления: при успехе — за 5 минут до истечения токена (`TokenRefresh:RefreshLeadTime`), при ошибке — через 1 минуту (`TokenRefresh:RetryDelay`). Отсутствие файла — тоже «ошибка»: сервис не падает, а ждёт минуту и пробует снова. Перезапуск после логина не нужен.

Refresh token — bearer: кто владеет файлом, тот входит в аккаунт. Файл создаётся с правами `0600`, в Git не попадает.

## API

### `GET /token`

```json
200 OK
{
  "accessToken": "<BI JWT>",
  "expiresAt": "2026-09-03T15:04:05+00:00"
}
```

`404 Not Found` — токена пока нет (сервис только стартовал, нет `steam-auth.json`, Steam или BI недоступны). Смотреть лог сервиса.

## Локальный запуск

Нужен .NET 10 SDK.

```bash
dotnet build

# терминал 1 — сервис
dotnet run --project ArmaReforger.Service
# слушает http://localhost:5283 (порт из Properties/launchSettings.json)

# терминал 2 — логин (один раз)
dotnet run --project ArmaReforger.Cli

# проверка
curl http://localhost:5283/token
```

Файл состояния по умолчанию: `~/.local/share/ArmaReforgerMonitor/steam-auth.json` (Linux), `%LOCALAPPDATA%\ArmaReforgerMonitor\steam-auth.json` (Windows).

Ожидаемый лог сервиса после логина:

```text
Connected to Steam CM
Logged on to Steam as ...
Web API ticket acquired: 252 of 2560 bytes
BI access token received, expires at ...
Next BI token refresh in 00:54:xx
```

### Steam Guard

Код отправляет сам Steam при каждом запуске CLI; отдельного «переслать код» нет. Если письмо не пришло — подождать 1–2 минуты, проверить спам и запустить CLI заново. При мобильном Steam Guard письма не будет: код берётся из приложения Steam на телефоне.

## Конфигурация

Читается из `appsettings.json` и переменных окружения (`Секция__Ключ`). CLI и сервис используют одни и те же переменные.

| Ключ | По умолчанию | Назначение |
|---|---|---|
| `Steam__AuthStateFilePath` | LocalApplicationData | путь к `steam-auth.json` |
| `Steam__AppId` | `1874880` | AppID Arma Reforger |
| `Steam__OperationTimeout` | `00:00:30` | таймаут подключения и логина |
| `Bohemia__IdentityBaseAddress` | `https://api-ar-id.bistudio.com/` | BI Identity API |
| `Bohemia__UserAgent` | `Arma Reforger/1.8.0.10 (Client; Windows)` | меняется с версией игры |
| `TokenRefresh__RefreshLeadTime` | `00:05:00` | за сколько до истечения обновлять |
| `TokenRefresh__RetryDelay` | `00:01:00` | пауза после ошибки |

## Docker

Один образ содержит и сервис, и CLI. Собирать из корня репозитория:

```bash
docker build -f deploy/Dockerfile -t arma-reforger-hz .

# сервис
docker run -d --name arma-reforger-hz -p 127.0.0.1:8080:8080 -v ./data:/data arma-reforger-hz

# логин
docker run --rm -it -v ./data:/data --entrypoint dotnet arma-reforger-hz cli/ArmaReforger.Cli.dll
```

В контейнере сервис слушает **8080** (не 5283), состояние лежит в `/data/steam-auth.json`. Контейнер работает не от root, поэтому каталог `data` должен быть доступен на запись.

## Деплой

Схема: GitHub Actions собирает образ и публикует в `ghcr.io/kolbasyatin/arma-reforger-hz` при push в `master` (теги `latest` и `sha-<коммит>`). Сервер тянет образ и запускает через systemd.

### Сервер, первый раз

```bash
sudo mkdir -p /opt/arma-reforger-hz && cd /opt/arma-reforger-hz
sudo cp deploy/docker-compose.yml deploy/steam-login.sh .

# если пакет в GHCR приватный:
docker login ghcr.io   # PAT с правом read:packages

sudo cp deploy/arma-reforger.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now arma-reforger

./steam-login.sh       # логин, пароль, код -> data/steam-auth.json
```

Сервис подхватит файл в течение минуты. Проверка:

```bash
docker logs -f arma-reforger-hz
```

### Обновление

```bash
sudo systemctl restart arma-reforger    # pull делается на старте
```

Откат на конкретный коммит — поменять тег в `docker-compose.yml` с `latest` на `sha-xxxxxxx` и перезапустить.

### Повторный логин

Нужен, если Steam отозвал refresh token (в логе `Steam logon failed`) или устройство отозвано в настройках аккаунта Steam. Сервис останавливать не нужно:

```bash
cd /opt/arma-reforger-hz && ./steam-login.sh
```

### Сеть

Порт наружу в `docker-compose.yml` не публикуется. Интеграция с telegram notifier (общая docker-сеть или проброс порта) настраивается отдельно.

## Секреты

Не хранить в Git и логах: пароль Steam, `steam-auth.json`, Steam ticket, BI JWT, SteamID, identityId. Каталог `data/` на сервере — вне репозитория.
