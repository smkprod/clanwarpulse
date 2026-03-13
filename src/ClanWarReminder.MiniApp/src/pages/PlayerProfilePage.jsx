import { Button, Chip, LinearProgress, MenuItem, Paper, Stack, TextField, Typography } from "@mui/material";
import { getLocale } from "../i18n";

export function PlayerProfilePage({ copy, language, profile, profileWindowWeeks, onWindowChange, onBack, onRefresh, busy }) {
  if (!profile) {
    return (
      <Paper elevation={0} sx={surfaceSx}>
        <Stack spacing={1.2}>
          <Button variant="outlined" onClick={onBack} sx={{ alignSelf: "flex-start" }}>
            {copy.profile.back}
          </Button>
          <Typography color="text.secondary">{copy.profile.noProfile}</Typography>
        </Stack>
      </Paper>
    );
  }

  const recentWeeks = profile.recentWeeks ?? [];
  const playedWeeks = recentWeeks.filter((week) => (week.totalContribution ?? 0) > 0);
  const averageContributionPerWeek = playedWeeks.length
    ? playedWeeks.reduce((sum, week) => sum + (week.totalContribution ?? 0), 0) / playedWeeks.length
    : 0;
  const averageContributionPerBattle =
    profile.totalTrackedWarBattles > 0
      ? recentWeeks.reduce((sum, week) => sum + (week.totalContribution ?? 0), 0) / profile.totalTrackedWarBattles
      : 0;
  const recentFormScore = recentWeeks.slice(0, 3).reduce((sum, week) => sum + (week.totalContribution ?? 0), 0);

  return (
    <Stack spacing={1.5}>
      <Paper elevation={0} sx={surfaceSx}>
        <Stack spacing={1.6}>
          <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" spacing={1}>
            <Stack spacing={0.8}>
              <Button variant="outlined" onClick={onBack} sx={{ alignSelf: "flex-start" }}>
                {copy.profile.back}
              </Button>
              <Stack spacing={0.35}>
                <Typography variant="h5">{copy.profile.title}</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>
                  {profile.playerName} · {profile.playerTag} · {profile.currentClanName}
                </Typography>
              </Stack>
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
              <TextField
                select
                size="small"
                label={copy.profile.range}
                value={profileWindowWeeks}
                onChange={(event) => onWindowChange(Number(event.target.value))}
                sx={{ minWidth: 150 }}
              >
                {[3, 5, 7, 10].map((value) => (
                  <MenuItem key={value} value={value}>
                    {value}
                  </MenuItem>
                ))}
              </TextField>
              <Button variant="contained" onClick={onRefresh} disabled={busy}>
                {copy.profile.refresh}
              </Button>
            </Stack>
          </Stack>

          <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
            <Chip label={`${copy.profile.status}: ${profile.activityLabel}`} color={mapStatusColor(profile.activityLabel)} />
            <Chip label={`${copy.profile.dataQuality}: ${profile.dataQualityLabel}`} variant="outlined" />
            <Chip label={`${copy.profile.range}: ${profile.profileWindowWeeks}`} variant="outlined" />
          </Stack>

          <MetricGrid copy={copy} profile={profile} averageContributionPerWeek={averageContributionPerWeek} averageContributionPerBattle={averageContributionPerBattle} recentFormScore={recentFormScore} />
        </Stack>
      </Paper>

      <Stack direction={{ xs: "column", lg: "row" }} spacing={1.5}>
        <Paper elevation={0} sx={{ ...surfaceSx, flex: 1.2 }}>
          <Typography variant="h6" sx={{ mb: 1.1 }}>
            {copy.profile.recentWars}
          </Typography>
          {(profile.recentWeeks ?? []).length ? (
            <Stack spacing={1}>
              {profile.recentWeeks.map((week) => (
                <Paper key={`${week.warKey}-${week.clanTag}`} elevation={0} sx={innerCardSx}>
                  <Stack spacing={0.5}>
                    <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={0.5}>
                      <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>
                        {week.warKey} · {week.clanName}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {week.battlesPlayed}/{week.maxBattles} · {week.totalContribution ?? 0} pts
                      </Typography>
                    </Stack>
                    <Typography variant="body2" color="text.secondary">
                      WR {fmt(week.warWinRate ?? 0)}% · Participation {fmt(week.participationRate)}%
                    </Typography>
                    <LinearProgress value={Math.min(100, week.participationRate)} variant="determinate" sx={progressSx} />
                  </Stack>
                </Paper>
              ))}
            </Stack>
          ) : (
            <Typography color="text.secondary">{copy.profile.noWarHistory}</Typography>
          )}
        </Paper>

        <Stack spacing={1.5} sx={{ flex: 0.95 }}>
          <Paper elevation={0} sx={surfaceSx}>
            <Typography variant="h6" sx={{ mb: 1.1 }}>
              {copy.profile.recentClans}
            </Typography>
            {(profile.recentClans ?? []).length ? (
              <Stack spacing={0.9}>
                {profile.recentClans.map((clan) => (
                  <Paper key={`${clan.clanTag}-${clan.lastSeenAtUtc}`} elevation={0} sx={innerCardSx}>
                    <Typography sx={{ fontWeight: 700, overflowWrap: "anywhere" }}>{clan.clanName}</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>
                      {clan.clanTag} · {clan.totalContribution} pts · WR {fmt(clan.warWinRate ?? 0)}%
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {formatDate(clan.firstSeenAtUtc, language)} - {formatDate(clan.lastSeenAtUtc, language)}
                    </Typography>
                  </Paper>
                ))}
              </Stack>
            ) : (
              <Typography color="text.secondary">{copy.profile.noClanHistory}</Typography>
            )}
          </Paper>

          <Paper elevation={0} sx={surfaceSx}>
            <Typography variant="h6" sx={{ mb: 0.7 }}>
              {copy.profile.why}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {copy.profile.whyText}
            </Typography>
          </Paper>
        </Stack>
      </Stack>
    </Stack>
  );
}

function MetricGrid({ copy, profile, averageContributionPerWeek, averageContributionPerBattle, recentFormScore }) {
  return (
    <>
      <Stack direction={{ xs: "column", md: "row" }} spacing={1.1}>
        <MetricCard label={copy.profile.metrics.participation} value={`${fmt(profile.overallParticipationRate)}%`} helper={`${profile.availableHistoryWeeks} wars`} />
        <MetricCard label={copy.profile.metrics.avgBattles} value={fmt(profile.averageBattlesPerWeek)} helper={`${profile.totalTrackedWarBattles} tracked`} />
        <MetricCard label={copy.profile.metrics.winRate} value={`${fmt(profile.recentWarWinRate)}%`} helper={`${profile.recentWarWins}-${profile.recentWarLosses}`} />
        <MetricCard label={copy.profile.metrics.prediction} value={`${profile.predictedNextWeekBattles}/16`} helper={`${profile.predictedNextWeekContribution} pts`} />
        <MetricCard label={copy.profile.metrics.current} value={`${profile.currentWeekBattlesPlayed}/16`} helper={`${profile.currentWeekContribution} pts`} />
      </Stack>

      <Stack direction={{ xs: "column", md: "row" }} spacing={1.1}>
        <MetricCard label={copy.profile.metrics.avgWeek} value={fmt(averageContributionPerWeek)} helper="Per war window" />
        <MetricCard label={copy.profile.metrics.avgBattle} value={fmt(averageContributionPerBattle)} helper="Average per battle" />
        <MetricCard label={copy.profile.metrics.fullCompletion} value={`${fmt(profile.fullCompletionRate)}%`} helper="Closed all battles" />
        <MetricCard label={copy.profile.metrics.form} value={fmt(recentFormScore)} helper="Last 3 wars total" />
      </Stack>
    </>
  );
}

function MetricCard({ label, value, helper }) {
  return (
    <Paper elevation={0} sx={{ ...innerCardSx, flex: 1 }}>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="h5" sx={{ mt: 0.3 }}>
        {value}
      </Typography>
      <Typography variant="caption" color="text.secondary">
        {helper}
      </Typography>
    </Paper>
  );
}

function fmt(value) {
  const number = Number(value ?? 0);
  return Number.isInteger(number) ? String(number) : number.toFixed(1);
}

function formatDate(value, language) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return new Intl.DateTimeFormat(getLocale(language), {
    day: "2-digit",
    month: "short",
    year: "numeric"
  }).format(date);
}

function mapStatusColor(label) {
  if (label === "Активный" || label === "Active") {
    return "success";
  }

  if (label === "Нестабильный" || label === "Unstable") {
    return "warning";
  }

  return "default";
}

const surfaceSx = {
  p: { xs: 1.5, md: 1.8 },
  border: (theme) => `1px solid ${theme.palette.divider}`
};

const innerCardSx = {
  p: 1.2,
  border: (theme) => `1px solid ${theme.palette.divider}`,
  bgcolor: (theme) => theme.palette.background.paper
};

const progressSx = { mt: 0.4, height: 9, borderRadius: 999 };
