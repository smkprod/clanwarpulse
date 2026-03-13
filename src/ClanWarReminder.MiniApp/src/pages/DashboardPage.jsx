import { useState } from "react";
import {
  Alert,
  Avatar,
  Box,
  Button,
  Chip,
  Divider,
  LinearProgress,
  MenuItem,
  Paper,
  Stack,
  Switch,
  TextField,
  Typography
} from "@mui/material";
import { AppShell } from "../components/AppShell";

export function DashboardPage({
  copy,
  identity,
  dashboard,
  clanDetails,
  telegramSync,
  playerProfile,
  authorizedTags,
  themeMode,
  language,
  activeTab,
  onTabChange,
  onRefresh,
  onNotifyNotPlayed,
  onOpenBot,
  hasBotLink,
  canNotifyNotPlayed,
  onOpenPlayerProfile,
  onRelinkTelegramUser,
  onLoadTelegramSync,
  onThemeChange,
  onLanguageChange,
  onUnlink,
  busy
}) {
  const currentMember = (dashboard.allMembers ?? []).find(
    (member) => normalizeTag(member.playerTag) === normalizeTag(identity.playerTag)
  );
  const averageDeckPoints = playerProfile?.totalTrackedWarBattles
    ? (playerProfile.recentWeeks ?? []).reduce((sum, week) => sum + (week.totalContribution ?? 0), 0) /
      playerProfile.totalTrackedWarBattles
    : currentMember?.averageContributionPerBattle ?? 0;
  const linkedCount = telegramSync?.members?.filter((member) => member.isLinked).length ?? 0;
  const tabs = [
    { value: "home", label: copy.tabs.home },
    { value: "war", label: copy.tabs.war },
    { value: "clan", label: copy.tabs.clan },
    { value: "settings", label: copy.tabs.settings }
  ];
  const actions =
    activeTab === "home"
      ? [
          { label: copy.home.actions.refresh, onClick: onRefresh, disabled: busy, variant: "contained" },
          {
            label: copy.home.actions.notify,
            onClick: onNotifyNotPlayed,
            disabled: busy || !canNotifyNotPlayed,
            variant: "outlined",
            color: "warning"
          },
          hasBotLink
            ? {
                label: copy.home.actions.openBot,
                onClick: onOpenBot,
                disabled: busy,
                variant: "outlined",
                color: "secondary"
              }
            : null
        ].filter(Boolean)
      : null;

  return (
    <AppShell
      appName={copy.appName}
      title={copy.sessionTitle(identity.playerName, identity.clanName)}
      subtitle={copy.sessionSubtitle(identity.playerTag, identity.clanTag)}
      activeTab={activeTab}
      onTabChange={onTabChange}
      tabs={tabs}
      actions={actions}
    >
      {activeTab === "home" ? (
        <HomeTab
          copy={copy}
          identity={identity}
          dashboard={dashboard}
          clanDetails={clanDetails}
          currentMember={currentMember}
          averageDeckPoints={averageDeckPoints}
          linkedCount={linkedCount}
          playerProfile={playerProfile}
        />
      ) : null}

      {activeTab === "war" ? (
        <WarTab
          copy={copy}
          dashboard={dashboard}
          authorizedTags={authorizedTags}
          onOpenPlayerProfile={onOpenPlayerProfile}
          telegramSync={telegramSync}
          onRelinkTelegramUser={onRelinkTelegramUser}
          onLoadTelegramSync={onLoadTelegramSync}
          busy={busy}
        />
      ) : null}

      {activeTab === "clan" ? (
        <ClanTab copy={copy} clanDetails={clanDetails} onOpenPlayerProfile={onOpenPlayerProfile} busy={busy} />
      ) : null}

      {activeTab === "settings" ? (
        <SettingsTab
          copy={copy}
          identity={identity}
          themeMode={themeMode}
          language={language}
          onThemeChange={onThemeChange}
          onLanguageChange={onLanguageChange}
          onUnlink={onUnlink}
          linkedTelegramGroupId={Boolean(telegramSync?.platformGroupId)}
          busy={busy}
        />
      ) : null}
    </AppShell>
  );
}

function HomeTab({ copy, identity, dashboard, clanDetails, currentMember, averageDeckPoints, linkedCount, playerProfile }) {
  const totalMembers = dashboard.allMembers?.length ?? clanDetails?.participantsCount ?? 0;
  const playedBattles = playerProfile?.currentWeekBattlesPlayed ?? currentMember?.battlesPlayed ?? 0;
  const totalBattles = playerProfile?.currentWeekMaxBattles ?? 16;
  const remainingBattles = playerProfile?.currentWeekBattlesRemaining ?? Math.max(0, totalBattles - playedBattles);
  const cards = [
    { label: copy.home.cards.clanName, value: identity.clanName, helper: copy.home.cardsHelp.clanName },
    { label: copy.home.cards.playerName, value: identity.playerName, helper: copy.home.cardsHelp.playerName },
    { label: copy.home.cards.clanPoints, value: fmt(clanDetails?.currentScore), helper: copy.home.cardsHelp.clanPoints },
    { label: copy.home.cards.medals, value: fmt(clanDetails?.fame), helper: copy.home.cardsHelp.medals },
    { label: copy.home.cards.boatPoints, value: fmt(clanDetails?.repairPoints), helper: copy.home.cardsHelp.boatPoints },
    { label: copy.home.cards.avgDeck, value: fmt(averageDeckPoints), helper: copy.home.cardsHelp.avgDeck }
  ];

  return (
    <Stack spacing={1.5}>
      <SectionHero label={copy.home.label} title={copy.home.title} subtitle={copy.home.subtitle}>
        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
          <Chip label={`${copy.home.warStatus}: ${dashboard.warStatusText}`} color={dashboard.isWarActive ? "success" : "warning"} />
          <Chip label={`${copy.home.currentWar}: ${dashboard.warKey}`} variant="outlined" />
          <Chip
            label={`${copy.home.memberStatus}: ${copy.home.memberStatusValue(
              playedBattles,
              remainingBattles,
              totalBattles
            )}`}
            variant="outlined"
          />
        </Stack>
      </SectionHero>

      <GridCards>
        {cards.map((card) => (
          <StatCard key={card.label} label={card.label} value={card.value} helper={card.helper} />
        ))}
      </GridCards>

      <GridCards>
        <StatCard label={copy.home.quickStats.active} value={dashboard.played?.length ?? 0} helper="Played at least one battle" />
        <StatCard label={copy.home.quickStats.inactive} value={dashboard.notPlayed?.length ?? 0} helper="Still at risk" />
        <StatCard label={copy.home.quickStats.members} value={totalMembers} helper="Current roster size" />
        <StatCard label={copy.home.quickStats.linked} value={linkedCount} helper="Telegram-linked members" />
      </GridCards>
    </Stack>
  );
}

function WarTab({ copy, dashboard, authorizedTags, onOpenPlayerProfile, telegramSync, onRelinkTelegramUser, onLoadTelegramSync, busy }) {
  const authorizedSet = new Set((authorizedTags ?? []).map(normalizeTag));
  const played = dashboard.played ?? [];
  const notPlayed = dashboard.notPlayed ?? [];
  const totalContribution = (dashboard.allMembers ?? []).reduce((sum, member) => sum + (member.totalContribution ?? 0), 0);
  const [drafts, setDrafts] = useState({});

  return (
    <Stack spacing={1.5}>
      <SectionHero label={copy.tabs.war} title={copy.war.title} subtitle={copy.war.subtitle}>
        {!dashboard.isWarActive ? <Alert severity="warning">{copy.war.warInactive}</Alert> : null}
        <GridCards>
          <StatCard label={copy.war.played} value={played.length} helper="Played at least one battle" />
          <StatCard label={copy.war.notPlayed} value={notPlayed.length} helper="No battle played yet" />
          <StatCard
            label={copy.war.remainingBattles}
            value={(dashboard.allMembers ?? []).reduce((sum, member) => sum + (member.battlesRemaining ?? 0), 0)}
            helper="Sum across the clan"
          />
          <StatCard label={copy.war.totalContribution} value={totalContribution} helper="Current clan war score" />
        </GridCards>
      </SectionHero>

      <Stack direction={{ xs: "column", lg: "row" }} spacing={1.5}>
        <MemberListCard title={copy.war.played} emptyText={copy.war.nobodyPlayed} members={played} authorizedSet={authorizedSet} onOpenPlayerProfile={onOpenPlayerProfile} busy={busy} copy={copy} />
        <MemberListCard title={copy.war.notPlayed} emptyText={copy.war.allDone} members={notPlayed} authorizedSet={authorizedSet} onOpenPlayerProfile={onOpenPlayerProfile} busy={busy} copy={copy} />
      </Stack>

      <Paper elevation={0} sx={surfaceSx}>
        <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1.2}>
          <Stack spacing={0.35}>
            <Typography variant="h6">{copy.war.telegram.title}</Typography>
            <Typography variant="body2" color="text.secondary">
              {telegramSync?.platformGroupId ? `${copy.war.telegram.linked}: ${telegramSync.members.length}` : copy.war.telegram.noGroup}
            </Typography>
          </Stack>
          <Button variant="outlined" onClick={onLoadTelegramSync} disabled={busy}>
            {copy.war.telegram.refresh}
          </Button>
        </Stack>

        <Stack spacing={1} sx={{ mt: 1.5 }}>
          {(telegramSync?.linkedUsers ?? []).length ? (
            telegramSync.linkedUsers.map((user) => (
              <Paper key={user.platformUserId} elevation={0} sx={innerCardSx}>
                <Stack spacing={0.9}>
                  <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.5}>
                    <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>
                      {user.displayName} · {user.platformUserId}
                    </Typography>
                    <Chip size="small" color={user.inCurrentClan ? "success" : "warning"} label={user.inCurrentClan ? "In current clan" : "Outside current clan"} />
                  </Stack>
                  <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
                    <TextField
                      fullWidth
                      size="small"
                      label={copy.war.telegram.relinkLabel}
                      value={drafts[user.platformUserId] ?? user.playerTag ?? ""}
                      onChange={(event) =>
                        setDrafts((current) => ({
                          ...current,
                          [user.platformUserId]: event.target.value
                        }))
                      }
                    />
                    <Button
                      variant="contained"
                      onClick={() =>
                        onRelinkTelegramUser(
                          user.platformUserId,
                          user.displayName,
                          drafts[user.platformUserId] ?? user.playerTag ?? ""
                        )
                      }
                      disabled={busy || !(drafts[user.platformUserId] ?? user.playerTag ?? "").trim()}
                    >
                      {copy.war.telegram.relink}
                    </Button>
                  </Stack>
                </Stack>
              </Paper>
            ))
          ) : (
            <Typography color="text.secondary" sx={{ mt: 1.2 }}>
              {copy.war.telegram.noUsers}
            </Typography>
          )}
        </Stack>
      </Paper>
    </Stack>
  );
}

function ClanTab({ copy, clanDetails, onOpenPlayerProfile, busy }) {
  if (!clanDetails) {
    return (
      <Paper elevation={0} sx={surfaceSx}>
        <Typography color="text.secondary">{copy.clan.noData}</Typography>
      </Paper>
    );
  }

  return (
    <Stack spacing={1.5}>
      <SectionHero label={copy.tabs.clan} title={copy.clan.title} subtitle={copy.clan.subtitle}>
        <GridCards>
          <StatCard label="Clan points" value={clanDetails.currentScore} helper="Current river race score" />
          <StatCard label={copy.clan.region} value={clanDetails.region} helper="Configured clan location" />
          <StatCard label={copy.clan.trophies} value={clanDetails.clanTrophies} helper={`Required ${clanDetails.requiredTrophies}`} />
          <StatCard label={copy.clan.members} value={clanDetails.participantsCount} helper={copy.clan.memberOpenHint} />
        </GridCards>
      </SectionHero>

      <Stack direction={{ xs: "column", lg: "row" }} spacing={1.5}>
        <Paper elevation={0} sx={{ ...surfaceSx, flex: 1.2 }}>
          <Typography variant="h6" sx={{ mb: 1.1 }}>
            {copy.clan.members}
          </Typography>
          <Stack spacing={0.8}>
            {(clanDetails.members ?? []).map((member) => (
              <Button
                key={member.playerTag}
                variant="text"
                disabled={busy}
                onClick={() => onOpenPlayerProfile(member.playerTag)}
                sx={{
                  justifyContent: "space-between",
                  p: 1.2,
                  border: (theme) => `1px solid ${theme.palette.divider}`,
                  borderRadius: 3,
                  textTransform: "none"
                }}
              >
                <Stack direction="row" spacing={1.1} alignItems="center" sx={{ minWidth: 0 }}>
                  <Avatar sx={{ bgcolor: "primary.main", color: "#fff" }}>{member.playerName.slice(0, 1).toUpperCase()}</Avatar>
                  <Box sx={{ textAlign: "left", minWidth: 0 }}>
                    <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{member.playerName}</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>
                      {member.playerTag} · {member.role}
                    </Typography>
                  </Box>
                </Stack>
                <Typography variant="body2" color="text.secondary">
                  {member.trophies}
                </Typography>
              </Button>
            ))}
          </Stack>
        </Paper>

        <Stack spacing={1.5} sx={{ flex: 0.95 }}>
          <Paper elevation={0} sx={surfaceSx}>
            <Typography variant="h6" sx={{ mb: 1.1 }}>
              {copy.clan.topContributors}
            </Typography>
            <Stack spacing={0.9}>
              {(clanDetails.topContributors ?? []).slice(0, 8).map((member) => (
                <Paper key={member.playerTag} elevation={0} sx={innerCardSx}>
                  <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.5}>
                    <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{member.playerName}</Typography>
                    <Typography variant="body2" color="text.secondary">
                      {member.totalContribution} pts
                    </Typography>
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    Fame {member.fame} · Boat {member.repairPoints} · Battles {member.battlesPlayed}
                  </Typography>
                </Paper>
              ))}
            </Stack>
          </Paper>

          <Paper elevation={0} sx={surfaceSx}>
            <Typography variant="h6" sx={{ mb: 1.1 }}>
              {copy.clan.recentWars}
            </Typography>
            <Stack spacing={0.9}>
              {(clanDetails.recentWars ?? []).map((war) => (
                <Paper key={war.warKey} elevation={0} sx={innerCardSx}>
                  <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.5}>
                    <Typography sx={{ fontWeight: 700 }}>{war.warKey}</Typography>
                    <Typography variant="body2" color="text.secondary">
                      #{war.rank} · {war.score}
                    </Typography>
                  </Stack>
                </Paper>
              ))}
            </Stack>
          </Paper>
        </Stack>
      </Stack>
    </Stack>
  );
}

function SettingsTab({ copy, identity, themeMode, language, onThemeChange, onLanguageChange, onUnlink, linkedTelegramGroupId, busy }) {
  return (
    <Stack spacing={1.5}>
      <SectionHero label={copy.tabs.settings} title={copy.settings.title} subtitle={copy.settings.subtitle} />

      <Stack direction={{ xs: "column", lg: "row" }} spacing={1.5}>
        <Paper elevation={0} sx={{ ...surfaceSx, flex: 1 }}>
          <Typography variant="h6" sx={{ mb: 1.3 }}>
            {copy.settings.appearance}
          </Typography>
          <Stack spacing={1.4}>
            <Stack direction="row" justifyContent="space-between" alignItems="center">
              <Box>
                <Typography sx={{ fontWeight: 700 }}>{copy.settings.theme}</Typography>
                <Typography variant="body2" color="text.secondary">
                  {themeMode === "dark" ? copy.settings.dark : copy.settings.light}
                </Typography>
              </Box>
              <Switch checked={themeMode === "dark"} onChange={(event) => onThemeChange(event.target.checked ? "dark" : "light")} />
            </Stack>

            <TextField select label={copy.settings.language} value={language} onChange={(event) => onLanguageChange(event.target.value)}>
              <MenuItem value="ru">Русский</MenuItem>
              <MenuItem value="en">English</MenuItem>
            </TextField>
          </Stack>
        </Paper>

        <Paper elevation={0} sx={{ ...surfaceSx, flex: 1 }}>
          <Typography variant="h6" sx={{ mb: 1.3 }}>
            {copy.settings.account}
          </Typography>
          <Stack spacing={1.1}>
            <InfoRow label={copy.settings.linkedPlayer} value={identity.playerName} />
            <InfoRow label={copy.settings.linkedTag} value={identity.playerTag} />
            <InfoRow label={copy.settings.telegramLink} value={linkedTelegramGroupId ? copy.settings.yes : copy.settings.no} />
            <Divider />
            <Typography variant="body2" color="text.secondary">
              {copy.settings.syncText}
            </Typography>
            <Button variant="contained" color="warning" onClick={onUnlink} disabled={busy}>
              {copy.settings.unlink}
            </Button>
          </Stack>
        </Paper>
      </Stack>
    </Stack>
  );
}

function SectionHero({ label, title, subtitle, children }) {
  return (
    <Paper elevation={0} sx={surfaceSx}>
      <Stack spacing={1.2}>
        <Stack spacing={0.45}>
          {label ? (
            <Typography variant="body2" color="text.secondary">
              {label}
            </Typography>
          ) : null}
          <Typography variant="h5">{title}</Typography>
          {subtitle ? (
            <Typography variant="body2" color="text.secondary">
              {subtitle}
            </Typography>
          ) : null}
        </Stack>
        {children}
      </Stack>
    </Paper>
  );
}

function GridCards({ children }) {
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "1fr", sm: "repeat(2, minmax(0, 1fr))" },
        gap: 1.1
      }}
    >
      {children}
    </Box>
  );
}

function StatCard({ label, value, helper }) {
  return (
    <Paper elevation={0} sx={innerCardSx}>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="h5" sx={{ mt: 0.35, overflowWrap: "anywhere" }}>
        {value}
      </Typography>
      <Typography variant="caption" color="text.secondary">
        {helper}
      </Typography>
    </Paper>
  );
}

function MemberListCard({ title, emptyText, members, authorizedSet, onOpenPlayerProfile, busy, copy }) {
  return (
    <Paper elevation={0} sx={{ ...surfaceSx, flex: 1 }}>
      <Typography variant="h6" sx={{ mb: 1.1 }}>
        {title}
      </Typography>
      {members?.length ? (
        <Stack spacing={0.9}>
          {members.map((member) => (
            <Paper key={`${title}-${member.playerTag}`} elevation={0} sx={innerCardSx}>
              <Stack spacing={0.6}>
                <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.5}>
                  <Button
                    variant="text"
                    disabled={busy}
                    onClick={() => onOpenPlayerProfile(member.playerTag)}
                    sx={{ justifyContent: "flex-start", p: 0, textTransform: "none" }}
                  >
                    <Typography sx={{ fontWeight: 700, textAlign: "left", overflowWrap: "anywhere" }}>
                      {member.playerName}
                    </Typography>
                  </Button>
                  <Stack direction="row" spacing={0.7} useFlexGap flexWrap="wrap">
                    {authorizedSet.has(normalizeTag(member.playerTag)) ? <Chip size="small" label="Linked" color="success" /> : null}
                    <Chip size="small" label={`${member.battlesRemaining ?? 0} left`} variant="outlined" />
                  </Stack>
                </Stack>
                <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>
                  {member.playerTag} · {member.battlesPlayed ?? 0}/4 · {copy.war.avgPerBattle}: {fmt(member.averageContributionPerBattle)}
                </Typography>
                <LinearProgress value={Math.min(100, ((member.battlesPlayed ?? 0) / 4) * 100)} variant="determinate" sx={progressSx} />
              </Stack>
            </Paper>
          ))}
        </Stack>
      ) : (
        <Typography color="text.secondary">{emptyText}</Typography>
      )}
    </Paper>
  );
}

function InfoRow({ label, value }) {
  return (
    <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.5}>
      <Typography color="text.secondary">{label}</Typography>
      <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{value}</Typography>
    </Stack>
  );
}

function normalizeTag(tag) {
  const value = String(tag ?? "").trim().toUpperCase();
  return value.startsWith("#") ? value : `#${value}`;
}

function fmt(value) {
  const number = Number(value ?? 0);
  return Number.isInteger(number) ? String(number) : number.toFixed(1);
}

const surfaceSx = { p: { xs: 1.5, md: 1.8 }, border: (theme) => `1px solid ${theme.palette.divider}` };
const innerCardSx = {
  p: 1.2,
  border: (theme) => `1px solid ${theme.palette.divider}`,
  bgcolor: (theme) => theme.palette.background.paper
};
const progressSx = { mt: 0.3, height: 8, borderRadius: 999 };
