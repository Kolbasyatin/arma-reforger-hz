# Arma Reforger lobby monitor — контекст проекта

## Цель

Создать самостоятельный кроссплатформенный C#/.NET-сервис, который получает список серверов Arma Reforger и данные очередей непосредственно через Steam и backend Bohemia Interactive.

Целевая цепочка уже подтверждена экспериментально:

```text
сохранённый Steam refresh token
  -> SteamKit2 -> Steam CM
  -> новый Steam Web API ticket для AppID 1874880
  -> BI Identity API -> BI access token
  -> BI lobby/rooms/search -> список комнат и очередей
```

Для работы не нужны:

- запущенный Arma Reforger;
- клиентский мод;
- Steam Desktop;
- dedicated server в роли crawler.

## Режим совместной работы

- Пользователь сам пишет код локально в Rider. Агент объясняет и предлагает изменения; не создаёт приложение вместо пользователя без прямого запроса.
- Общение на русском, короткими итерациями.
- Ответы должны быть максимально короткими: вывод и одна рекомендация, без длинных объяснений и перечисления альтернатив. Разворачивать конкретный пункт только по отдельной просьбе пользователя.
- Пользователь знаком с IDE, HTTP, backend-разработкой и общими принципами программирования. Не нужно объяснять очевидные IDE-действия или HTTP с нуля.
- C# изучается в процессе, поэтому новый для проекта синтаксис и особенности SteamKit/.NET нужно кратко объяснять.
- Любой новый термин, аббревиатуру или название технологии расшифровывать при первом упоминании (например: PoC — Proof of Concept, черновик для проверки идеи).
- Перед командой или изменением объяснить, зачем оно нужно и какой результат ожидается.
- Не выдавать большой объём архитектуры заранее. Сначала один проверяемый шаг, затем разбор результата.
- Текущий монолитный `Program.cs` — временный исследовательский PoC. После подтверждения полного сценария пользователь хочет написать чистовую версию с разделением ответственности.

## Технологические решения

- Язык: C#.
- Runtime/SDK: .NET 10, target framework `net10.0`.
- Текущий тип проекта: Console Application с top-level statements.
- IDE: JetBrains Rider.
- Steam: NuGet-пакет `SteamKit2` версии `3.4.0`.
- Целевая ОС: Linux. Разработка PoC сейчас идёт на Windows.
- Не применять WPF, DPAPI, Windows Credential Manager и другие Windows-only решения.
- Состояние аутентификации хранить кроссплатформенно в JSON. На Linux ограничивать права файла до `0600`.

## Что уже доказано

### Steam-аутентификация

- `SteamClient` успешно подключается к Steam CM.
- Вход по логину/паролю и Steam Guard через `BeginAuthSessionViaCredentialsAsync` работает.
- `IsPersistentSession = true` возвращает Steam refresh token.
- Повторный вход через сохранённый refresh token работает без пароля и повторного Steam Guard:

```csharp
steamUser.LogOn(new SteamUser.LogOnDetails
{
    Username = savedAuth.AccountName,
    AccessToken = savedAuth.RefreshToken,
    ShouldRememberPassword = true
});
```

- После работы использовать `Disconnect()`, а не явный `LogOff()`: `LogOff()` способен аннулировать persistent token.

### GuardData

`AuthPollResult.NewGuardData` — выданная Steam метка ранее подтверждённого устройства, главным образом для email Steam Guard. Её сохраняют по аккаунту и передают как `AuthSessionDetails.GuardData` при следующем полном входе по паролю. Она не является сессией, refresh token или игровым билетом. При исправном refresh token не используется; при мобильном Steam Guard часто может отсутствовать.

### Тип Steam ticket

Перехваченный билет настоящего Reforger был Base64-строкой длиной 336 символов и декодировался в 252 байта. Его бинарная структура была разобрана:

- Game Connect Token: 20 байт;
- session header: 24 байта;
- ticket type: `5`;
- App Ownership Ticket: 196 байт;
- AppID внутри билета: `1874880`;
- ticket type `5` в SteamKit означает `WebApiTicket`.

Следовательно, нужен именно:

```csharp
SteamAuthTicket.GetAuthTicketForWebApi(1874880, identity)
```

Главная неизвестная была `identity`. Эксперимент SteamKit -> BI завершился `HTTP 200`: Bohemia принимает билет с пустым значением:

```csharp
const uint appId = 1874880;
const string identity = "";
```

Никакой hook `steam_api64.dll` для определения `identity` больше не нужен.

### Размер билета SteamKit

SteamKit возвращает `TicketInfo.Ticket` размером 2560 байт, дополняя фактическую структуру случайными байтами. Reforger отправляет только фактическую длину. В успешном эксперименте:

```text
SteamKit buffer: 2560 bytes
Actual ticket: 252 bytes
```

Использованный алгоритм обрезки:

```csharp
static byte[] TrimWebApiTicket(byte[] ticket)
{
    var gameConnectTokenLength =
        BinaryPrimitives.ReadInt32LittleEndian(ticket.AsSpan(0, 4));

    var sessionLengthOffset = 4 + gameConnectTokenLength;

    var sessionLength =
        BinaryPrimitives.ReadInt32LittleEndian(
            ticket.AsSpan(sessionLengthOffset, 4));

    var ownershipLengthOffset =
        sessionLengthOffset + 4 + sessionLength;

    var ownershipLength =
        BinaryPrimitives.ReadInt32LittleEndian(
            ticket.AsSpan(ownershipLengthOffset, 4));

    var actualLength =
        ownershipLengthOffset + 4 + ownershipLength;

    return ticket[..actualLength];
}
```

`TicketInfo` должен оставаться живым до завершения обмена с BI. После обмена его можно `Dispose()`, тем самым отменив билет. Steam ticket не нужно сохранять или повторно использовать.

## Подтверждённые BI API

Данные сняты с Arma Reforger `1.8.0.10` 29 августа 2026 года. Версии могут измениться после обновления игры, поэтому в чистовой реализации их нужно вынести в конфигурацию/метаданные протокола.

### 1. Steam ticket -> BI access token

```http
POST https://api-ar-id.bistudio.com/game-identity/api/v1.1/identities/reforger/auth?include=profile
Content-Type: application/json
User-Agent: Arma Reforger/1.8.0.10 (Client; Windows)
```

```json
{
  "platform": "steam",
  "token": "<Base64 фактических байтов Steam ticket>",
  "platformOpts": {
    "appId": "1874880"
  }
}
```

Ответ содержит:

```json
{
  "identityId": "...",
  "accessToken": "<BI JWT>",
  "accessTokenExp": 0,
  "identity": {}
}
```

Экспериментальный запрос из собственного C#-процесса получил `HTTP 200 OK`. BI JWT имел срок жизни 3600 секунд.

Минимальная модель:

```csharp
sealed record BiAuthResponse(
    string IdentityId,
    string AccessToken,
    long AccessTokenExp);
```

### 2. Необязательный game session login

Настоящий клиент вызывает:

```http
POST https://api-ar-game.bistudio.com/game-api/api/v1.0/session/login
```

```json
{
  "accessToken": "<BI access token>",
  "clientVersion": "1.8.0",
  "platformId": "ReforgerSteam",
  "gameVersion": "1.8.0.10",
  "platformUsername": "<Steam display name>"
}
```

Ответ содержит профиль, страну, play time, совместимые версии, арендованные серверы, уведомления, микротранзакции и `sessionId`.

Для `lobby/rooms/search` возвращённый `sessionId` не передаётся ни в JSON, ни в замеченных заголовках/cookie. Вызов `session/login` можно пока оставить для повторения поведения настоящего клиента, но для read-only мониторинга он, вероятно, не нужен. Это можно подтвердить отдельным A/B-тестом: новый BI token -> сразу rooms/search без session/login.

### 3. Поиск комнат

```http
POST https://api-ar-game.bistudio.com/game-api/api/v1.0/lobby/rooms/search
Content-Type: application/json
```

Рабочее тело запроса:

```json
{
  "directJoinCode": "",
  "hostAddress": "",
  "order": "PlayerCount",
  "scenarioId": "",
  "includePing": 0,
  "text": "",
  "minPlayersPercent": 0,
  "maxPlayersPercent": 100,
  "minPlayersCount": 0,
  "maxPlayersCount": 256,
  "modded": false,
  "ascendent": false,
  "gameClientFilter": "AnyCompatible",
  "accessToken": "<BI access token>",
  "clientVersion": "1.8.0",
  "platformId": "ReforgerSteam",
  "gameClientType": "PLATFORM_PC",
  "lightweight": true,
  "from": 0,
  "limit": 50,
  "pingValues": []
}
```

Важно: `ascendent` обязателен. Если он отсутствует/null, backend возвращает `InvalidInput`.

Запрос с BI access token, полученным полностью через SteamKit, успешно вернул список комнат. Это финальное подтверждение основной гипотезы.

Для известного сервера можно заполнять `hostAddress` значением вида:

```text
1.2.3.4:2001
```

## Нужные поля ответа lobby

В ответе комнаты интересуют как минимум:

- идентификатор/адрес и имя комнаты;
- `playerCount`;
- `playerCountLimit`;
- `joinable`;
- версия и сценарий;
- признак модифицированного сервера;
- `joinQueue.type`;
- `joinQueue.size`;
- `joinQueue.maxSize`;
- `joinQueue.positionAvgWaitTime`.

`positionAvgWaitTime` иногда отсутствует, особенно при пустой очереди, поэтому в DTO поле должно быть nullable.

## Жизненный цикл секретов и токенов

```text
Steam refresh token
  долгоживущий; хранится локально; используется для повторного Steam logon

GuardData
  хранится локально; нужен только как помощь при fallback-входе по паролю

Steam Web API ticket
  создаётся заново для каждого обмена с BI; не сохраняется; после обмена отменяется

BI access token
  живёт примерно 1 час; используется во всех lobby-запросах до истечения
```

В чистовой версии не нужно получать Steam ticket на каждый опрос каталога. Нормальный цикл:

```text
получить один BI access token
  -> выполнять много lobby-запросов
  -> обновить BI token незадолго до accessTokenExp или после 401/403
```

При частых перезапусках можно сохранять BI access token вместе с `accessTokenExp` и использовать его до истечения. Steam refresh token обновлять при каждом входе не требуется. Если Steam когда-либо вернёт новый непустой refresh token, сохранённое значение нужно заменить атомарно.

## Локальное хранение

Сохранять Steam auth state вне репозитория, например через:

```csharp
Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
```

Это кроссплатформенно: на Linux обычно соответствует `~/.local/share`.

Пример состояния:

```json
{
  "accountName": "...",
  "refreshToken": "...",
  "guardData": "..."
}
```

На Linux после создания файла:

```csharp
File.SetUnixFileMode(
    authFile,
    UnixFileMode.UserRead | UnixFileMode.UserWrite);
```

Не хранить реальные пароли, Steam tickets, refresh tokens, BI JWT, SteamID, identityId, IP или sessionId в Git, `AGENTS.md`, тестовых fixtures и логах.

## Текущее состояние PoC

Подтверждён успешный вывод примерно следующего вида:

```text
Connected to Steam CM
Logging on with saved refresh token
Logon result: OK
SteamKit buffer: 2560 bytes
Actual ticket: 252 bytes
BI response: 200 OK
Lobby response: 200 OK
Disconnected from Steam CM
```

PoC пока линейный и расположен главным образом в `Program.cs`. Не надо повторно исследовать уже доказанные пункты. Перед рефакторингом следующий агент должен сначала прочитать фактический текущий код пользователя.

## Предлагаемая следующая итерация

Начать чистовую реализацию небольшими шагами:

1. Зафиксировать текущий рабочий PoC отдельным commit/tag.
2. Выделить модели конфигурации и auth state без изменения поведения.
3. Выделить сохранение/загрузку Steam auth state с атомарной записью и правами `0600`.
4. Выделить Steam-аутентификацию и получение Web API ticket.
5. Выделить BI Identity client и BI Lobby client на `HttpClient`.
6. Добавить кэш BI access token и обновление по `accessTokenExp`/401/403.
7. Создать DTO ответа комнат, отдельно учесть nullable queue fields.
8. Добавить поиск конкретного `hostAddress`, затем периодический polling.
9. Только после этого выбирать формат постоянного сервиса, БД/метрики и deployment под Linux.

Названия классов и окончательную архитектуру не считать уже принятыми: обсуждать по одной итерации с пользователем.

## Незавершённые проверки

- Обязателен ли `session/login` перед `rooms/search` для полностью новой BI-сессии.
- Как именно и когда Steam refresh token обновляется/ротируется в длительно работающем сервисе; сохранять новый token, если SteamKit его возвращает.
- Нужен ли keepalive для BI game session при долгой работе.
- Rate limits Steam и BI при выбранной частоте обновлений.
- Поведение при обновлении версии Reforger/API.
- После завершения сетевого исследования удалить установленный корневой сертификат mitmproxy из доверенных сертификатов Windows.

## Полезные первичные источники

- SteamKit: <https://github.com/SteamRE/SteamKit>
- SteamKit `SteamAuthTicket`: <https://github.com/SteamRE/SteamKit/blob/master/SteamKit2/SteamKit2/Steam/Handlers/SteamAuthTicket/SteamAuthTicket.cs>
- Steam authentication and ownership: <https://partner.steamgames.com/doc/features/auth>
- `ISteamUser::GetAuthTicketForWebApi`: <https://partner.steamgames.com/doc/api/ISteamUser#GetAuthTicketForWebApi>
