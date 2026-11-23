import { expect, type Page } from "@playwright/test";

/** YYYY-MM-DD of the next given weekday (0 = Sunday … 6 = Saturday), at least one day ahead. */
export function next(weekday: number, from = new Date()): string {
  const d = new Date(from);
  do d.setDate(d.getDate() + 1);
  while (d.getDay() !== weekday);
  // Local date parts, not toISOString(): late at night the UTC date is already tomorrow.
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

export async function open(page: Page, doctor: string, date: string) {
  await page.goto("/");
  const select = page.getByLabel("Doctor");
  const option = select.locator("option", { hasText: doctor });
  await expect(option).toHaveCount(1); // doctors are loaded
  await select.selectOption((await option.getAttribute("value"))!);
  await page.getByLabel("Date").fill(date);
}

export const freeSlots = (page: Page) => page.locator(".slots button");
export const slot = (page: Page, time: string) => page.locator(".slots").getByRole("button", { name: time, exact: true });
export const agendaItem = (page: Page, text: string) => page.locator(".agenda li").filter({ hasText: text });

export async function pickPatient(page: Page, dni: string) {
  await page.getByLabel("Patient DNI").fill(dni);
  await page.getByRole("button", { name: "Search" }).click();
}

export async function book(page: Page, time: string, dni: string, reason = "") {
  await slot(page, time).click();
  await expect(page.getByRole("heading", { name: new RegExp(`^Book ${time}`) })).toBeVisible();
  await pickPatient(page, dni);
  await expect(page.getByText("Patient:")).toBeVisible();
  if (reason) await page.getByLabel("Reason (optional)").fill(reason);
  await page.getByRole("button", { name: "Confirm booking" }).click();
}
