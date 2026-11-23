import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react()],
  // In development the API runs on :5000; the browser calls /api on the same origin.
  server: { proxy: { "/api": "http://localhost:5000" } },
});
