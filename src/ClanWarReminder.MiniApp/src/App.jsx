import { useMemo, useState } from "react";
import {
  Alert,
  Box,
  CircularProgress,
  Container,
  CssBaseline,
  Stack,
  ThemeProvider,
  Typography
} from "@mui/material";
import { apiGet, apiPost } from "./api/client";
import { SignInPage } from "./pages/SignInPage";
import { DashboardPage } from "./pages/DashboardPage";
import { getTelegramChatId, getTelegramInitData, initTelegramWebApp, notify, openBotLink } from "./lib/telegram";
import { appTheme } from "./theme";

initTelegramWebApp();

export default function App() {
  const [playerTag, setPlayerTag] = useState("");
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState({ text: "Enter player tag to load clan war dashboard.", severity: "info" });
  const [session, setSession] = useState(null);
  const [tab, setTab] = useState(0);
  const [selectedClanTag, setSelectedClanTag] = useState("");
  const [clanDetails, setClanDetails] = useState(null);
  const [telegramSync, setTelegramSync] = useState(null);

  const titleLine = useMemo(() => {
    if (!session) {
      return "Tag based sign in";
    }

    return `${session.identity.playerName} | ${session.identity.clanName}`;
  }, [session]);

  async function loginByTag() {
    await runBusy(async () => {
      const data = await apiPost("/miniapp/auth/player", {
        playerTag,
        telegramInitData: getTelegramInitData(),
        telegramChatId: getTelegramChatId()
      });
      setSession(data);
      setTab(0);
      setClanDetails(null);
      setSelectedClanTag("");
      await loadTelegramSync(data.identity.playerTag);
      setFeedback({ text: `Loaded ${data.identity.playerName} (${data.identity.playerTag})`, severity: "success" });
      notify("success");
    });
  }

  async function refreshDashboard() {
    if (!session?.identity?.playerTag) {
      return;
    }

    await runBusy(async () => {
      const data = await apiGet(`/miniapp/player/dashboard?playerTag=${encodeURIComponent(session.identity.playerTag)}`);
      setSession(data);
      setClanDetails(null);
      setSelectedClanTag("");
      await loadTelegramSync(data.identity.playerTag);
      setFeedback({ text: "Clan war data refreshed.", severity: "success" });
      notify("success");
    });
  }

  async function loadClanDetails(clanTag) {
    await runBusy(async () => {
      const data = await apiGet(`/miniapp/clan/details?clanTag=${encodeURIComponent(clanTag)}`);
      setSelectedClanTag(clanTag);
      setClanDetails(data);
      setFeedback({ text: `Loaded clan stats for ${data.clanName}.`, severity: "success" });
      notify("success");
    });
  }

  async function notifyNotPlayed() {
    if (!session?.linkedTelegramGroupId) {
      setFeedback({ text: "Telegram group is not linked for this clan. Run /commands/setup/telegram first.", severity: "warning" });
      return;
    }

    await runBusy(async () => {
      const result = await apiPost("/commands/notify/not-played", {
        platform: "Telegram",
        platformGroupId: session.linkedTelegramGroupId
      });

      if (!result.sent) {
        setFeedback({ text: result.text ?? "No notifications sent.", severity: "info" });
        return;
      }

      setFeedback({
        text: `Tagged not played: linked ${result.linkedTargets}, unlinked ${result.unlinkedTargets}.`,
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

  async function relinkTelegramUser(platformUserId, displayName, targetPlayerTag) {
    if (!session?.linkedTelegramGroupId) {
      setFeedback({ text: "Telegram group is not linked for this clan.", severity: "warning" });
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
      setFeedback({ text: "Telegram link updated.", severity: "success" });
      notify("success");
    });
  }

  async function runBusy(action) {
    try {
      setBusy(true);
      await action();
    } catch (error) {
      setFeedback({ text: error.message || "Unexpected error", severity: "error" });
      notify("error");
    } finally {
      setBusy(false);
    }
  }

  return (
    <ThemeProvider theme={appTheme}>
      <CssBaseline />
      <Container maxWidth="md" sx={{ py: 2.5, pb: 4, overflowX: "hidden" }}>
        <Stack spacing={1.5} sx={{ mb: 2.5 }}>
          <Typography variant="overline" sx={{ color: "#8dd6ff", letterSpacing: "0.13em" }}>
            Telegram Mini App
          </Typography>
          <Typography variant="h4">Clan War Pulse</Typography>
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

        {session && (
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
            onLoadTelegramSync={() => runBusy(() => loadTelegramSync(session.identity.playerTag))}
            onRelinkTelegramUser={relinkTelegramUser}
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
