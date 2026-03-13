import { Box, Button, Chip, Paper, Stack, TextField, Typography } from "@mui/material";

export function SignInPage({ copy, playerTag, onPlayerTagChange, onSignIn, busy }) {
  return (
    <Paper elevation={0} sx={shellSx}>
      <Box sx={glowSx} />
      <Stack spacing={2.2} sx={{ position: "relative" }}>
        <Chip label={copy.signInBadge} color="primary" sx={{ alignSelf: "flex-start" }} />
        <Stack spacing={1}>
          <Typography variant="h4" sx={{ maxWidth: 660 }}>
            {copy.signInTitle}
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 700 }}>
            {copy.signInText}
          </Typography>
        </Stack>
        <Paper elevation={0} sx={hintSx}>
          <Stack spacing={0.5}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {copy.signInHintTitle}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {copy.signInHintText}
            </Typography>
          </Stack>
        </Paper>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.2}>
          <TextField
            fullWidth
            label={copy.playerTag}
            value={playerTag}
            onChange={(event) => onPlayerTagChange(event.target.value)}
            placeholder="#PLAYER"
          />
          <Button variant="contained" size="large" onClick={onSignIn} disabled={busy || !playerTag.trim()} sx={{ minWidth: { sm: 176 } }}>
            {copy.connect}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}

const shellSx = {
  position: "relative",
  overflow: "hidden",
  p: { xs: 2.2, md: 3 },
  border: (theme) => `1px solid ${theme.palette.divider}`,
  background: (theme) =>
    theme.palette.mode === "dark"
      ? "linear-gradient(145deg, rgba(255,122,89,0.14), rgba(17,34,57,0.84) 42%, rgba(8,17,30,0.96))"
      : "linear-gradient(145deg, rgba(255,122,89,0.13), rgba(255,255,255,0.95) 44%, rgba(245,239,231,0.98))"
};

const glowSx = {
  position: "absolute",
  inset: 0,
  background:
    "radial-gradient(520px 240px at 0% 0%, rgba(255,255,255,0.18) 0%, transparent 62%), radial-gradient(420px 260px at 100% 0%, rgba(35,216,163,0.14) 0%, transparent 62%)",
  pointerEvents: "none"
};

const hintSx = {
  p: 2,
  border: (theme) => `1px solid ${theme.palette.divider}`,
  bgcolor: (theme) => theme.palette.background.paper
};
