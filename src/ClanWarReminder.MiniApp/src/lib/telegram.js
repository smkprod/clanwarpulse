export const webApp = window.Telegram?.WebApp;

export function initTelegramWebApp() {
  webApp?.ready();
  webApp?.expand();
}

export function supportsHaptics() {
  const version = Number.parseFloat(webApp?.version ?? "0");
  return Number.isFinite(version) && version >= 6.1 && Boolean(webApp?.HapticFeedback?.notificationOccurred);
}

export function notify(type) {
  if (supportsHaptics()) {
    webApp.HapticFeedback.notificationOccurred(type);
  }
}

export function openBotLink(url) {
  if (!url) {
    return;
  }

  if (webApp?.openTelegramLink) {
    webApp.openTelegramLink(url);
    return;
  }

  window.open(url, "_blank", "noopener,noreferrer");
}

export function getTelegramInitData() {
  return webApp?.initData ?? "";
}

export function getTelegramChatId() {
  const raw = webApp?.initDataUnsafe?.chat?.id;
  return raw !== undefined && raw !== null ? String(raw) : "";
}
