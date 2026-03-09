import { Button, Paper, Stack, TextField, Typography } from "@mui/material";

export function SignInPage({ playerTag, onPlayerTagChange, onSignIn, busy }) {
  return (
    <Paper elevation={0} sx={{ p: 1.6, border: "1px solid rgba(132,186,217,0.2)", backdropFilter: "blur(6px)", overflow: "hidden" }}>
      <Typography variant="h6">Вход по тегу игрока</Typography>
      <Typography variant="body2" sx={{ color: "#9ec2da", mb: 1.5 }}>
        Введите тег игрока, например #ABC123. После первого входа мини-приложение будет запоминать привязку к вашему Telegram-аккаунту.
      </Typography>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.2}>
        <TextField
          fullWidth
          label="Тег игрока"
          value={playerTag}
          onChange={(e) => onPlayerTagChange(e.target.value)}
          placeholder="#PLAYER"
        />
        <Button variant="contained" onClick={onSignIn} disabled={busy || !playerTag.trim()}>
          Войти
        </Button>
      </Stack>
    </Paper>
  );
}
