export interface Doctor {
  id: number;
  fullName: string;
  specialty: string;
  slotMinutes: number;
}

export interface Patient {
  id: number;
  fullName: string;
  dni: string;
  email: string | null;
  phone: string | null;
}

export interface Slot {
  startsAt: string;
  endsAt: string;
}

export interface Appointment {
  id: number;
  doctor: { id: number; fullName: string };
  specialty: string;
  patient: { id: number; fullName: string };
  startsAt: string;
  endsAt: string;
  status: "Scheduled" | "Cancelled" | "Completed";
  reason: string | null;
  cancellationReason: string | null;
}

/** Error with the server's ProblemDetails message. */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: { "content-type": "application/json", ...init?.headers },
  });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new ApiError(response.status, problemText(body) ?? `Request failed (${response.status})`);
  }
  return response.json() as Promise<T>;
}

/** ProblemDetails → one readable line (validation errors list each field). */
export function problemText(body: unknown): string | null {
  if (!body) return null;
  if (typeof body === "string") return body;
  const problem = body as { detail?: string; title?: string; errors?: Record<string, string[]> };
  if (problem.errors) {
    return Object.values(problem.errors).flat().join(" ");
  }
  return problem.detail ?? problem.title ?? null;
}

export const api = {
  doctors: () => request<Doctor[]>("/doctors"),
  availability: (doctorId: number, date: string) => request<Slot[]>(`/doctors/${doctorId}/availability?date=${date}`),
  findPatient: (dni: string) => request<Patient[]>(`/patients?dni=${encodeURIComponent(dni)}`),
  createPatient: (data: { fullName: string; dni: string; email?: string; phone?: string }) =>
    request<Patient>("/patients", { method: "POST", body: JSON.stringify(data) }),
  appointments: (doctorId: number, date: string) =>
    request<Appointment[]>(`/appointments?doctorId=${doctorId}&date=${date}`),
  book: (doctorId: number, patientId: number, startsAt: string, reason: string) =>
    request<Appointment>("/appointments", {
      method: "POST",
      body: JSON.stringify({ doctorId, patientId, startsAt, reason }),
    }),
  cancel: (id: number, reason: string) =>
    request<Appointment>(`/appointments/${id}/cancel`, { method: "POST", body: JSON.stringify({ reason }) }),
};

/** "2026-11-24T09:30:00" → "09:30" (times are clinic local time, so no time-zone conversion). */
export function timeOf(isoLocal: string): string {
  return isoLocal.slice(11, 16);
}

/** Next weekday as YYYY-MM-DD, starting tomorrow. */
export function nextWorkday(from = new Date()): string {
  const d = new Date(from);
  do {
    d.setDate(d.getDate() + 1);
  } while (d.getDay() === 0 || d.getDay() === 6);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
