import { useCallback, useEffect, useState } from "react";
import { api, ApiError, nextWorkday, timeOf, type Appointment, type Doctor, type Patient, type Slot } from "./api";
import { PatientPicker } from "./PatientPicker";

export default function App() {
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [doctorId, setDoctorId] = useState<number | null>(null);
  const [date, setDate] = useState(nextWorkday);
  const [slots, setSlots] = useState<Slot[]>([]);
  const [agenda, setAgenda] = useState<Appointment[]>([]);
  const [selected, setSelected] = useState<Slot | null>(null);
  const [patient, setPatient] = useState<Patient | null>(null);
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);
  // Which doctor/day the lists on screen belong to; while they differ from the selection, we're loading.
  const [loadedKey, setLoadedKey] = useState("");
  const loading = loadedKey !== `${doctorId}|${date}`;

  useEffect(() => {
    api.doctors().then((list) => {
      setDoctors(list);
      setDoctorId((current) => current ?? list[0]?.id ?? null);
    });
  }, []);

  // Bumping `version` reloads the lists for the current doctor and day.
  const [version, setVersion] = useState(0);
  const refresh = useCallback(() => setVersion((v) => v + 1), []);

  useEffect(() => {
    if (!doctorId) return;
    let active = true; // ignore responses that arrive after the user picked another doctor/day
    Promise.all([api.availability(doctorId, date), api.appointments(doctorId, date)]).then(([free, booked]) => {
      if (!active) return;
      setSlots(free);
      setAgenda(booked);
      setLoadedKey(`${doctorId}|${date}`);
    });
    return () => {
      active = false;
    };
  }, [doctorId, date, version]);

  const fail = (err: unknown) =>
    setMessage({ kind: "error", text: err instanceof ApiError ? err.message : "Could not reach the server" });

  const book = async () => {
    if (!doctorId || !selected || !patient) return;
    try {
      const appointment = await api.book(doctorId, patient.id, selected.startsAt, reason);
      setMessage({ kind: "ok", text: `Booked ${patient.fullName} at ${timeOf(appointment.startsAt)}` });
      setSelected(null);
      setPatient(null); // the next booking is usually for someone else
      setReason("");
      refresh();
    } catch (err) {
      fail(err);
      refresh(); // the slot may have been taken by someone else
    }
  };

  const cancel = async (appointment: Appointment) => {
    const why = window.prompt(`Cancel ${appointment.patient.fullName} at ${timeOf(appointment.startsAt)}? Reason:`);
    if (why === null) return;
    try {
      await api.cancel(appointment.id, why);
      setMessage({ kind: "ok", text: "Appointment cancelled" });
      refresh();
    } catch (err) {
      fail(err);
    }
  };

  const doctor = doctors.find((d) => d.id === doctorId);

  return (
    <div className="page">
      <header>
        <h1>Clinic appointments</h1>
        <p>Front desk: book and cancel appointments.</p>
      </header>

      <section className="filters card">
        <label>
          Doctor
          <select
            value={doctorId ?? ""}
            onChange={(e) => {
              setDoctorId(Number(e.target.value));
              setSelected(null);
            }}
          >
            {doctors.map((d) => (
              <option key={d.id} value={d.id}>
                {d.fullName} · {d.specialty}
              </option>
            ))}
          </select>
        </label>
        <label>
          Date
          <input
            type="date"
            value={date}
            onChange={(e) => {
              setDate(e.target.value);
              setSelected(null);
            }}
          />
        </label>
        {doctor && <span className="muted">{doctor.slotMinutes}-minute appointments</span>}
      </section>

      {message && (
        <div className={`message ${message.kind}`} role={message.kind === "error" ? "alert" : "status"}>
          {message.text}
          <button className="link" onClick={() => setMessage(null)} aria-label="Dismiss">
            ×
          </button>
        </div>
      )}

      <div className="columns">
        <section className="card">
          <h2>Free slots</h2>
          {loading ? (
            <p className="muted">Loading…</p>
          ) : slots.length === 0 ? (
            <p className="muted">No free slots on this day.</p>
          ) : (
            <div className="slots">
              {slots.map((s) => (
                <button
                  key={s.startsAt}
                  className={`slot${selected?.startsAt === s.startsAt ? " selected" : ""}`}
                  aria-pressed={selected?.startsAt === s.startsAt}
                  onClick={() => setSelected(s)}
                >
                  {timeOf(s.startsAt)}
                </button>
              ))}
            </div>
          )}

          {selected && (
            <div className="booking">
              <h3>Book {timeOf(selected.startsAt)}–{timeOf(selected.endsAt)}</h3>
              <PatientPicker value={patient} onChange={setPatient} onError={fail} />
              <label>
                Reason (optional)
                <input value={reason} onChange={(e) => setReason(e.target.value)} maxLength={300} />
              </label>
              <button className="primary" disabled={!patient} onClick={book}>
                Confirm booking
              </button>
            </div>
          )}
        </section>

        <section className="card">
          <h2>Agenda</h2>
          {agenda.length === 0 ? (
            <p className="muted">No appointments yet.</p>
          ) : (
            <ul className="agenda">
              {agenda.map((a) => (
                <li key={a.id} className={a.status.toLowerCase()}>
                  <strong>{timeOf(a.startsAt)}</strong>
                  <span>
                    {a.patient.fullName}
                    {a.reason && <small> · {a.reason}</small>}
                    {a.status === "Cancelled" && <small> · cancelled{a.cancellationReason ? `: ${a.cancellationReason}` : ""}</small>}
                  </span>
                  {a.status === "Scheduled" && (
                    <button className="link" onClick={() => cancel(a)}>
                      Cancel
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
