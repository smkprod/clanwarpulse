import { useMemo, useState } from "react";
import { Box, Button, Chip, Divider, LinearProgress, Paper, Stack, Tab, Tabs, TextField, Typography } from "@mui/material";

export function DashboardTabs(props) {
  const {
    tab, onTabChange, dashboard, authorizedTags, selectedClanTag, clanDetails, onRefresh, onLoadClanDetails,
    onOpenBot, hasBotLink, onNotifyNotPlayed, canNotifyNotPlayed, telegramSync, onOpenPlayerProfile,
    onLoadTelegramSync, onRelinkTelegramUser, busy, identity
  } = props;

  return (
    <Paper elevation={0} sx={{ p: 1.6, border: "1px solid rgba(132,186,217,0.2)", backdropFilter: "blur(6px)", overflow: "hidden" }}>
      <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1.2} alignItems={{ sm: "center" }}>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ minWidth: 0 }}>
          <InfoChip text={`Игрок ${identity.playerName}`} />
          <InfoChip text={`Клан ${identity.clanName}`} color="primary" />
          <InfoChip text={`Война ${dashboard.warKey}`} color="secondary" />
        </Stack>
        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
          <Button variant="outlined" onClick={onRefresh} disabled={busy}>Обновить</Button>
          <Button variant="outlined" color="warning" onClick={onNotifyNotPlayed} disabled={busy || !canNotifyNotPlayed}>Отметить не сыгравших</Button>
          {hasBotLink && <Button variant="contained" color="secondary" onClick={onOpenBot}>Открыть бота</Button>}
        </Stack>
      </Stack>

      <Tabs value={tab} onChange={(_, value) => onTabChange(value)} sx={{ mt: 1.5, mb: 1.5 }} variant="scrollable" scrollButtons="auto">
        <Tab label="Активность" />
        <Tab label="Соперники" />
        <Tab label="Прогноз" />
        <Tab label="История" />
        <Tab label="Кланы" />
        <Tab label="Telegram" />
      </Tabs>
      <Divider sx={{ mb: 1.5, borderColor: "rgba(132,186,217,0.18)" }} />

      {tab === 0 && <ActivityTab dashboard={dashboard} authorizedTags={authorizedTags} onOpenPlayerProfile={onOpenPlayerProfile} busy={busy} />}
      {tab === 1 && <OpponentsTab dashboard={dashboard} identity={identity} />}
      {tab === 2 && <ForecastTab dashboard={dashboard} />}
      {tab === 3 && <HistoryTab dashboard={dashboard} />}
      {tab === 4 && <ClansTab dashboard={dashboard} selectedClanTag={selectedClanTag} clanDetails={clanDetails} onLoadClanDetails={onLoadClanDetails} />}
      {tab === 5 && <TelegramSyncTab sync={telegramSync} onReload={onLoadTelegramSync} onRelink={onRelinkTelegramUser} busy={busy} />}
    </Paper>
  );
}

function ActivityTab({ dashboard, authorizedTags, onOpenPlayerProfile, busy }) {
  const authorizedSet = new Set((authorizedTags ?? []).map(normalizeTag));
  const members = [...(dashboard.played ?? []), ...(dashboard.notPlayed ?? [])];
  const totalRemaining = members.reduce((sum, m) => sum + (m.battlesRemaining ?? 0), 0);
  const totalContribution = members.reduce((sum, m) => sum + (m.totalContribution ?? 0), 0);

  return (
    <Stack spacing={1.2}>
      <Stack direction={{ xs: "column", md: "row" }} spacing={1}>
        <MetricCard label="Осталось боев" value={totalRemaining} helper="Суммарно по клану" />
        <MetricCard label="Очки клана" value={totalContribution} helper="Текущий вклад по КВ" />
        <MetricCard label="Игроков в бою" value={`${dashboard.played?.length ?? 0}/${members.length}`} helper="Нажмите на ник, откроется отдельная страница" />
      </Stack>
      <Stack direction={{ xs: "column", md: "row" }} spacing={1.5}>
        <ScrollList title="Сыграли" members={dashboard.played} emptyText="Пока никто не сыграл." authorizedSet={authorizedSet} onSelectPlayer={onOpenPlayerProfile} busy={busy} />
        <ScrollList title="Не сыграли" members={dashboard.notPlayed} emptyText="Все уже сыграли." authorizedSet={authorizedSet} onSelectPlayer={onOpenPlayerProfile} busy={busy} />
      </Stack>
    </Stack>
  );
}

function OpponentsTab({ dashboard, identity }) {
  const raceClans = [...(dashboard.currentRaceClans ?? [])].sort((a, b) => b.totalScore - a.totalScore);
  const opponents = dashboard.opponents?.length ? dashboard.opponents : raceClans.filter((x) => normalizeTag(x.clanTag) !== normalizeTag(dashboard.clanTag));
  const maxScore = raceClans.length ? Math.max(...raceClans.map((x) => x.totalScore), 1) : 1;
  if (!raceClans.length) return <Typography color="text.secondary">Данные по соперникам пока недоступны.</Typography>;

  return (
    <Stack spacing={1.2}>
      <Paper variant="outlined" sx={cardSx}>
        <Typography sx={{ fontWeight: 700, mb: 0.8 }}>Текущая ситуация в речной гонке</Typography>
        <Stack spacing={0.8}>{raceClans.map((clan, index) => {
          const isOwn = normalizeTag(clan.clanTag) === normalizeTag(dashboard.clanTag);
          return (
            <Box key={`race-${clan.clanTag}`}>
              <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
                <Typography variant="body2" sx={{ fontWeight: isOwn ? 800 : 600, overflowWrap: "anywhere" }}>{index + 1}. {clan.clanName}{isOwn ? ` (ваш клан: ${identity.clanName})` : ""}</Typography>
                <Typography variant="body2" color="text.secondary">сейчас {clan.periodPoints ?? 0} | всего {clan.totalScore}</Typography>
              </Stack>
              <LinearProgress variant="determinate" value={Math.min(100, (clan.totalScore / maxScore) * 100)} sx={progressSx} />
            </Box>
          );
        })}</Stack>
      </Paper>
      {opponents.map((opponent) => (
        <Paper key={opponent.clanTag} variant="outlined" sx={cardSx}>
          <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1}>
            <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{opponent.clanName}</Typography>
            <Typography color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{opponent.clanTag}</Typography>
          </Stack>
          <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: "wrap" }} useFlexGap>
            <Chip size="small" label={`Счет ${opponent.totalScore}`} />
            <Chip size="small" color="secondary" label={`Сейчас ${opponent.periodPoints ?? 0}`} />
            <Chip size="small" label={`Слава ${opponent.fame}`} />
            <Chip size="small" label={`Починка ${opponent.repairPoints}`} />
            <Chip size="small" label={`Игроков ${opponent.participantsCount}`} />
          </Stack>
        </Paper>
      ))}
    </Stack>
  );
}

function ForecastTab({ dashboard }) {
  const raceClans = dashboard.currentRaceClans ?? [];
  const forecastMap = new Map((dashboard.forecast?.ranking ?? []).map((x) => [normalizeTag(x.clanTag), x]));
  const ranking = raceClans.map((clan) => forecastMap.get(normalizeTag(clan.clanTag)) ?? ({ clanTag: clan.clanTag, clanName: clan.clanName, predictedScore: clan.totalScore, recentAverageScore: clan.totalScore, recentAverageRank: 5, sampleSize: 0 })).sort((a, b) => b.predictedScore - a.predictedScore);
  const maxForecast = ranking.length ? Math.max(...ranking.map((item) => item.predictedScore), 1) : 1;
  if (!ranking.length) return <Typography color="text.secondary">Для прогноза пока недостаточно данных.</Typography>;

  return <Stack spacing={1}>{ranking.map((item, index) => <Paper key={item.clanTag} variant="outlined" sx={cardSx}><Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1}><Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{index + 1}. {item.clanName}</Typography><Typography color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{item.clanTag}</Typography></Stack><Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: "wrap" }} useFlexGap><Chip size="small" color="primary" label={`Прогноз ${item.predictedScore}`} /><Chip size="small" label={`Средний счет ${item.recentAverageScore}`} /><Chip size="small" label={`Среднее место ${item.recentAverageRank}`} /><Chip size="small" label={`Матчей ${item.sampleSize}`} /></Stack><Box sx={{ mt: 1.2 }}><LinearProgress variant="determinate" value={Math.min(100, (item.predictedScore / maxForecast) * 100)} sx={{ ...progressSx, height: 9 }} /></Box></Paper>)}</Stack>;
}

function HistoryTab({ dashboard }) {
  const history = dashboard.history ?? [];
  if (!history.length) return <Typography color="text.secondary">История пока недоступна.</Typography>;
  return <Stack spacing={1.2}>{history.map((race) => { const maxScore = race.results.length ? Math.max(...race.results.map((r) => r.score), 1) : 1; return <Paper key={race.warKey} variant="outlined" sx={cardSx}><Typography sx={{ fontWeight: 700, mb: 1, overflowWrap: "anywhere" }}>{race.warKey}</Typography><Stack spacing={0.9}>{race.results.map((row) => <Box key={`${race.warKey}-${row.clanTag}`}><Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}><Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>{row.rank}. {row.clanName}</Typography><Typography variant="body2" color="text.secondary">{row.score}</Typography></Stack><LinearProgress variant="determinate" value={Math.min(100, (row.score / maxScore) * 100)} sx={progressSx} /></Box>)}</Stack></Paper>; })}</Stack>;
}

function ClansTab({ dashboard, selectedClanTag, clanDetails, onLoadClanDetails }) {
  const clans = dashboard.currentRaceClans ?? [];
  if (!clans.length) return <Typography color="text.secondary">Кланы текущей гонки пока недоступны.</Typography>;
  return (
    <Stack spacing={1.2}>
      <Typography variant="body2" sx={{ color: "#9ec2da" }}>Выберите клан, чтобы посмотреть лучших участников и прошлые войны.</Typography>
      <Stack spacing={0.8}>{clans.map((clan) => <Paper key={clan.clanTag} variant="outlined" sx={cardSx}><Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.8} alignItems={{ sm: "center" }}><Box sx={{ minWidth: 0 }}><Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{clan.clanName}</Typography><Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{clan.clanTag} | счет {clan.totalScore}</Typography></Box><Button size="small" variant={selectedClanTag === clan.clanTag ? "contained" : "outlined"} onClick={() => onLoadClanDetails(clan.clanTag)}>Подробнее</Button></Stack></Paper>)}</Stack>
      {clanDetails && <ClanDetailsCard details={clanDetails} />}
    </Stack>
  );
}

function ClanDetailsCard({ details }) {
  const maxContributor = details.topContributors?.length ? Math.max(...details.topContributors.map((x) => x.totalContribution), 1) : 1;
  const maxHistory = details.recentWars?.length ? Math.max(...details.recentWars.map((x) => x.score), 1) : 1;
  return (
    <Paper variant="outlined" sx={cardSx}>
      <Typography sx={{ fontWeight: 800, overflowWrap: "anywhere" }}>{details.clanName}</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1, overflowWrap: "anywhere" }}>{details.clanTag} | текущий счет {details.currentScore} | средний недавний {details.averageRecentScore}</Typography>
      <Typography sx={{ fontWeight: 700, mb: 0.6 }}>Лучшие участники текущей гонки</Typography>
      <Stack spacing={0.8} sx={{ mb: 1.2 }}>{(details.topContributors ?? []).slice(0, 10).map((row, idx) => <Box key={row.playerTag}><Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}><Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>{idx + 1}. {row.playerName}</Typography><Typography variant="body2" color="text.secondary">{row.totalContribution}</Typography></Stack><LinearProgress variant="determinate" value={Math.min(100, (row.totalContribution / maxContributor) * 100)} sx={progressSx} /></Box>)}</Stack>
      <Typography sx={{ fontWeight: 700, mb: 0.6 }}>Прошлые войны кланов</Typography>
      <Stack spacing={0.8}>{(details.recentWars ?? []).map((war) => <Box key={`${details.clanTag}-${war.warKey}`}><Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}><Typography variant="body2">{war.warKey}</Typography><Typography variant="body2" color="text.secondary">место {war.rank} | счет {war.score}</Typography></Stack><LinearProgress variant="determinate" value={Math.min(100, (war.score / maxHistory) * 100)} sx={progressSx} /></Box>)}</Stack>
    </Paper>
  );
}

function TelegramSyncTab({ sync, onReload, onRelink, busy }) {
  const [drafts, setDrafts] = useState({});
  const members = sync?.members ?? [];
  const linkedUsers = sync?.linkedUsers ?? [];
  const unlinkedMembers = useMemo(() => members.filter((x) => !x.isLinked).slice(0, 30), [members]);
  const linkedCount = members.filter((x) => x.isLinked).length;
  const setDraft = (userId, value) => setDrafts((prev) => ({ ...prev, [userId]: value }));
  const getDraft = (user) => drafts[user.platformUserId] ?? user.playerTag ?? "";

  return (
    <Stack spacing={1.2}>
      <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1}>
        <Typography variant="body2" sx={{ color: "#9ec2da" }}>Привязано в клане: {linkedCount}/{members.length} {sync?.platformGroupId ? `| чат ${sync.platformGroupId}` : "| чат не настроен"}</Typography>
        <Button variant="outlined" onClick={onReload} disabled={busy}>Обновить синхронизацию</Button>
      </Stack>
      {!sync?.platformGroupId && <Typography color="warning.main">Для этого клана Telegram-чат не настроен. Сначала выполните `/setup #CLANTAG`.</Typography>}
      <Paper variant="outlined" sx={cardSx}><Typography sx={{ fontWeight: 700, mb: 0.8 }}>Непривязанные участники</Typography>{unlinkedMembers.length ? <Stack spacing={0.7} sx={{ maxHeight: 260, overflowY: "auto" }}>{unlinkedMembers.map((member) => <Stack key={`unlinked-${member.playerTag}`} direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}><Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>{member.playerName} {member.isAuthorized ? "[авторизован]" : ""} ({member.playerTag})</Typography><Typography variant="body2" color="text.secondary">осталось {member.battlesRemaining}</Typography></Stack>)}</Stack> : <Typography variant="body2" color="text.secondary">Все текущие участники клана уже привязаны.</Typography>}</Paper>
      <Paper variant="outlined" sx={cardSx}><Typography sx={{ fontWeight: 700, mb: 0.8 }}>Привязанные пользователи Telegram</Typography>{linkedUsers.length ? <Stack spacing={0.9} sx={{ maxHeight: 340, overflowY: "auto" }}>{linkedUsers.map((user) => <Paper key={`linked-${user.platformUserId}`} variant="outlined" sx={{ p: 0.9, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(10,24,37,0.65)" }}><Stack spacing={0.7}><Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}><Typography variant="body2" sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{user.displayName} ({user.platformUserId})</Typography><Typography variant="body2" color={user.inCurrentClan ? "success.main" : "warning.main"}>{user.inCurrentClan ? "в текущем клане" : "не в текущем клане"}</Typography></Stack><Stack direction={{ xs: "column", sm: "row" }} spacing={0.8}><TextField fullWidth size="small" label="Тег игрока" value={getDraft(user)} onChange={(e) => setDraft(user.platformUserId, e.target.value)} placeholder="#PLAYER" /><Button variant="contained" onClick={() => onRelink(user.platformUserId, user.displayName, getDraft(user))} disabled={busy || !sync?.platformGroupId || !getDraft(user).trim()}>Перепривязать</Button></Stack></Stack></Paper>)}</Stack> : <Typography variant="body2" color="text.secondary">Пока нет привязанных пользователей Telegram.</Typography>}</Paper>
    </Stack>
  );
}

function ScrollList({ title, members, emptyText, authorizedSet, onSelectPlayer, busy }) {
  return (
    <Paper variant="outlined" sx={{ flex: 1, minWidth: 0, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", display: "flex", flexDirection: "column", overflow: "hidden" }}>
      <Box sx={{ px: 1.2, py: 1, borderBottom: "1px solid rgba(132,186,217,0.2)" }}><Typography variant="subtitle2">{title}</Typography></Box>
      <Box sx={{ maxHeight: 320, overflowY: "auto", px: 1.2, py: 0.8 }}>{members?.length ? <Stack spacing={0.8}>{members.map((member) => { const normalized = normalizeTag(member.playerTag); return <Paper key={`${member.playerTag}-${title}`} variant="outlined" sx={{ p: 0.9, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(10,24,37,0.65)", overflow: "hidden" }}><Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}><Button variant="text" disabled={busy} onClick={() => onSelectPlayer(member.playerTag)} sx={{ justifyContent: "flex-start", px: 0, minWidth: 0, textTransform: "none", fontWeight: 700, overflowWrap: "anywhere" }}>{member.playerName} {authorizedSet.has(normalized) ? "[авторизован]" : ""}</Button><Typography variant="body2" color="text.secondary">осталось {member.battlesRemaining ?? 0}</Typography></Stack><Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{member.playerTag} | сыграно {member.battlesPlayed ?? 0}/4{member.totalContribution != null ? ` | очки ${member.totalContribution}` : ""}</Typography><LinearProgress variant="determinate" value={Math.min(100, ((member.battlesPlayed ?? 0) / 4) * 100)} sx={progressSx} /></Paper>; })}</Stack> : <Typography variant="body2" color="text.secondary">{emptyText}</Typography>}</Box>
    </Paper>
  );
}

function MetricCard({ label, value, helper }) {
  return <Paper variant="outlined" sx={{ flex: 1, p: 1.1, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)" }}><Typography variant="body2" sx={{ color: "#9ec2da" }}>{label}</Typography><Typography variant="h5" sx={{ fontWeight: 800, mt: 0.2 }}>{value}</Typography><Typography variant="caption" color="text.secondary">{helper}</Typography></Paper>;
}

function InfoChip({ text, color }) {
  return <Chip label={text} color={color} size="small" sx={{ maxWidth: "100%", "& .MuiChip-label": { overflow: "hidden", textOverflow: "ellipsis" } }} />;
}

function normalizeTag(tag) { const value = String(tag ?? "").trim().toUpperCase(); return value.startsWith("#") ? value : `#${value}`; }
const cardSx = { p: 1.2, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" };
const progressSx = { mt: 0.45, height: 8, borderRadius: 999, bgcolor: "rgba(255,255,255,0.08)" };
