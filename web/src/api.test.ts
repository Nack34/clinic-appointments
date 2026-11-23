import { describe, expect, it } from "vitest";
import { nextWorkday, problemText, timeOf } from "./api";

describe("problemText", () => {
  it("joins validation errors from ProblemDetails", () => {
    expect(problemText({ title: "One or more validation errors occurred.", errors: { Dni: ["DNI must have 7 or 8 digits"] } })).toBe(
      "DNI must have 7 or 8 digits",
    );
  });

  it("uses the detail of a business-rule error", () => {
    expect(problemText({ status: 409, detail: "That slot is already booked" })).toBe("That slot is already booked");
  });

  it("accepts plain strings and empty bodies", () => {
    expect(problemText("A patient with that DNI already exists")).toBe("A patient with that DNI already exists");
    expect(problemText(null)).toBeNull();
  });
});

describe("dates", () => {
  it("reads the local time without time-zone conversion", () => {
    expect(timeOf("2026-11-24T09:30:00")).toBe("09:30");
  });

  it("skips weekends", () => {
    expect(nextWorkday(new Date(2026, 10, 20))).toBe("2026-11-23"); // Friday → Monday
    expect(nextWorkday(new Date(2026, 10, 23))).toBe("2026-11-24"); // Monday → Tuesday
  });
});
