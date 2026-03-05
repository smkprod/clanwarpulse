# ClanWarReminder (MVP)

Clean Architecture SaaS backend for clan war tracking and automated reminders.

## Solution Structure

- `src/ClanWarReminder.Domain`: Core entities and enums.
- `src/ClanWarReminder.Application`: Use-case services and abstraction interfaces.
- `src/ClanWarReminder.Infrastructure`: EF Core, PostgreSQL, Clash Royale API client, messaging adapters.
- `src/ClanWarReminder.Api`: Command/query HTTP endpoints for bot adapters.
- `src/ClanWarReminder.Worker`: Background polling and reminder dispatch.

## Core Flows

1. `/commands/setup`
- Input: `platform`, `platformGroupId`, `clanTag`
- Stores or updates group-clan mapping.

2. `/commands/link`
- Input: `platform`, `platformGroupId`, `platformUserId`, `displayName`, `playerTag`
- Links platform user to Clash Royale player tag inside a configured group.

3. `/commands/status`
- Input: `platform`, `platformGroupId`
- Returns who has played and who has not in the current war.

4. Worker cycle (`WarReminderWorker`)
- Reads active groups.
- Fetches current war from Clash Royale.
- Resolves inactive linked players.
- Sends reminder via platform messenger.
- Persists reminder history (`userId`, `groupId`, `warKey`) to prevent duplicate reminders.

5. Telegram Mini App (`/miniapp`)
- React + MUI Web UI for `/setup`, `/link`, `/status` with Telegram WebApp context.
- Served from API static assets (`wwwroot/miniapp`).
- Validates Telegram `initData` through backend endpoint before using user identity.

### Mini App Frontend Workflow

- Source project: `src/ClanWarReminder.MiniApp`
- Dev server:
  - `cd src/ClanWarReminder.MiniApp`
  - `npm install`
  - `npm run dev`
- Production build:
  - `cd src/ClanWarReminder.MiniApp`
  - `npm run build`


## Data Model

- `Group` (`platform`, `platformGroupId`, `clanTag`, `isActive`)
- `User` (`platform`, `platformUserId`, `displayName`)
- `PlayerLink` (`userId`, `groupId`, `playerTag`)
- `Reminder` (`userId`, `groupId`, `warKey`, `sentAtUtc`)

## Configuration

Set in `appsettings.json` for API and Worker:

- `ConnectionStrings:DefaultConnection`
- `ClashRoyale:BaseUrl`
- `ClashRoyale:ApiToken`
- `Telegram:BotToken`
- `Telegram:MaxAuthAgeMinutes`
- `Worker:PollMinutes`

## Docker

Run API + Worker + PostgreSQL with Docker Compose:

1. Create `.env` from template:
   - `cp .env.example .env` (Linux/macOS)
   - `Copy-Item .env.example .env` (PowerShell)
2. Put real values into `.env`:
   - `CLASH_ROYALE_API_TOKEN`
   - `TELEGRAM_BOT_TOKEN`
   - `TELEGRAM_BOT_USERNAME`
3. Build and start:
   - `docker compose up --build -d`
4. API is available at:
   - `http://localhost:8080`
5. Stop containers:
   - `docker compose down`

### Telegram Bot Commands (no webhook required)

`TelegramCommandWorker` uses Telegram `getUpdates` polling, so bot commands work even on local Docker.

Use commands in the chat where the bot is added:

- `/help`
- `/setup #CLANTAG`
- `/link #PLAYERTAG`
- `/status`
- `/tagnotplayed`

Important:

- Bot must be added to the group chat.
- Bot token must be present in `.env` (`TELEGRAM_BOT_TOKEN`).
- For `/status` and war data, Clash Royale token/IP must be valid (no `403` from Clash API).

Files added for containerization:

- `Dockerfile.api`
- `Dockerfile.worker`
- `docker-compose.yml`
- `.dockerignore`

## Telegram Mini App Auth Validation

- Endpoint: `POST /miniapp/auth/telegram`
- Input: `{ \"initData\": \"<window.Telegram.WebApp.initData>\" }`
- Validation:
  - Verifies Telegram signature (`hash`) with bot token.
  - Validates `auth_date` freshness (`Telegram:MaxAuthAgeMinutes`).
  - Extracts trusted Telegram user identity from signed payload.

## Next Implementation Steps

1. Add admin-only authorization for bot commands in Telegram groups.
2. Persist Telegram polling offset in DB to survive worker restarts without reprocessing backlog.
3. Add integration tests for command flows and reminder deduplication.
4. Add richer command set (`/sync`, `/relink`, `/forecast`) directly in chat.
