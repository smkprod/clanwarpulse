import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  BottomNavigation,
  BottomNavigationAction,
  Box,
  CircularProgress,
  Container,
  CssBaseline,
  Paper,
  Stack,
  ThemeProvider,
  Typography
} from "@mui/material";
import { apiGet, apiPost } from "./api/client";
import { DashboardPage } from "./pages/DashboardPage";
import { PlayerProfilePage } from "./pages/PlayerProfilePage";
import { SignInPage } from "./pages/SignInPage";
import { getTelegramChatId, getTelegramInitData, initTelegramWebApp, notify, openBotLink } from "./lib/telegram";
import { appTheme } from "./theme";

initTelegramWebApp();

const SESSION_STORAGE_KEY = "clanwarreminder.playerTag";
const MOBILE_TABS = ["Home", "Members", "Opponents", "History", "Clans", "Bot"];

export default function App() {
  const [playerTag, setPlayerTag] = useState("");
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState({
    text: "Введите тег игрока или дождитесь автоматического входа через Telegram.",
    severity: "info"
  });
  const [session, setSession] = useState(null);
  const [tab, setTab] = useState(0);
  const [selectedClanTag, setSelectedClanTag] = useState("");
  const [clanDetails, setClanDetails] = useState(null);
  const [telegramSync, setTelegramSync] = useState(null);
  const [playerProfile, setPlayerProfile] = useState(null);
  const [currentView, setCurrentView] = useState("dashboard");
  const [profileWindowWeeks, setProfileWindowWeeks] = useState(5);

  const titleLine = useMemo(() => {
    if (!session) {
      return "Первый вход: Telegram + тег Clash Royale";
    }

    return `${session.identity.playerName} | ${session.identity.clanName}`;
  }, [session]);

  useEffect(() => {
    const savedTag = window.localStorage.getItem(SESSION_STORAGE_KEY);
    if (savedTag) {
      setPlayerTag(savedTag);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function restoreSession() {
      const telegramInitData = getTelegramInitData();
      if (!telegramInitData || session) {
        return;
      }

      try {
        setBusy(true);
        const data = await apiPost("/miniapp/auth/restore", {
          telegramInitData,
          telegramChatId: getTelegramChatId()
        });

        if (cancelled || !data?.restored || !data?.session) {
          if (data?.message) {
            setFeedback({ text: data.message, severity: "info" });
          }
          return;
        }

        setSession(data.session);
        setPlayerTag(data.session.identity.playerTag);
        window.localStorage.setItem(SESSION_STORAGE_KEY, data.session.identity.playerTag);
        await loadTelegramSync(data.session.identity.playerTag);
        await loadPlayerProfile(data.session.identity.playerTag);
        setCurrentView("dashboard");
        setFeedback({
          text: `Автоматически восстановлен вход для ${data.session.identity.playerName}.`,
          severity: "success"
        });
      } catch (error) {
        if (!cancelled) {
          setFeedback({
            text: error.message || "Не удалось восстановить вход через Telegram.",
            severity: "warning"
          });
        }
      } finally {
        if (!cancelled) {
          setBusy(false);
        }
      }
    }

    restoreSession();
    return () => {
      cancelled = true;
    };
  }, [session]);

  async function loginByTag() {
    await runBusy(async () => {
      const data = await apiPost("/miniapp/auth/player", {
        playerTag,
        telegramInitData: getTelegramInitData(),
        telegramChatId: getTelegramChatId()
      });
      setSession(data);
      window.localStorage.setItem(SESSION_STORAGE_KEY, data.identity.playerTag);
      setPlayerTag(data.identity.playerTag);
      setTab(0);
      setClanDetails(null);
      setSelectedClanTag("");
      await loadTelegramSync(data.identity.playerTag);
      await loadPlayerProfile(data.identity.playerTag);
      setCurrentView("dashboard");
      setFeedback({
        text: `Загружен игрок ${data.identity.playerName} (${data.identity.playerTag})`,
        severity: "success"
      });
      notify("success");
    });
  }

  async function refreshDashboard() {
    if (!session?.identity?.playerTag) {
      return;
    }

    await runBusy(async () => {
      const data = await apiGet(
        `/miniapp/player/dashboard?playerTag=${encodeURIComponent(session.identity.playerTag)}`
      );
      setSession(data);
      window.localStorage.setItem(SESSION_STORAGE_KEY, data.identity.playerTag);
      setClanDetails(null);
      setSelectedClanTag("");
      await loadTelegramSync(data.identity.playerTag);
      await loadPlayerProfile(data.identity.playerTag);
      setCurrentView("dashboard");
      setFeedback({ text: "Данные войны клана обновлены.", severity: "success" });
      notify("success");
    });
  }

  async function loadClanDetails(clanTag) {
    await runBusy(async () => {
      const data = await apiGet(`/miniapp/clan/details?clanTag=${encodeURIComponent(clanTag)}`);
      setSelectedClanTag(clanTag);
      setClanDetails(data);
      setFeedback({ text: `Загружена статистика клана ${data.clanName}.`, severity: "success" });
      notify("success");
    });
  }

  async function notifyNotPlayed() {
    if (!session?.linkedTelegramGroupId) {
      setFeedback({
        text: "Для этого клана не настроена Telegram-группа. Сначала выполните /setup #CLANTAG в чате.",
        severity: "warning"
      });
      return;
    }

    await runBusy(async () => {
      const result = await apiPost("/commands/notify/not-played", {
        platform: "Telegram",
        platformGroupId: session.linkedTelegramGroupId
      });

      if (!result.sent) {
        setFeedback({ text: result.text ?? "Уведомления не отправлены.", severity: "info" });
        return;
      }

      setFeedback({
        text: `Отметки отправлены: привязанных ${result.linkedTargets}, непривязанных ${result.unlinkedTargets}.`,
        severity: "success"
      });
      notify("success");
    });
  }

  async function loadTelegramSync(sourcePlayerTag = null) {
    const tag = sourcePlayerTag ?? session?.identity?.playerTag;
    if (!tag) {
      return;
    }

    const data = await apiGet(`/miniapp/telegram/sync?playerTag=${encodeURIComponent(tag)}`);
    setTelegramSync(data);
  }

  async function loadPlayerProfile(sourcePlayerTag = null, windowWeeks = profileWindowWeeks) {
    const tag = sourcePlayerTag ?? session?.identity?.playerTag;
    if (!tag) {
      return;
    }

    const data = await apiGet(
      `/miniapp/player/profile?playerTag=${encodeURIComponent(tag)}&windowWeeks=${encodeURIComponent(windowWeeks)}`
    );
    setPlayerProfile(data);
  }

  async function openPlayerProfile(tag) {
    await runBusy(async () => {
      await loadPlayerProfile(tag, profileWindowWeeks);
      setCurrentView("player");
    });
  }

  async function relinkTelegramUser(platformUserId, displayName, targetPlayerTag) {
    if (!session?.linkedTelegramGroupId) {
      setFeedback({
        text: "Для этого клана не настроена Telegram-группа.",
        severity: "warning"
      });
      return;
    }

    await runBusy(async () => {
      await apiPost("/miniapp/telegram/relink", {
        platformGroupId: session.linkedTelegramGroupId,
        platformUserId,
        displayName,
        playerTag: targetPlayerTag
      });
      await loadTelegramSync(session.identity.playerTag);
      setFeedback({ text: "Привязка Telegram обновлена.", severity: "success" });
      notify("success");
    });
  }

  async function runBusy(action) {
    try {
      setBusy(true);
      await action();
    } catch (error) {
      setFeedback({ text: error.message || "Произошла непредвиденная ошибка.", severity: "error" });
      notify("error");
    } finally {
      setBusy(false);
    }
  }

  return (
    <ThemeProvider theme={appTheme}>
      <CssBaseline />
      <Container
        maxWidth="md"
        sx={{
          py: 2.5,
          pb: session && currentView === "dashboard" ? { xs: 12, sm: 4 } : 4,
          overflowX: "hidden"
        }}
      >
        <Stack spacing={1.6} sx={{ mb: 2.5 }}>
          <Typography variant="h4">Clan War Control</Typography>
          <Typography variant="body2" sx={{ color: "#b3d3e8", overflowWrap: "anywhere" }}>
            {titleLine}
          </Typography>
        </Stack>

        {feedback.text && (
          <Alert severity={feedback.severity} sx={{ mb: 2 }}>
            {feedback.text}
          </Alert>
        )}

        {!session && (
          <SignInPage
            playerTag={playerTag}
            onPlayerTagChange={setPlayerTag}
            onSignIn={loginByTag}
            busy={busy}
          />
        )}

        {session && currentView === "dashboard" && (
          <>
            <DashboardPage
              tab={tab}
              onTabChange={setTab}
              dashboard={session.dashboard}
              identity={session.identity}
              authorizedTags={session.authorizedPlayerTags ?? []}
              selectedClanTag={selectedClanTag}
              clanDetails={clanDetails}
              onRefresh={refreshDashboard}
              onLoadClanDetails={loadClanDetails}
              onOpenBot={() => openBotLink(session.botLink)}
              hasBotLink={Boolean(session.botLink)}
              onNotifyNotPlayed={notifyNotPlayed}
              canNotifyNotPlayed={Boolean(session.linkedTelegramGroupId)}
              telegramSync={telegramSync}
              playerProfile={playerProfile}
              onOpenPlayerProfile={openPlayerProfile}
              onLoadTelegramSync={() => runBusy(() => loadTelegramSync(session.identity.playerTag))}
              onRelinkTelegramUser={relinkTelegramUser}
              busy={busy}
            />
            <Paper
              elevation={0}
              sx={{
                position: "fixed",
                left: 12,
                right: 12,
                bottom: 12,
                zIndex: 20,
                display: { xs: "block", md: "none" },
                borderRadius: 3,
                overflow: "hidden"
              }}
            >
              <BottomNavigation showLabels value={tab} onChange={(_, value) => setTab(value)}>
                {MOBILE_TABS.map((label, index) => (
                  <BottomNavigationAction key={label} label={label} value={index} />
                ))}
              </BottomNavigation>
            </Paper>
          </>
        )}

        {session && currentView === "player" && (
          <PlayerProfilePage
            profile={playerProfile}
            profileWindowWeeks={profileWindowWeeks}
            onWindowChange={(value) => {
              setProfileWindowWeeks(value);
              return runBusy(() =>
                loadPlayerProfile(playerProfile?.playerTag ?? session.identity.playerTag, value)
              );
            }}
            onBack={() => setCurrentView("dashboard")}
            onRefresh={() =>
              runBusy(() =>
                loadPlayerProfile(playerProfile?.playerTag ?? session.identity.playerTag, profileWindowWeeks)
              )
            }
            busy={busy}
          />
        )}

        {busy && (
          <Box sx={{ position: "fixed", right: 18, bottom: 18 }}>
            <CircularProgress size={34} thickness={4.4} />
          </Box>
        )}
      </Container>
    </ThemeProvider>
  );
}
