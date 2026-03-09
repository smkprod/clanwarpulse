import { createTheme } from "@mui/material";

export const appTheme = createTheme({
  palette: {
    mode: "dark",
    primary: { main: "#6ed6ff" },
    secondary: { main: "#65f0c2" },
    warning: { main: "#ffcf6b" },
    background: {
      default: "transparent",
      paper: "rgba(8, 20, 32, 0.82)"
    },
    divider: "rgba(146, 204, 234, 0.14)"
  },
  shape: { borderRadius: 18 },
  typography: {
    fontFamily: "Manrope, sans-serif",
    h4: { fontWeight: 800, letterSpacing: "0.01em" },
    h5: { fontWeight: 800, letterSpacing: "-0.01em" },
    h6: { fontWeight: 700 }
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        html: { height: "100%" },
        body: {
          margin: 0,
          minHeight: "100%",
          background:
            "radial-gradient(1100px 760px at 10% 0%, rgba(62, 128, 196, 0.36) 0%, transparent 58%), radial-gradient(900px 600px at 100% 18%, rgba(33, 118, 151, 0.26) 0%, transparent 56%), linear-gradient(180deg, #05101a 0%, #081523 48%, #07111a 100%)"
        },
        "#root": { minHeight: "100%" }
      }
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: "none",
          boxShadow: "0 18px 45px rgba(2, 10, 18, 0.26)"
        }
      }
    },
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 14,
          fontWeight: 700,
          textTransform: "none"
        }
      }
    },
    MuiBottomNavigation: {
      styleOverrides: {
        root: {
          background: "rgba(8, 20, 32, 0.9)",
          backdropFilter: "blur(12px)",
          borderTop: "1px solid rgba(146, 204, 234, 0.14)"
        }
      }
    }
  }
});
