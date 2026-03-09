# Clan War Reminder Product Blueprint

## Positioning
Telegram Mini App and SaaS dashboard for Clash Royale clan war operations: current war control, participation tracking, opponent analytics, historical trends, and automated Telegram reminders.

## Core UX
- Mobile-first dashboard for leaders and active members.
- Blue premium visual language with compact cards and bottom navigation.
- Summary-first layout: current war state first, details on tap.

## Primary Sections
1. Home
2. Members
3. Opponents
4. History
5. Telegram
6. Settings

## MVP Features
- Telegram onboarding with Clash Royale tag validation
- Current clan war dashboard
- Played / inactive / partial participation views
- Player profile with recent war history
- Opponent comparison
- Join / leave membership log
- Reminder history and anti-spam reminders
- Forecasted final score with confidence

## Backend Services
- Clan sync worker
- War snapshot worker
- Membership change detector
- Reminder engine
- Prediction engine
- Mini app API

## Data Model
- Players
- Telegram users and links
- Clans
- Clan membership snapshots
- Clan membership events
- Wars
- War clan snapshots
- War participant snapshots
- Player war performance
- Reminder logs
- Prediction snapshots

## Prediction MVP
Forecast = current points + expected remaining player contribution.

Factors:
- current realized score
- time remaining
- active vs inactive players
- recent player averages
- recent clan trend
- war type adjustment
- consistency and participation probability

## Frontend Direction
- Bottom navigation on mobile
- Hero summary on home
- Sticky filters in members
- Dense cards on phone, wider tables on desktop
- Charts for trend and forecast only where they add decision value

## Post-MVP
- Better forecast tuning from stored actual-vs-predicted history
- Leader-specific admin tools
- Daily summary digests
- Deeper opponent trend modeling
