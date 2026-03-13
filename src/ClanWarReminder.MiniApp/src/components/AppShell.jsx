import { Box, Button, Container, Paper, Stack, Tab, Tabs, Typography } from "@mui/material";

export function AppShell({ appName, title, subtitle, activeTab, onTabChange, tabs, actions, children }) {
  return (
    <Container maxWidth="lg" sx={{ py: { xs: 2, md: 3 }, pb: { xs: 11, md: 4 } }}>
      <Stack spacing={2.2}>
        <Paper elevation={0} sx={heroSx}>
          <Box sx={heroGlowSx} />
          <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" spacing={2} sx={{ position: "relative" }}>
            <Stack spacing={1}>
              <Typography variant="overline">{appName}</Typography>
              <Stack spacing={0.7}>
                <Typography variant="h4">{title}</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 740, overflowWrap: "anywhere" }}>
                  {subtitle}
                </Typography>
              </Stack>
            </Stack>
            {actions ? (
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1} useFlexGap flexWrap="wrap">
                {actions.map((action) => (
                  <Button
                    key={action.label}
                    variant={action.variant ?? "contained"}
                    color={action.color ?? "primary"}
                    onClick={action.onClick}
                    disabled={action.disabled}
                  >
                    {action.label}
                  </Button>
                ))}
              </Stack>
            ) : null}
          </Stack>
        </Paper>

        <Paper elevation={0} sx={navSx}>
          <Tabs value={activeTab} onChange={(_, value) => onTabChange(value)} variant="scrollable" allowScrollButtonsMobile>
            {tabs.map((tab) => (
              <Tab key={tab.value} value={tab.value} label={tab.label} sx={{ minHeight: 48 }} />
            ))}
          </Tabs>
        </Paper>

        <Box>{children}</Box>
      </Stack>

      <Paper elevation={0} sx={mobileNavSx}>
        <Stack direction="row" spacing={0.8}>
          {tabs.map((tab) => (
            <Button
              key={`mobile-${tab.value}`}
              fullWidth
              size="small"
              variant={activeTab === tab.value ? "contained" : "text"}
              onClick={() => onTabChange(tab.value)}
            >
              {tab.label}
            </Button>
          ))}
        </Stack>
      </Paper>
    </Container>
  );
}

const heroSx = {
  position: "relative",
  overflow: "hidden",
  p: { xs: 2, md: 2.6 },
  border: (theme) => `1px solid ${theme.palette.divider}`,
  background: (theme) =>
    theme.palette.mode === "dark"
      ? "linear-gradient(145deg, rgba(255,122,89,0.14), rgba(17,34,57,0.84) 38%, rgba(9,19,34,0.92))"
      : "linear-gradient(145deg, rgba(255,122,89,0.13), rgba(255,255,255,0.92) 34%, rgba(245,239,231,0.96))"
};

const heroGlowSx = {
  position: "absolute",
  inset: 0,
  background:
    "radial-gradient(440px 220px at 0% 0%, rgba(255,255,255,0.18) 0%, transparent 62%), radial-gradient(520px 260px at 100% 0%, rgba(35,216,163,0.16) 0%, transparent 60%)",
  pointerEvents: "none"
};

const navSx = {
  border: (theme) => `1px solid ${theme.palette.divider}`,
  p: 0.8,
  overflow: "hidden"
};

const mobileNavSx = {
  position: "fixed",
  left: 12,
  right: 12,
  bottom: 12,
  zIndex: 20,
  display: { xs: "block", md: "none" },
  border: (theme) => `1px solid ${theme.palette.divider}`,
  px: 0.8,
  py: 0.8
};
