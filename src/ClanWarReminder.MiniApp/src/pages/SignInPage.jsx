import { Box, Button, Chip, Paper, Stack, TextField, Typography } from "@mui/material";

export function SignInPage({ playerTag, onPlayerTagChange, onSignIn, busy }) {
  return (
    <Paper
      elevation={0}
      sx={{
        position: "relative",
        overflow: "hidden",
        p: { xs: 1.7, sm: 2.2 },
        border: "1px solid rgba(132,186,217,0.2)",
        backdropFilter: "blur(12px)",
        background:
          "linear-gradient(180deg, rgba(9, 23, 36, 0.94) 0%, rgba(8, 18, 30, 0.88) 100%)"
      }}
    >
      <Box
        sx={{
          position: "absolute",
          inset: 0,
          background:
            "radial-gradient(580px 280px at 12% 0%, rgba(110, 214, 255, 0.18) 0%, transparent 60%), radial-gradient(460px 260px at 100% 10%, rgba(101, 240, 194, 0.14) 0%, transparent 62%)",
          pointerEvents: "none"
        }}
      />
      <Stack spacing={1.5} sx={{ position: "relative" }}>
        <Chip label="Clan War SaaS" color="primary" sx={{ alignSelf: "flex-start" }} />
        <Stack spacing={0.8}>
          <Typography variant="h5">Контроль войны клана в Telegram</Typography>
          <Typography variant="body2" sx={{ color: "#a9cde3", maxWidth: 560 }}>
            Подключите тег игрока и откройте дашборд с участием в КВ, прогнозом очков,
            историей клана и Telegram-напоминаниями.
          </Typography>
        </Stack>
        <Paper
          variant="outlined"
          sx={{
            p: 1.2,
            borderColor: "rgba(146, 204, 234, 0.16)",
            bgcolor: "rgba(7, 18, 29, 0.62)"
          }}
        >
          <Typography variant="subtitle2" sx={{ mb: 0.35 }}>
            Что будет после входа
          </Typography>
          <Typography variant="body2" sx={{ color: "#9ec2da" }}>
            Mini app привяжет ваш тег к Telegram-профилю и откроет персональный доступ к
            клановой статистике.
          </Typography>
        </Paper>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.2}>
          <TextField
            fullWidth
            label="Тег игрока"
            value={playerTag}
            onChange={(e) => onPlayerTagChange(e.target.value)}
            placeholder="#PLAYER"
          />
          <Button
            variant="contained"
            onClick={onSignIn}
            disabled={busy || !playerTag.trim()}
            sx={{ minWidth: { sm: 148 } }}
          >
            Подключить
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
