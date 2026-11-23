import { defineConfig, devices } from "@playwright/test";

/**
 * Starts the real stack (ASP.NET Core API on a fresh SQLite file with demo doctors, Vite dev server)
 * and drives it in Chromium. Locally: CHROME_PATH=/usr/bin/google-chrome to use an installed Chrome.
 */
export default defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  workers: 1, // one shared database
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: "http://localhost:5173",
    timezoneId: "America/Argentina/Buenos_Aires",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
        launchOptions: process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {},
      },
    },
  ],
  webServer: [
    {
      command: "rm -f e2e.db && dotnet run --no-launch-profile --urls http://localhost:5000",
      cwd: "../src/Clinic.Api",
      env: { ConnectionStrings__Clinic: "Data Source=e2e.db", ASPNETCORE_ENVIRONMENT: "Development" },
      url: "http://localhost:5000/api/doctors",
      timeout: 300_000,
      reuseExistingServer: false,
    },
    {
      command: "npx vite --port 5173 --strictPort",
      cwd: "../web",
      url: "http://localhost:5173",
      timeout: 120_000,
      reuseExistingServer: false,
    },
  ],
});
