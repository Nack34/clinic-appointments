import { expect, test } from "@playwright/test";
import { agendaItem, book, freeSlots, next, open, pickPatient, slot } from "./helpers";

const MONDAY = next(1);
const WEDNESDAY = next(3);

test("free slots follow each doctor's schedule and slot length", async ({ page }) => {
  await open(page, "Laura Gómez", MONDAY);
  await expect(page.getByText("30-minute appointments")).toBeVisible();
  await expect(freeSlots(page).first()).toHaveText("09:00");
  await expect(slot(page, "13:00")).toHaveCount(0); // lunch break
  await expect(freeSlots(page)).toHaveCount(14); // 9–13 and 14–17 in 30-minute slots

  await open(page, "Martín Álvarez", MONDAY);
  await expect(page.getByText("20-minute appointments")).toBeVisible();
  await expect(freeSlots(page)).toHaveCount(12); // 8–12 in 20-minute slots

  await open(page, "Sofía Romero", MONDAY); // only works Tuesdays and Thursdays
  await expect(page.getByText("No free slots on this day.")).toBeVisible();
});

test("book an existing patient found by DNI", async ({ page }) => {
  await open(page, "Laura Gómez", MONDAY);
  await book(page, "09:00", "30111222", "Control anual");
  await expect(page.getByRole("status")).toHaveText(/Booked Juan Pérez at 09:00/);
  await expect(slot(page, "09:00")).toHaveCount(0);
  await expect(agendaItem(page, "Juan Pérez")).toContainText("09:00");
  await expect(agendaItem(page, "Juan Pérez")).toContainText("Control anual");
});

test("register a new patient while booking", async ({ page }) => {
  const dni = String(40_000_000 + (Date.now() % 1_000_000));
  await open(page, "Laura Gómez", MONDAY);
  await slot(page, "09:30").click();
  await pickPatient(page, dni);
  await expect(page.getByText(`No patient with DNI ${dni}`)).toBeVisible();
  await page.getByLabel("Full name").fill("Lucía Benítez");
  await page.getByLabel("Phone (optional)").fill("+54 221 555-0199");
  await page.getByRole("button", { name: "Register patient" }).click();
  await expect(page.getByText("Patient:")).toContainText("Lucía Benítez");
  await page.getByRole("button", { name: "Confirm booking" }).click();
  await expect(agendaItem(page, "Lucía Benítez")).toContainText("09:30");
});

test("cancelling frees the slot again and keeps the reason in the agenda", async ({ page }) => {
  await open(page, "Martín Álvarez", WEDNESDAY);
  await book(page, "08:20", "28999888");
  await expect(agendaItem(page, "María Fernández")).toBeVisible();
  await expect(slot(page, "08:20")).toHaveCount(0);

  page.once("dialog", (dialog) => dialog.accept("Patient has a fever, will call back"));
  await agendaItem(page, "María Fernández").getByRole("button", { name: "Cancel" }).click();
  await expect(page.getByRole("status")).toHaveText(/Appointment cancelled/);
  await expect(agendaItem(page, "María Fernández")).toContainText("cancelled: Patient has a fever, will call back");
  await expect(slot(page, "08:20")).toBeVisible();
});

test("two receptionists racing for the same slot: one wins, the other gets a clear error", async ({ browser }) => {
  const desk1 = await (await browser.newContext()).newPage();
  const desk2 = await (await browser.newContext()).newPage();
  for (const desk of [desk1, desk2]) {
    await open(desk, "Laura Gómez", WEDNESDAY);
    await slot(desk, "10:00").click();
  }
  await pickPatient(desk1, "30111222");
  await pickPatient(desk2, "28999888");
  await desk1.getByRole("button", { name: "Confirm booking" }).click();
  await expect(desk1.getByRole("status")).toHaveText(/Booked Juan Pérez at 10:00/);

  await desk2.getByRole("button", { name: "Confirm booking" }).click();
  await expect(desk2.getByRole("alert")).toContainText(/already booked/i);
  await expect(slot(desk2, "10:00")).toHaveCount(0); // desk 2's list refreshed
  await expect(agendaItem(desk2, "Juan Pérez")).toContainText("10:00");
});
