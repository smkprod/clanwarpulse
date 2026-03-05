import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  base: "/miniapp/",
  build: {
    outDir: path.resolve(__dirname, "../ClanWarReminder.Api/wwwroot/miniapp"),
    emptyOutDir: true,
    sourcemap: false
  }
});
