import { alpha, createTheme } from "@mui/material";

const accent = {
  coral: "#ff7a59",
  mint: "#23d8a3",
  blue: "#3e7bff",
  gold: "#f7b538"
};

export function buildAppTheme(mode) {
  const isDark = mode === "dark";

  return createTheme({
    palette: {
      mode,
      primary: { main: isDark ? accent.coral : "#ff6b43" },
      secondary: { main: isDark ? accent.mint : "#0fb789" },
      warning: { main: accent.gold },
      info: { main: isDark ? "#73a8ff" : accent.blue },
      success: { main: isDark ? "#4ce0a4" : "#17b67d" },
      background: {
        default: isDark ? "#07111d" : "#f7f3ee",
        paper: isDark ? alpha("#112239", 0.78) : alpha("#ffffff", 0.86)
      },
      text: {
        primary: isDark ? "#f7f7fb" : "#162033",
        secondary: isDark ? "#9db2cf" : "#5f6d84"
      },
      divider: isDark ? alpha("#92b4de", 0.18) : alpha("#203356", 0.12)
    },
    shape: { borderRadius: 24 },
    typography: {
      fontFamily: '"Manrope", sans-serif',
      h3: { fontFamily: '"Sora", sans-serif', fontWeight: 700, letterSpacing: "-0.03em" },
      h4: { fontFamily: '"Sora", sans-serif', fontWeight: 700, letterSpacing: "-0.03em" },
      h5: { fontFamily: '"Sora", sans-serif', fontWeight: 700, letterSpacing: "-0.03em" },
      h6: { fontFamily: '"Sora", sans-serif', fontWeight: 700, letterSpacing: "-0.02em" },
      button: { fontWeight: 700 }
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          html: { height: "100%" },
          body: {
            minHeight: "100%",
            background: isDark
              ? "radial-gradient(1100px 700px at 0% 0%, rgba(255,122,89,0.18) 0%, transparent 54%), radial-gradient(900px 620px at 100% 0%, rgba(35,216,163,0.16) 0%, transparent 56%), linear-gradient(180deg, #07111d 0%, #091423 45%, #0b1627 100%)"
              : "radial-gradient(1200px 820px at 0% 0%, rgba(255,122,89,0.16) 0%, transparent 54%), radial-gradient(900px 700px at 100% 0%, rgba(35,216,163,0.18) 0%, transparent 56%), linear-gradient(180deg, #fbf7f1 0%, #f5efe7 100%)",
            backgroundAttachment: "fixed"
          },
          "#root": { minHeight: "100%" },
          "*": { boxSizing: "border-box" }
        }
      },
      MuiPaper: {
        styleOverrides: {
          root: {
            backgroundImage: "none",
            backdropFilter: "blur(14px)",
            boxShadow: isDark ? "0 24px 60px rgba(2, 8, 15, 0.35)" : "0 24px 60px rgba(34, 47, 78, 0.12)"
          }
        }
      },
      MuiButton: {
        styleOverrides: {
          root: {
            borderRadius: 16,
            paddingInline: 18,
            textTransform: "none"
          }
        }
      },
      MuiChip: {
        styleOverrides: { root: { borderRadius: 999 } }
      },
      MuiOutlinedInput: {
        styleOverrides: { root: { borderRadius: 18 } }
      },
      MuiTabs: {
        styleOverrides: { indicator: { height: 3, borderRadius: 999 } }
      }
    }
  });
}
