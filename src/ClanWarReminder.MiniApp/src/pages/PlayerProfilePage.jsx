import { Box, Button, Chip, LinearProgress, MenuItem, Paper, Stack, TextField, Typography } from "@mui/material";

export function PlayerProfilePage({ profile, profileWindowWeeks, onWindowChange, onBack, onRefresh, busy }) {
  if (!profile) {
    return (
      <Paper elevation={0} sx={shellSx}>
        <Stack spacing={1.2}>
          <Button variant="outlined" onClick={onBack} sx={{ alignSelf: "flex-start" }}>Назад</Button>
          <Typography color="text.secondary">Профиль игрока пока не загружен.</Typography>
        </Stack>
      </Paper>
    );
  }

  return (
    <Paper elevation={0} sx={shellSx}>
      <Stack spacing={1.2}>
        <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={1}>
          <Button variant="outlined" onClick={onBack} sx={{ alignSelf: "flex-start" }}>Назад к клану</Button>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ alignSelf: "flex-start" }}>
            <TextField
              select
              size="small"
              label="Диапазон КВ"
              value={profileWindowWeeks}
              onChange={(e) => onWindowChange(Number(e.target.value))}
              sx={{ minWidth: 140 }}
            >
              {[3, 5, 7, 10].map((value) => (
                <MenuItem key={value} value={value}>
                  Последние {value}
                </MenuItem>
              ))}
            </TextField>
            <Button variant="contained" onClick={onRefresh} disabled={busy} sx={{ alignSelf: "flex-start" }}>Обновить профиль</Button>
          </Stack>
        </Stack>

        <Paper variant="outlined" sx={cardSx}>
          <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" spacing={1}>
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontWeight: 800, fontSize: "1.2rem", overflowWrap: "anywhere" }}>{profile.playerName}</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{profile.playerTag} • {profile.currentClanName} • {profile.currentClanTag}</Typography>
            </Box>
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
              <InfoChip text={`Статус ${profile.activityLabel}`} color={profile.activityLabel === "Активный" ? "success" : profile.activityLabel === "Нестабильный" ? "warning" : "default"} />
              <InfoChip text={`Рейтинг ${profile.activityScore}/100`} color="primary" />
              <InfoChip text={`Окно ${profile.profileWindowWeeks} КВ`} />
              <InfoChip text={`Качество ${profile.dataQualityLabel}`} />
            </Stack>
          </Stack>
        </Paper>

        <Stack direction={{ xs: "column", md: "row" }} spacing={1}>
          <MetricCard label="Участие" value={`${fmt(profile.overallParticipationRate)}%`} helper={`Покрытие ${profile.availableHistoryWeeks} КВ, показано ${Math.min(profile.profileWindowWeeks, profile.availableHistoryWeeks || profile.profileWindowWeeks)}`} />
          <MetricCard label="Среднее боев" value={fmt(profile.averageBattlesPerWeek)} helper={`Всего учтено боев ${profile.totalTrackedWarBattles}`} />
          <MetricCard label="Прогноз" value={`${profile.predictedNextWeekBattles}/16`} helper={`${profile.predictedNextWeekContribution} очков в следующем КВ`} />
          <MetricCard label="Сейчас" value={`${profile.currentWeekBattlesPlayed}/16`} helper={`${profile.currentWeekContribution} очков в текущем КВ`} />
        </Stack>

        <Paper variant="outlined" sx={cardSx}>
          <Typography sx={{ fontWeight: 700, mb: 0.8 }}>Почему такой прогноз</Typography>
          <Typography variant="body2" color="text.secondary">
            Основа профиля считается по последним {profile.profileWindowWeeks} КВ. Сейчас доступно {profile.availableHistoryWeeks} КВ истории, качество оценки: {profile.dataQualityLabel.toLowerCase()}.
            Недели с высоким вкладом и большим числом боев получают больший вес, чтобы колизей влиял на прогноз сильнее обычной речной гонки.
          </Typography>
        </Paper>

        <Stack direction={{ xs: "column", lg: "row" }} spacing={1.2}>
          <Paper variant="outlined" sx={{ ...cardSx, flex: 1.2 }}>
            <Typography sx={{ fontWeight: 700, mb: 0.8 }}>Последние {profile.profileWindowWeeks} КВ</Typography>
            <TrendChart weeks={profile.recentWeeks ?? []} />
          </Paper>
          <Paper variant="outlined" sx={{ ...cardSx, flex: 1 }}>
            <Typography sx={{ fontWeight: 700, mb: 0.8 }}>История кланов</Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 0.8 }}>
              {profile.clanHistoryNote}
            </Typography>
            {(profile.recentClans ?? []).length ? (
              <Stack spacing={0.7}>
                {profile.recentClans.map((clan) => (
                  <Paper key={`${clan.clanTag}-${clan.lastSeenAtUtc}`} variant="outlined" sx={miniCardSx}>
                    <Typography variant="body2" sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{clan.clanName}</Typography>
                    <Typography variant="caption" color="text.secondary" sx={{ display: "block", overflowWrap: "anywhere" }}>
                      {clan.clanTag} • боев {clan.warBattles} • с {formatDate(clan.firstSeenAtUtc)} по {formatDate(clan.lastSeenAtUtc)}
                    </Typography>
                  </Paper>
                ))}
              </Stack>
            ) : (
              <Typography color="text.secondary">Нет доступной истории кланов.</Typography>
            )}
          </Paper>
        </Stack>

        <Paper variant="outlined" sx={cardSx}>
          <Typography sx={{ fontWeight: 700, mb: 0.8 }}>История по КВ</Typography>
          <Stack spacing={0.9}>
            {(profile.recentWeeks ?? []).map((week) => (
              <Box key={`${week.warKey}-${week.clanTag}`}>
                <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.4}>
                  <Typography variant="body2" sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>
                    {week.warKey} • {week.clanName} {week.isColosseumWeighted ? "• приоритет для прогноза" : ""}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {week.battlesPlayed}/{week.maxBattles} • {fmt(week.participationRate)}%{week.totalContribution != null ? ` • ${week.totalContribution} очков` : ""}
                  </Typography>
                </Stack>
                <LinearProgress variant="determinate" value={Math.min(100, week.participationRate)} sx={progressSx} />
              </Box>
            ))}
          </Stack>
        </Paper>
      </Stack>
    </Paper>
  );
}

function TrendChart({ weeks }) {
  if (!weeks.length) {
    return <Typography color="text.secondary">Нет данных для графика.</Typography>;
  }

  const ordered = [...weeks].reverse();
  const maxContribution = Math.max(...ordered.map((x) => x.totalContribution ?? 0), 1);
  return (
    <Stack spacing={1}>
      {ordered.map((week) => (
        <Box key={`${week.warKey}-${week.clanTag}`}>
          <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.3}>
            <Typography variant="body2">{week.warKey}</Typography>
            <Typography variant="caption" color="text.secondary">
              {week.totalContribution != null ? `${week.totalContribution} очков` : "очки недоступны"} • {week.battlesPlayed}/16
            </Typography>
          </Stack>
          <Stack direction="row" spacing={0.7} sx={{ mt: 0.45 }}>
            <Box sx={{ flex: 1 }}>
              <LinearProgress variant="determinate" value={Math.min(100, ((week.totalContribution ?? 0) / maxContribution) * 100)} sx={progressSx} />
            </Box>
            <Box sx={{ flex: 1 }}>
              <LinearProgress color="secondary" variant="determinate" value={Math.min(100, (week.battlesPlayed / 16) * 100)} sx={progressSx} />
            </Box>
          </Stack>
        </Box>
      ))}
      <Typography variant="caption" color="text.secondary">Первая полоса: очки КВ. Вторая: сыгранные бои.</Typography>
    </Stack>
  );
}

function MetricCard({ label, value, helper }) {
  return (
    <Paper variant="outlined" sx={{ flex: 1, p: 1.1, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)" }}>
      <Typography variant="body2" sx={{ color: "#9ec2da" }}>{label}</Typography>
      <Typography variant="h5" sx={{ fontWeight: 800, mt: 0.2 }}>{value}</Typography>
      <Typography variant="caption" color="text.secondary">{helper}</Typography>
    </Paper>
  );
}

function InfoChip({ text, color }) {
  return <Chip label={text} color={color} size="small" sx={{ maxWidth: "100%", "& .MuiChip-label": { overflow: "hidden", textOverflow: "ellipsis" } }} />;
}

function fmt(value) {
  const number = Number(value ?? 0);
  return Number.isInteger(number) ? String(number) : number.toFixed(1);
}

function formatDate(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? String(value) : new Intl.DateTimeFormat("ru-RU", { day: "2-digit", month: "2-digit", year: "numeric" }).format(date);
}

const shellSx = { p: 1.6, border: "1px solid rgba(132,186,217,0.2)", backdropFilter: "blur(6px)", overflow: "hidden" };
const cardSx = { p: 1.2, borderColor: "rgba(132,186,217,0.2)", bgcolor: "rgba(6,17,27,0.6)", overflow: "hidden" };
const miniCardSx = { p: 0.8, borderColor: "rgba(132,186,217,0.14)", bgcolor: "rgba(8,19,29,0.58)" };
const progressSx = { mt: 0.45, height: 8, borderRadius: 999, bgcolor: "rgba(255,255,255,0.08)" };
