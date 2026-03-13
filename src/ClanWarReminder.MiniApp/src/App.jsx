import { useEffect, useMemo, useState } from "react";
import { Alert, Box, CircularProgress, CssBaseline, Stack, ThemeProvider, Typography } from "@mui/material";
import { apiGet, apiPost } from "./api/client";
import { DashboardPage } from "./pages/DashboardPage";
import { PlayerProfilePage } from "./pages/PlayerProfilePage";
import { SignInPage } from "./pages/SignInPage";
import { getCopy } from "./i18n";
import { getTelegramChatId, getTelegramInitData, initTelegramWebApp, notify, openBotLink } from "./lib/telegram";
import { buildAppTheme } from "./theme";

initTelegramWebApp();

const STORAGE_KEYS = {
  playerTag: "clanwarreminder.playerTag",
  themeMode: "clanwarreminder.themeMode",
  language: "clanwarreminder.language"
};

export default function App() {
  const [playerTag, setPlayerTag] = useState("");
  const [themeMode, setThemeMode] = useState(loadStoredValue(STORAGE_KEYS.themeMode, "dark"));
  const [language, setLanguage] = useState(loadStoredValue(STORAGE_KEYS.language, "ru"));
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState(null);
  const [session, setSession] = useState(null);
  const [currentView, setCurrentView] = useState("dashboard");
  const [activeTab, setActiveTab] = useState("home");
  const [clanDetails, setClanDetails] = useState(null);
  const [telegramSync, setTelegramSync] = useState(null);
  const [playerProfile, setPlayerProfile] = useState(null);
  const [profileWindowWeeks, setProfileWindowWeeks] = useState(5);

  const theme = useMemo(() => buildAppTheme(themeMode), [themeMode]);
  const copy = getCopy(language);

  useEffect(() => {
    const savedTag = window.localStorage.getItem(STORAGE_KEYS.playerTag);
    if (savedTag) {
      setPlayerTag(savedTag);
    }
  }, []);

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEYS.themeMode, themeMode);
  }, [themeMode]);

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEYS.language, language);
  }, [language]);

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

        await applySession(data.session, copy.restoreSuccess(data.session.identity.playerName));
      } catch (error) {
        if (!cancelled) {
          setFeedback({ text: error.message || copy.unexpectedError, severity: "warning" });
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
  }, [session, copy]);

  async function loginByTag() {
    await runBusy(async () => {
      const data = await apiPost("/miniapp/auth/player", {
        playerTag,
        telegramInitData: getTelegramInitData(),
        telegramChatId: getTelegramChatId()
      });

      await applySession(data, copy.loginSuccess(data.identity.playerName, data.identity.playerTag));
      notify("success");
    });
  }

  async function applySession(nextSession, successMessage) {
    setSession(nextSession);
    setPlayerTag(nextSession.identity.playerTag);
    setActiveTab("home");
    setCurrentView("dashboard");
    window.localStorage.setItem(STORAGE_KEYS.playerTag, nextSession.identity.playerTag);

    const [nextClanDetails, nextTelegramSync, nextPlayerProfile] = await Promise.all([
      apiGet(`/miniapp/clan/details?clanTag=${encodeURIComponent(nextSession.identity.clanTag)}`),
      apiGet(`/miniapp/telegram/sync?playerTag=${encodeURIComponent(nextSession.identity.playerTag)}`),
      apiGet(
        `/miniapp/player/profile?playerTag=${encodeURIComponent(nextSession.identity.playerTag)}&windowWeeks=${encodeURIComponent(
          profileWindowWeeks
        )}`
      )
    ]);

    setClanDetails(nextClanDetails);
    setTelegramSync(nextTelegramSync);
    setPlayerProfile(nextPlayerProfile);
    setFeedback({ text: successMessage, severity: "success" });
  }

  async function refreshDashboard() {
    if (!session?.identity?.playerTag) {
      return;
    }

    await runBusy(async () => {
      const data = await apiGet(`/miniapp/player/dashboard?playerTag=${encodeURIComponent(session.identity.playerTag)}`);
      setSession(data);
      const [nextClanDetails, nextTelegramSync, nextPlayerProfile] = await Promise.all([
        apiGet(`/miniapp/clan/details?clanTag=${encodeURIComponent(data.identity.clanTag)}`),
        apiGet(`/miniapp/telegram/sync?playerTag=${encodeURIComponent(data.identity.playerTag)}`),
        apiGet(
          `/miniapp/player/profile?playerTag=${encodeURIComponent(data.identity.playerTag)}&windowWeeks=${encodeURIComponent(
            profileWindowWeeks
          )}`
        )
      ]);
      setClanDetails(nextClanDetails);
      setTelegramSync(nextTelegramSync);
      setPlayerProfile(nextPlayerProfile);
      setFeedback({ text: copy.dashboardUpdated, severity: "success" });
      notify("success");
    });
  }

  async function notifyNotPlayed() {
    if (!session?.linkedTelegramGroupId) {
      setFeedback({ text: copy.noTelegramGroup, severity: "warning" });
      return;
    }

    await runBusy(async () => {
      const result = await apiPost("/commands/notify/not-played", {
        platform: "Telegram",
        platformGroupId: session.linkedTelegramGroupId
      });

      if (!result.sent) {
        setFeedback({ text: result.text ?? copy.notifySkipped, severity: "info" });
        return;
      }

      setFeedback({ text: copy.notifyDone(result.linkedTargets, result.unlinkedTargets), severity: "success" });
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
      setFeedback({ text: copy.telegramGroupMissing, severity: "warning" });
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
      setFeedback({ text: copy.telegramSyncUpdated, severity: "success" });
      notify("success");
    });
  }

  async function unlinkPlayerTag() {
    await runBusy(async () => {
      const telegramInitData = getTelegramInitData();

      if (telegramInitData) {
        await apiPost("/miniapp/player/unlink", { telegramInitData });
        setFeedback({ text: copy.unlinkSuccess, severity: "success" });
      } else {
        setFeedback({ text: copy.unlinkLocalOnly, severity: "info" });
      }

      clearSessionState();
    });
  }

  function clearSessionState() {
    setSession(null);
    setClanDetails(null);
    setTelegramSync(null);
    setPlayerProfile(null);
    setPlayerTag("");
    setCurrentView("dashboard");
    setActiveTab("home");
    window.localStorage.removeItem(STORAGE_KEYS.playerTag);
  }

  async function runBusy(action) {
    try {
      setBusy(true);
      await action();
    } catch (error) {
      setFeedback({ text: error.message || copy.unexpectedError, severity: "error" });
      notify("error");
    } finally {
      setBusy(false);
    }
  }

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box sx={{ minHeight: "100vh", px: { xs: 0.8, md: 0 } }}>
        <Stack spacing={1.2} sx={{ maxWidth: 1320, mx: "auto", pt: 2 }}>
          {feedback?.text ? (
            <Alert severity={feedback.severity} sx={{ mx: { xs: 1.2, md: 3 } }}>
              {feedback.text}
            </Alert>
          ) : null}

          {!session ? (
            <Box sx={{ px: { xs: 1.2, md: 3 }, pb: 4 }}>
              <Stack spacing={2} sx={{ mb: 2 }}>
                <Typography variant="h3">{copy.appName}</Typography>
                <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 760 }}>
                  {copy.signInText}
                </Typography>
              </Stack>
              <SignInPage copy={copy} playerTag={playerTag} onPlayerTagChange={setPlayerTag} onSignIn={loginByTag} busy={busy} />
            </Box>
          ) : null}

          {session && currentView === "dashboard" ? (
            <DashboardPage
              copy={copy}
              identity={session.identity}
              dashboard={session.dashboard}
              clanDetails={clanDetails}
              telegramSync={telegramSync}
              playerProfile={playerProfile}
              authorizedTags={session.authorizedPlayerTags ?? []}
              themeMode={themeMode}
              language={language}
              activeTab={activeTab}
              onTabChange={setActiveTab}
              onRefresh={refreshDashboard}
              onNotifyNotPlayed={notifyNotPlayed}
              onOpenBot={() => openBotLink(session.botLink)}
              hasBotLink={Boolean(session.botLink)}
              canNotifyNotPlayed={Boolean(session.linkedTelegramGroupId)}
              onOpenPlayerProfile={openPlayerProfile}
              onRelinkTelegramUser={relinkTelegramUser}
              onLoadTelegramSync={() => runBusy(() => loadTelegramSync(session.identity.playerTag))}
              onThemeChange={setThemeMode}
              onLanguageChange={setLanguage}
              onUnlink={unlinkPlayerTag}
              busy={busy}
            />
          ) : null}

          {session && currentView === "player" ? (
            <Box sx={{ px: { xs: 1.2, md: 3 }, pb: 3 }}>
              <PlayerProfilePage
                copy={copy}
                language={language}
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
            </Box>
          ) : null}
        </Stack>

        {busy ? (
          <Box sx={{ position: "fixed", right: 18, bottom: 18, zIndex: 30 }}>
            <CircularProgress size={34} thickness={4.5} />
          </Box>
        ) : null}
      </Box>
    </ThemeProvider>
  );
}

function loadStoredValue(key, fallbackValue) {
  if (typeof window === "undefined") {
    return fallbackValue;
  }

  return window.localStorage.getItem(key) ?? fallbackValue;
}
