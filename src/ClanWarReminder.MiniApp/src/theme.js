import { createTheme } from "@mui/material";

export const appTheme = createTheme({
  palette: {
    mode: "dark",
    primary: { main: "#66d3ff" },
    secondary: { main: "#57e3ac" },
    background: {
      default: "transparent",
      paper: "rgba(8, 20, 32, 0.82)"
    }
  },
  shape: { borderRadius: 14 },
  typography: {
    fontFamily: "Manrope, sans-serif",
    h4: { fontWeight: 800, letterSpacing: "0.01em" },
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
            "radial-gradient(1100px 700px at 8% 8%, #1a3f66 0%, transparent 60%), radial-gradient(1000px 600px at 95% 20%, #155f5c 0%, transparent 58%), linear-gradient(170deg, #040a10 0%, #091624 55%, #07131d 100%)"
        },
        "#root": { minHeight: "100%" }
      }
    }
  }
});
