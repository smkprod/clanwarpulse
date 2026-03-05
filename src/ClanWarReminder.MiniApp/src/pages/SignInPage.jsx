import { Button, Paper, Stack, TextField, Typography } from "@mui/material";

export function SignInPage({ playerTag, onPlayerTagChange, onSignIn, busy }) {
  return (
    <Paper elevation={0} sx={{ p: 1.6, border: "1px solid rgba(132,186,217,0.2)", backdropFilter: "blur(6px)", overflow: "hidden" }}>
      <Typography variant="h6">Sign in with player tag</Typography>
      <Typography variant="body2" sx={{ color: "#9ec2da", mb: 1.5 }}>
        Enter player tag (for example #ABC123). In Telegram mini app, your account will be linked automatically for mentions in clan chat.
      </Typography>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.2}>
        <TextField
          fullWidth
          label="Player Tag"
          value={playerTag}
          onChange={(e) => onPlayerTagChange(e.target.value)}
          placeholder="#PLAYER"
        />
        <Button variant="contained" onClick={onSignIn} disabled={busy || !playerTag.trim()}>
          Sign in
        </Button>
      </Stack>
    </Paper>
  );
}
