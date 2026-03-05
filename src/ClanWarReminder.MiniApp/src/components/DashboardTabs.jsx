import { useMemo, useState } from "react";
import {
  Box,
  Button,
  Chip,
  Divider,
  LinearProgress,
  Paper,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography
} from "@mui/material";

export function DashboardTabs({
  tab,
  onTabChange,
  dashboard,
  authorizedTags,
  selectedClanTag,
  clanDetails,
  onRefresh,
  onLoadClanDetails,
  onOpenBot,
  hasBotLink,
  onNotifyNotPlayed,
  canNotifyNotPlayed,
  telegramSync,
  onLoadTelegramSync,
  onRelinkTelegramUser,
  busy,
  identity
}) {
  return (
    <Paper elevation={0} sx={{ p: 1.6, border: "1px solid rgba(132,186,217,0.2)", backdropFilter: "blur(6px)", overflow: "hidden" }}>
      <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1.2} alignItems={{ sm: "center" }}>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ minWidth: 0 }}>
          <InfoChip text={`Nick ${identity.playerName}`} />
          <InfoChip text={`Clan ${identity.clanName}`} color="primary" />
          <InfoChip text={`War ${dashboard.warKey}`} color="secondary" />
        </Stack>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" onClick={onRefresh} disabled={busy}>Refresh</Button>
          <Button variant="outlined" color="warning" onClick={onNotifyNotPlayed} disabled={busy || !canNotifyNotPlayed}>
            Tag Not Played
          </Button>
          {hasBotLink && <Button variant="contained" color="secondary" onClick={onOpenBot}>Open Bot</Button>}
        </Stack>
      </Stack>

      <Tabs value={tab} onChange={(_, value) => onTabChange(value)} sx={{ mt: 1.5, mb: 1.5 }} variant="scrollable" scrollButtons="auto">
        <Tab label="Activity" />
        <Tab label="Opponents" />
        <Tab label="Forecast" />
        <Tab label="History" />
        <Tab label="Clans" />
        <Tab label="Telegram Sync" />
      </Tabs>
      <Divider sx={{ mb: 1.5, borderColor: "rgba(132,186,217,0.18)" }} />

      {tab === 0 && <ActivityTab dashboard={dashboard} authorizedTags={authorizedTags} />}
      {tab === 1 && <OpponentsTab dashboard={dashboard} identity={identity} />}
      {tab === 2 && <ForecastTab dashboard={dashboard} />}
      {tab === 3 && <HistoryTab dashboard={dashboard} />}
      {tab === 4 && (
        <ClansTab
          dashboard={dashboard}
          selectedClanTag={selectedClanTag}
          clanDetails={clanDetails}
          onLoadClanDetails={onLoadClanDetails}
        />
      )}
      {tab === 5 && (
        <TelegramSyncTab
          sync={telegramSync}
          onReload={onLoadTelegramSync}
          onRelink={onRelinkTelegramUser}
          busy={busy}
        />
      )}
    </Paper>
  );
}

function InfoChip({ text, color }) {
  return (
    <Chip
      label={text}
      color={color}
      size="small"
      sx={{ maxWidth: "100%", "& .MuiChip-label": { overflow: "hidden", textOverflow: "ellipsis" } }}
    />
  );
}

function ActivityTab({ dashboard, authorizedTags }) {
  const authorizedSet = new Set((authorizedTags ?? []).map((x) => normalizeTag(x)));
  const members = [...(dashboard.played ?? []), ...(dashboard.notPlayed ?? [])];
  const totalRemaining = members.reduce((sum, m) => sum + (m.battlesRemaining ?? 0), 0);

  return (
    <Stack spacing={1.2}>
      <Typography variant="body2" sx={{ color: "#9ec2da" }}>
        Battles remaining in your clan: {totalRemaining}
      </Typography>
      <Stack direction={{ xs: "column", md: "row" }} spacing={1.5}>
        <ScrollList title="Played clan war" members={dashboard.played} emptyText="Nobody played yet." authorizedSet={authorizedSet} />
        <ScrollList title="Not played clan war" members={dashboard.notPlayed} emptyText="Everyone has played." authorizedSet={authorizedSet} />
      </Stack>
    </Stack>
  );
}

function OpponentsTab({ dashboard, identity }) {
  const raceClans = [...(dashboard.currentRaceClans ?? [])].sort((a, b) => b.totalScore - a.totalScore);
  const opponents = dashboard.opponents?.length
    ? dashboard.opponents
    : raceClans.filter((x) => normalizeTag(x.clanTag) !== normalizeTag(dashboard.clanTag));
  const maxScore = raceClans.length ? Math.max(...raceClans.map((x) => x.totalScore), 1) : 1;

  if (!raceClans.length) {
    return <Typography color="text.secondary">Opponents data is not available yet.</Typography>;
  }

  return (
    <Stack spacing={1.2}>
      <Paper variant="outlined" sx={{ p: 1.2, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" }}>
        <Typography sx={{ fontWeight: 700, mb: 0.8 }}>Current River Race Situation</Typography>
        <Stack spacing={0.8}>
          {raceClans.map((clan, index) => {
            const isOwnClan = normalizeTag(clan.clanTag) === normalizeTag(dashboard.clanTag);
            return (
              <Box key={`race-${clan.clanTag}`}>
                <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
                  <Typography variant="body2" sx={{ fontWeight: isOwnClan ? 800 : 600, overflowWrap: "anywhere" }}>
                    {index + 1}. {clan.clanName}{isOwnClan ? ` (your clan: ${identity.clanName})` : ""}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    now {clan.periodPoints ?? 0} | total {clan.totalScore}
                  </Typography>
                </Stack>
                <LinearProgress
                  variant="determinate"
                  value={Math.min(100, (clan.totalScore / maxScore) * 100)}
                  sx={{ mt: 0.4, height: 8, borderRadius: 999, bgcolor: "rgba(255,255,255,0.08)" }}
                />
              </Box>
            );
          })}
        </Stack>
      </Paper>

      <Typography variant="body2" sx={{ color: "#9ec2da" }}>
        Opponents in current race
      </Typography>
      {opponents.map((opponent) => (
        <Paper key={opponent.clanTag} variant="outlined" sx={{ p: 1.2, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" }}>
          <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1}>
            <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{opponent.clanName}</Typography>
            <Typography color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{opponent.clanTag}</Typography>
          </Stack>
          <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: "wrap" }} useFlexGap>
            <Chip size="small" label={`Score ${opponent.totalScore}`} />
            <Chip size="small" color="secondary" label={`Now ${opponent.periodPoints ?? 0}`} />
            <Chip size="small" label={`Fame ${opponent.fame}`} />
            <Chip size="small" label={`Repair ${opponent.repairPoints}`} />
            <Chip size="small" label={`Players ${opponent.participantsCount}`} />
          </Stack>
        </Paper>
      ))}
    </Stack>
  );
}

function ForecastTab({ dashboard }) {
  const raceClans = dashboard.currentRaceClans ?? [];
  const forecastMap = new Map((dashboard.forecast?.ranking ?? []).map((x) => [normalizeTag(x.clanTag), x]));

  const ranking = raceClans.map((clan) => {
    const forecast = forecastMap.get(normalizeTag(clan.clanTag));
    if (forecast) {
      return forecast;
    }

    return {
      clanTag: clan.clanTag,
      clanName: clan.clanName,
      predictedScore: clan.totalScore,
      recentAverageScore: clan.totalScore,
      recentAverageRank: 5,
      sampleSize: 0
    };
  }).sort((a, b) => b.predictedScore - a.predictedScore);

  const maxForecast = ranking.length ? Math.max(...ranking.map((item) => item.predictedScore), 1) : 1;

  if (!ranking.length) {
    return <Typography color="text.secondary">Not enough data for forecast yet.</Typography>;
  }

  return (
    <Stack spacing={1}>
      <Typography variant="body2" sx={{ color: "#9ec2da" }}>
        Model {(dashboard.forecast?.basis ?? "Current race score")} | only clans from current race
      </Typography>
      {ranking.map((item, index) => (
        <Paper key={item.clanTag} variant="outlined" sx={{ p: 1.2, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" }}>
          <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1}>
            <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{index + 1}. {item.clanName}</Typography>
            <Typography color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{item.clanTag}</Typography>
          </Stack>
          <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: "wrap" }} useFlexGap>
            <Chip size="small" color="primary" label={`Forecast ${item.predictedScore}`} />
            <Chip size="small" label={`Avg score ${item.recentAverageScore}`} />
            <Chip size="small" label={`Avg rank ${item.recentAverageRank}`} />
            <Chip size="small" label={`Matches ${item.sampleSize}`} />
          </Stack>
          <Box sx={{ mt: 1.2 }}>
            <LinearProgress
              variant="determinate"
              value={Math.min(100, (item.predictedScore / maxForecast) * 100)}
              sx={{ height: 9, borderRadius: 999, bgcolor: "rgba(255,255,255,0.08)" }}
            />
          </Box>
        </Paper>
      ))}
    </Stack>
  );
}

function HistoryTab({ dashboard }) {
  const history = dashboard.history ?? [];

  if (!history.length) {
    return <Typography color="text.secondary">History is not available yet.</Typography>;
  }

  return (
    <Stack spacing={1.2}>
      {history.map((race) => {
        const maxScore = race.results.length ? Math.max(...race.results.map((r) => r.score), 1) : 1;
        return (
          <Paper key={race.warKey} variant="outlined" sx={{ p: 1.2, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" }}>
            <Typography sx={{ fontWeight: 700, mb: 1, overflowWrap: "anywhere" }}>{race.warKey}</Typography>
            <Stack spacing={0.9}>
              {race.results.map((row) => (
                <Box key={`${race.warKey}-${row.clanTag}`}>
                  <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
                    <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>{row.rank}. {row.clanName}</Typography>
                    <Typography variant="body2" color="text.secondary">{row.score}</Typography>
                  </Stack>
                  <LinearProgress
                    variant="determinate"
                    value={Math.min(100, (row.score / maxScore) * 100)}
                    sx={{ mt: 0.4, height: 8, borderRadius: 999, bgcolor: "rgba(255,255,255,0.08)" }}
                  />
                </Box>
              ))}
            </Stack>
          </Paper>
        );
      })}
    </Stack>
  );
}

function ClansTab({ dashboard, selectedClanTag, clanDetails, onLoadClanDetails }) {
  const clans = dashboard.currentRaceClans ?? [];

  if (!clans.length) {
    return <Typography color="text.secondary">Current race clans are not available.</Typography>;
  }

  return (
    <Stack spacing={1.2}>
      <Typography variant="body2" sx={{ color: "#9ec2da" }}>
        Pick a clan to see detailed stats: top contributors and past wars.
      </Typography>

      <Stack spacing={0.8}>
        {clans.map((clan) => (
          <Paper key={clan.clanTag} variant="outlined" sx={{ p: 1.1, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" }}>
            <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.8} alignItems={{ sm: "center" }}>
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{clan.clanName}</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{clan.clanTag} | score {clan.totalScore}</Typography>
              </Box>
              <Button
                size="small"
                variant={selectedClanTag === clan.clanTag ? "contained" : "outlined"}
                onClick={() => onLoadClanDetails(clan.clanTag)}
              >
                Details
              </Button>
            </Stack>
          </Paper>
        ))}
      </Stack>

      {clanDetails && <ClanDetailsCard details={clanDetails} />}
    </Stack>
  );
}

function ClanDetailsCard({ details }) {
  const maxContributor = details.topContributors?.length
    ? Math.max(...details.topContributors.map((x) => x.totalContribution), 1)
    : 1;
  const maxHistory = details.recentWars?.length
    ? Math.max(...details.recentWars.map((x) => x.score), 1)
    : 1;

  return (
    <Paper variant="outlined" sx={{ p: 1.2, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" }}>
      <Typography sx={{ fontWeight: 800, overflowWrap: "anywhere" }}>{details.clanName}</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1, overflowWrap: "anywhere" }}>
        {details.clanTag} | current score {details.currentScore} | avg recent {details.averageRecentScore}
      </Typography>

      <Typography sx={{ fontWeight: 700, mb: 0.6 }}>Top contributors (current race)</Typography>
      <Stack spacing={0.8} sx={{ mb: 1.2 }}>
        {(details.topContributors ?? []).slice(0, 10).map((row, idx) => (
          <Box key={row.playerTag}>
            <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
              <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>{idx + 1}. {row.playerName}</Typography>
              <Typography variant="body2" color="text.secondary">{row.totalContribution}</Typography>
            </Stack>
            <LinearProgress
              variant="determinate"
              value={Math.min(100, (row.totalContribution / maxContributor) * 100)}
              sx={{ mt: 0.4, height: 8, borderRadius: 999, bgcolor: "rgba(255,255,255,0.08)" }}
            />
          </Box>
        ))}
      </Stack>

      <Typography sx={{ fontWeight: 700, mb: 0.6 }}>Past clan wars</Typography>
      <Stack spacing={0.8}>
        {(details.recentWars ?? []).map((war) => (
          <Box key={`${details.clanTag}-${war.warKey}`}>
            <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
              <Typography variant="body2">{war.warKey}</Typography>
              <Typography variant="body2" color="text.secondary">rank {war.rank} | score {war.score}</Typography>
            </Stack>
            <LinearProgress
              variant="determinate"
              value={Math.min(100, (war.score / maxHistory) * 100)}
              sx={{ mt: 0.4, height: 8, borderRadius: 999, bgcolor: "rgba(255,255,255,0.08)" }}
            />
          </Box>
        ))}
      </Stack>
    </Paper>
  );
}

function TelegramSyncTab({ sync, onReload, onRelink, busy }) {
  const [drafts, setDrafts] = useState({});
  const members = sync?.members ?? [];
  const linkedUsers = sync?.linkedUsers ?? [];

  const unlinkedMembers = useMemo(
    () => members.filter((x) => !x.isLinked).slice(0, 30),
    [members]
  );

  const linkedCount = members.filter((x) => x.isLinked).length;

  function setDraft(userId, value) {
    setDrafts((prev) => ({ ...prev, [userId]: value }));
  }

  function getDraft(user) {
    return drafts[user.platformUserId] ?? user.playerTag ?? "";
  }

  return (
    <Stack spacing={1.2}>
      <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1}>
        <Typography variant="body2" sx={{ color: "#9ec2da" }}>
          Linked in clan: {linkedCount}/{members.length} {sync?.platformGroupId ? `| chat ${sync.platformGroupId}` : "| chat not configured"}
        </Typography>
        <Button variant="outlined" onClick={onReload} disabled={busy}>Reload Sync</Button>
      </Stack>

      {!sync?.platformGroupId && (
        <Typography color="warning.main">
          Telegram chat is not configured for this clan. Run `/commands/setup/telegram` first.
        </Typography>
      )}

      <Paper variant="outlined" sx={{ p: 1.1, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" }}>
        <Typography sx={{ fontWeight: 700, mb: 0.8 }}>Unlinked clan members</Typography>
        {unlinkedMembers.length ? (
          <Stack spacing={0.7} sx={{ maxHeight: 260, overflowY: "auto" }}>
            {unlinkedMembers.map((member) => (
              <Stack key={`unlinked-${member.playerTag}`} direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
                <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>
                  {member.playerName} {member.isAuthorized ? "[auth]" : ""} ({member.playerTag})
                </Typography>
                <Typography variant="body2" color="text.secondary">left {member.battlesRemaining}</Typography>
              </Stack>
            ))}
          </Stack>
        ) : (
          <Typography variant="body2" color="text.secondary">All current clan members are linked.</Typography>
        )}
      </Paper>

      <Paper variant="outlined" sx={{ p: 1.1, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" }}>
        <Typography sx={{ fontWeight: 700, mb: 0.8 }}>Linked Telegram users</Typography>
        {linkedUsers.length ? (
          <Stack spacing={0.9} sx={{ maxHeight: 340, overflowY: "auto" }}>
            {linkedUsers.map((user) => (
              <Paper key={`linked-${user.platformUserId}`} variant="outlined" sx={{ p: 0.9, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(10,24,37,0.65)" }}>
                <Stack spacing={0.7}>
                  <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
                    <Typography variant="body2" sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>
                      {user.displayName} ({user.platformUserId})
                    </Typography>
                    <Typography variant="body2" color={user.inCurrentClan ? "success.main" : "warning.main"}>
                      {user.inCurrentClan ? "in current clan" : "not in current clan"}
                    </Typography>
                  </Stack>
                  <Stack direction={{ xs: "column", sm: "row" }} spacing={0.8}>
                    <TextField
                      fullWidth
                      size="small"
                      label="Player tag"
                      value={getDraft(user)}
                      onChange={(e) => setDraft(user.platformUserId, e.target.value)}
                      placeholder="#PLAYER"
                    />
                    <Button
                      variant="contained"
                      onClick={() => onRelink(user.platformUserId, user.displayName, getDraft(user))}
                      disabled={busy || !sync?.platformGroupId || !getDraft(user).trim()}
                    >
                      Relink
                    </Button>
                  </Stack>
                </Stack>
              </Paper>
            ))}
          </Stack>
        ) : (
          <Typography variant="body2" color="text.secondary">No linked Telegram users yet.</Typography>
        )}
      </Paper>
    </Stack>
  );
}

function ScrollList({ title, members, emptyText, authorizedSet }) {
  return (
    <Paper variant="outlined" sx={{ flex: 1, minWidth: 0, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", display: "flex", flexDirection: "column", overflow: "hidden" }}>
      <Box sx={{ px: 1.2, py: 1, borderBottom: "1px solid rgba(132,186,217,0.2)" }}>
        <Typography variant="subtitle2">{title}</Typography>
      </Box>
      <Box sx={{ maxHeight: 320, overflowY: "auto", px: 1.2, py: 0.8 }}>
        {members?.length ? (
          <Stack spacing={0.8}>
            {members.map((member) => {
              const normalized = normalizeTag(member.playerTag);
              const isAuthorized = authorizedSet.has(normalized);
              const progress = Math.min(100, ((member.battlesPlayed ?? 0) / 4) * 100);

              return (
                <Paper key={`${member.playerTag}-${title}`} variant="outlined" sx={{ p: 0.9, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(10,24,37,0.65)", overflow: "hidden" }}>
                  <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
                    <Typography sx={{ fontWeight: 600, overflowWrap: "anywhere" }}>
                      {member.playerName} {isAuthorized ? "[auth]" : ""}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      left {member.battlesRemaining ?? 0}
                    </Typography>
                  </Stack>
                  <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>
                    {member.playerTag} | played {member.battlesPlayed ?? 0}/4
                  </Typography>
                  <LinearProgress
                    variant="determinate"
                    value={progress}
                    sx={{ mt: 0.5, height: 7, borderRadius: 999, bgcolor: "rgba(255,255,255,0.08)" }}
                  />
                </Paper>
              );
            })}
          </Stack>
        ) : (
          <Typography variant="body2" color="text.secondary">{emptyText}</Typography>
        )}
      </Box>
    </Paper>
  );
}

function normalizeTag(tag) {
  const value = String(tag ?? "").trim().toUpperCase();
  return value.startsWith("#") ? value : `#${value}`;
}
