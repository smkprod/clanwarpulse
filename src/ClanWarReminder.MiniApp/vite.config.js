import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

const embedBuild = process.env.MINIAPP_TARGET === "embed";

export default defineConfig({
  plugins: [react()],
  base: embedBuild ? "/miniapp/" : "/",
  build: {
    outDir: embedBuild
      ? path.resolve(__dirname, "../ClanWarReminder.Api/wwwroot/miniapp")
      : "dist",
    emptyOutDir: true,
    sourcemap: false
  }
});
