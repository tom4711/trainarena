/**
 * Regenerates docs/screenshots/*.png for the README.
 * Usage:
 *   1. App bauen/starten (z. B. `dotnet run --project src/TrainArena`)
 *   2. Falls `/css/host.css` 404: `cp -a src/TrainArena/wwwroot src/TrainArena/bin/Debug/net10.0/`
 *   3. `cd scripts && npm install && node ../scripts/capture-readme-screenshots.mjs`
 *   Optional: `TRAINARENA_URL=http://127.0.0.1:5175`
 */
import { chromium } from "playwright";
import { mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(root, "..");
const outDir = path.join(repoRoot, "docs", "screenshots");
const baseUrl = (process.env.TRAINARENA_URL || "http://127.0.0.1:5176").replace(/\/$/, "");

async function waitForQuizSelect(page) {
  await page.waitForFunction(() => {
    const sel = document.getElementById("quiz-select");
    if (!sel || sel.disabled) return false;
    return [...sel.options].some((o) => o.value);
  }, { timeout: 30_000 });
}

async function firstQuizValue(page) {
  return page.$eval("#quiz-select", (sel) => {
    const opt = [...sel.options].find((o) => o.value);
    return opt?.value ?? "";
  });
}

async function main() {
  await mkdir(outDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });

  const hostContext = await browser.newContext({
    viewport: { width: 1280, height: 900 },
    deviceScaleFactor: 2,
    reducedMotion: "no-preference",
  });
  const hostPage = await hostContext.newPage();
  await hostPage.addInitScript(() => sessionStorage.clear());
  await hostPage.goto(`${baseUrl}/`, { waitUntil: "networkidle" });
  await waitForQuizSelect(hostPage);
  const quizId = await firstQuizValue(hostPage);
  if (!quizId) throw new Error("No quiz in select");

  await hostPage.selectOption("#quiz-select", quizId);
  await hostPage.screenshot({
    path: path.join(outDir, "01-host-setup-powerups.png"),
    fullPage: true,
  });

  await hostPage.click("#btn-create");
  await hostPage.waitForSelector("#room-code:not(.hidden)", { timeout: 15_000 });
  const roomCode = (await hostPage.textContent("#room-code"))?.trim() ?? "";
  if (!roomCode) throw new Error("Room code missing");

  const mobileContext = await browser.newContext({
    viewport: { width: 390, height: 664 },
    deviceScaleFactor: 3,
    isMobile: true,
    hasTouch: true,
    reducedMotion: "no-preference",
  });
  const playerJoinPage = await mobileContext.newPage();
  await playerJoinPage.goto(`${baseUrl}/player/`, { waitUntil: "networkidle" });
  await playerJoinPage.fill("#code", roomCode);
  await playerJoinPage.fill("#nickname", "Alex");
  await playerJoinPage.screenshot({
    path: path.join(outDir, "03-player-join.png"),
    fullPage: true,
  });
  await playerJoinPage.click("#btn-join");
  await playerJoinPage.waitForSelector("#wait:not(.hidden)", { timeout: 15_000 });

  const player2Page = await mobileContext.newPage();
  await player2Page.goto(`${baseUrl}/player/`, { waitUntil: "networkidle" });
  await player2Page.fill("#code", roomCode);
  await player2Page.fill("#nickname", "Ben");
  await player2Page.click("#btn-join");
  await player2Page.waitForSelector("#wait:not(.hidden)", { timeout: 15_000 });

  await hostPage.waitForSelector("#player-list li", { timeout: 15_000 });
  await hostPage.waitForSelector("#qr-wrap:not(.hidden)", { timeout: 15_000 });
  await hostPage.screenshot({
    path: path.join(outDir, "02-host-lobby.png"),
    fullPage: true,
  });

  await hostPage.click("#btn-start");
  await hostPage.waitForSelector("#live:not(.hidden)", { timeout: 15_000 });
  await hostPage.waitForSelector("#question-text:not(:empty)", { timeout: 15_000 });
  await hostPage.waitForSelector("#arena-actions:not(.hidden)", { timeout: 15_000 });
  await hostPage.waitForTimeout(400);
  await hostPage.screenshot({
    path: path.join(outDir, "04-host-live-question.png"),
    fullPage: false,
  });

  await playerJoinPage.waitForSelector("#question:not(.hidden)", { timeout: 15_000 });
  await playerJoinPage.waitForSelector("#powerups:not(.hidden)", { timeout: 15_000 });
  await playerJoinPage.waitForTimeout(400);
  await playerJoinPage.screenshot({
    path: path.join(outDir, "05-player-question-powerups.png"),
    fullPage: true,
  });

  const shieldBtn = playerJoinPage.locator('button[data-powerup="shield"]');
  await shieldBtn.waitFor({ state: "visible", timeout: 10_000 });
  await shieldBtn.click();
  await playerJoinPage.waitForSelector("#fx-overlay:not(.hidden)", { timeout: 10_000 });
  await playerJoinPage.waitForTimeout(350);
  await playerJoinPage.screenshot({
    path: path.join(outDir, "06-player-shield-fx.png"),
    fullPage: true,
  });

  await browser.close();
  console.log("Wrote screenshots to", outDir);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
