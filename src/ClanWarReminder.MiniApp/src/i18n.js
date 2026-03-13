export const translations = {
  ru: {
    appName: "Clan War Control",
    signInBadge: "Clan War Companion",
    signInTitle: "Современный центр управления войной клана",
    signInText:
      "Подключите тег игрока и откройте понятный дашборд с активностью клана, статистикой игрока и быстрым доступом к Telegram-связке.",
    signInHintTitle: "После входа будет доступно",
    signInHintText:
      "Главная статистика, вкладка КВ, карточка клана, история игрока по прошлым войнам и настройки темы/языка.",
    playerTag: "Тег игрока",
    connect: "Подключить",
    restoreSuccess: (name) => `Вход восстановлен для ${name}.`,
    loginSuccess: (name, tag) => `Загружен игрок ${name} (${tag}).`,
    dashboardUpdated: "Данные обновлены.",
    notifyDone: (linked, unlinked) => `Уведомления отправлены: привязанных ${linked}, непривязанных ${unlinked}.`,
    notifySkipped: "Уведомления не были отправлены.",
    noTelegramGroup: "Для этого клана Telegram-группа не настроена. Сначала выполните /setup #CLANTAG в чате.",
    telegramGroupMissing: "Для этого клана Telegram-чат не настроен.",
    telegramSyncUpdated: "Синхронизация Telegram обновлена.",
    unlinkSuccess: "Текущий тег отвязан. Можно подключить другой аккаунт.",
    unlinkLocalOnly: "Локальная сессия очищена. Для полного отвязывания откройте mini app из Telegram.",
    unexpectedError: "Произошла непредвиденная ошибка.",
    sessionTitle: (player, clan) => `${player} · ${clan}`,
    sessionSubtitle: (tag, clanTag) => `${tag} · ${clanTag}`,
    tabs: { home: "Главная", war: "КВ", clan: "Клан", settings: "Настройки" },
    home: {
      label: "Обзор",
      title: "Главная панель клана",
      subtitle: "Ключевые показатели игрока и текущей речной гонки в одном экране.",
      cards: {
        clanName: "Название клана",
        playerName: "Никнейм игрока",
        clanPoints: "Очки клана",
        medals: "Медали",
        boatPoints: "Очки лодки",
        avgDeck: "Среднее за 1 колоду"
      },
      cardsHelp: {
        clanName: "Текущий клан игрока",
        playerName: "Активный связанный профиль",
        clanPoints: "Сумма fame и repair points",
        medals: "Текущее количество fame",
        boatPoints: "Текущие repair points",
        avgDeck: "Средний вклад игрока за бой по доступной истории"
      },
      warStatus: "Статус войны",
      currentWar: "Текущая КВ",
      memberStatus: "Статус игрока",
      memberStatusValue: (played, left) => `${played}/4 сыграно · осталось ${left}`,
      actions: { refresh: "Обновить", notify: "Напомнить", openBot: "Открыть бота" },
      quickStats: { active: "Сыграли", inactive: "Не сыграли", members: "Участников", linked: "Привязано в Telegram" }
    },
    war: {
      title: "Клановая война",
      subtitle: "Кто уже сыграл, кто ещё в риске и сколько очков команда набрала прямо сейчас.",
      warInactive: "Сейчас тренировочные дни: боевые списки и риски появятся только в активное окно войны.",
      played: "Сыграли",
      notPlayed: "Не сыграли",
      allDone: "Все участники уже закрыли доступные бои.",
      nobodyPlayed: "Пока никто не начал играть.",
      remainingBattles: "Осталось боёв",
      totalContribution: "Очки клана",
      avgPerBattle: "Среднее за бой",
      telegram: {
        title: "Telegram-синхронизация",
        linked: "Привязано",
        relink: "Перепривязать",
        refresh: "Обновить синхронизацию",
        noUsers: "Пока нет привязанных пользователей Telegram.",
        noGroup: "Для этого клана Telegram-чат ещё не настроен.",
        relinkLabel: "Новый тег игрока"
      }
    },
    clan: {
      title: "Клан",
      subtitle: "Полная карточка текущего клана, состав участников и история прошлых войн.",
      region: "Регион",
      trophies: "Трофеи клана",
      members: "Участники",
      topContributors: "Лучшие участники текущей гонки",
      recentWars: "Прошлые войны",
      noData: "Данные клана пока недоступны.",
      memberOpenHint: "Нажмите на участника, чтобы открыть его подробную статистику."
    },
    settings: {
      title: "Настройки",
      subtitle: "Персонализация интерфейса, язык приложения и управление привязанным игроком.",
      appearance: "Оформление",
      theme: "Тема",
      light: "Светлая",
      dark: "Тёмная",
      language: "Язык интерфейса",
      account: "Аккаунт",
      linkedTag: "Текущий тег",
      linkedPlayer: "Игрок",
      unlink: "Отвязать тег",
      syncText: "Если отвязка выполняется из Telegram mini app, связка игрока будет удалена и можно будет подключить другой тег.",
      telegramLink: "Чат клана",
      yes: "Да",
      no: "Нет"
    },
    profile: {
      back: "Назад",
      title: "Профиль игрока",
      refresh: "Обновить профиль",
      range: "Окно КВ",
      recentWars: "История по КВ",
      recentClans: "История кланов",
      why: "Как считается прогноз",
      whyText:
        "Оценка строится по последним доступным клановым войнам, с акцентом на участие, средний вклад за бой и winrate по боевым колодам.",
      metrics: {
        participation: "Участие",
        avgBattles: "Среднее боёв",
        winRate: "Winrate КВ",
        prediction: "Следующая КВ",
        current: "Текущая КВ",
        avgWeek: "Среднее очков",
        avgBattle: "За 1 бой",
        fullCompletion: "Полное закрытие",
        form: "Текущая форма"
      },
      status: "Статус",
      dataQuality: "Качество данных",
      noProfile: "Профиль игрока пока не загружен.",
      noClanHistory: "История кланов пока недоступна.",
      noWarHistory: "История по войнам пока недоступна."
    }
  },
  en: {
    appName: "Clan War Control",
    signInBadge: "Clan War Companion",
    signInTitle: "A modern control room for your clan war",
    signInText:
      "Connect a player tag and open a clean dashboard with clan activity, player performance and quick Telegram linkage.",
    signInHintTitle: "After sign in",
    signInHintText:
      "Home overview, War activity tab, clan roster, player war history and appearance/language settings will be available.",
    playerTag: "Player tag",
    connect: "Connect",
    restoreSuccess: (name) => `Session restored for ${name}.`,
    loginSuccess: (name, tag) => `Loaded player ${name} (${tag}).`,
    dashboardUpdated: "Data refreshed.",
    notifyDone: (linked, unlinked) => `Notifications sent: linked ${linked}, unlinked ${unlinked}.`,
    notifySkipped: "Notifications were not sent.",
    noTelegramGroup: "Telegram group is not configured for this clan yet. Run /setup #CLANTAG in chat first.",
    telegramGroupMissing: "Telegram chat is not configured for this clan.",
    telegramSyncUpdated: "Telegram sync refreshed.",
    unlinkSuccess: "The current tag has been unlinked. You can connect another account now.",
    unlinkLocalOnly: "Local session cleared. Open the mini app from Telegram to fully unlink the player.",
    unexpectedError: "Unexpected error.",
    sessionTitle: (player, clan) => `${player} · ${clan}`,
    sessionSubtitle: (tag, clanTag) => `${tag} · ${clanTag}`,
    tabs: { home: "Home", war: "War", clan: "Clan", settings: "Settings" },
    home: {
      label: "Overview",
      title: "Clan home",
      subtitle: "Key player and river race stats in one place.",
      cards: {
        clanName: "Clan name",
        playerName: "Player",
        clanPoints: "Clan points",
        medals: "Medals",
        boatPoints: "Boat points",
        avgDeck: "Average per deck"
      },
      cardsHelp: {
        clanName: "Current player clan",
        playerName: "Active linked profile",
        clanPoints: "Combined fame and repair points",
        medals: "Current fame total",
        boatPoints: "Current repair points",
        avgDeck: "Average contribution per battle from available history"
      },
      warStatus: "War status",
      currentWar: "Current war",
      memberStatus: "Player status",
      memberStatusValue: (played, left) => `${played}/4 played · ${left} left`,
      actions: { refresh: "Refresh", notify: "Notify", openBot: "Open bot" },
      quickStats: { active: "Played", inactive: "Not played", members: "Members", linked: "Telegram linked" }
    },
    war: {
      title: "Clan war",
      subtitle: "See who has played, who is still at risk and how much your team has earned right now.",
      warInactive: "It is currently a training window. Active war lists will appear once the war is live.",
      played: "Played",
      notPlayed: "Not played",
      allDone: "All members have already finished their available battles.",
      nobodyPlayed: "Nobody has started yet.",
      remainingBattles: "Battles left",
      totalContribution: "Clan points",
      avgPerBattle: "Avg per battle",
      telegram: {
        title: "Telegram sync",
        linked: "Linked",
        relink: "Relink",
        refresh: "Refresh sync",
        noUsers: "No Telegram users linked yet.",
        noGroup: "Telegram chat is not configured for this clan yet.",
        relinkLabel: "New player tag"
      }
    },
    clan: {
      title: "Clan",
      subtitle: "Full clan card, current roster and recent war history.",
      region: "Region",
      trophies: "Clan trophies",
      members: "Members",
      topContributors: "Top contributors",
      recentWars: "Recent wars",
      noData: "Clan data is not available yet.",
      memberOpenHint: "Tap a member to open detailed player stats."
    },
    settings: {
      title: "Settings",
      subtitle: "Interface personalization, language and linked player management.",
      appearance: "Appearance",
      theme: "Theme",
      light: "Light",
      dark: "Dark",
      language: "Language",
      account: "Account",
      linkedTag: "Current tag",
      linkedPlayer: "Player",
      unlink: "Unlink tag",
      syncText: "When used inside the Telegram mini app, unlinking removes the saved player binding so another tag can be attached.",
      telegramLink: "Clan chat",
      yes: "Yes",
      no: "No"
    },
    profile: {
      back: "Back",
      title: "Player profile",
      refresh: "Refresh profile",
      range: "War range",
      recentWars: "War history",
      recentClans: "Clan history",
      why: "How the forecast works",
      whyText:
        "The forecast uses recent clan wars, with emphasis on participation, average contribution per battle and battle-deck win rate.",
      metrics: {
        participation: "Participation",
        avgBattles: "Avg battles",
        winRate: "War winrate",
        prediction: "Next war",
        current: "Current war",
        avgWeek: "Avg points",
        avgBattle: "Per battle",
        fullCompletion: "Full completion",
        form: "Current form"
      },
      status: "Status",
      dataQuality: "Data quality",
      noProfile: "Player profile is not loaded yet.",
      noClanHistory: "Clan history is not available yet.",
      noWarHistory: "War history is not available yet."
    }
  }
};

export function getCopy(language) {
  return translations[language] ?? translations.ru;
}

export function getLocale(language) {
  return language === "en" ? "en-US" : "ru-RU";
}
