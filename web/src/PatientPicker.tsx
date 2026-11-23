import { useState } from "react";
import { api, type Patient } from "./api";

interface Props {
  value: Patient | null;
  onChange: (patient: Patient | null) => void;
  onError: (err: unknown) => void;
}

/** Find a patient by DNI, or register them if they're new. */
export function PatientPicker({ value, onChange, onError }: Props) {
  const [dni, setDni] = useState("");
  const [notFound, setNotFound] = useState(false);
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");

  if (value) {
    return (
      <p className="patient">
        Patient: <strong>{value.fullName}</strong> (DNI {value.dni}){" "}
        <button className="link" onClick={() => onChange(null)}>
          change
        </button>
      </p>
    );
  }

  const search = async () => {
    try {
      const [found] = await api.findPatient(dni.trim());
      if (found) onChange(found);
      else setNotFound(true);
    } catch (err) {
      onError(err);
    }
  };

  const register = async () => {
    try {
      onChange(await api.createPatient({ fullName, dni: dni.trim(), phone: phone || undefined }));
    } catch (err) {
      onError(err);
    }
  };

  return (
    <div className="picker">
      <div className="row">
        <label>
          Patient DNI
          <input
            value={dni}
            inputMode="numeric"
            onChange={(e) => {
              setDni(e.target.value.replace(/\D/g, ""));
              setNotFound(false);
            }}
            onKeyDown={(e) => e.key === "Enter" && dni && search()}
          />
        </label>
        <button onClick={search} disabled={dni.length < 7}>
          Search
        </button>
      </div>
      {notFound && (
        <div className="new-patient">
          <p className="muted">No patient with DNI {dni}. Register them:</p>
          <label>
            Full name
            <input value={fullName} onChange={(e) => setFullName(e.target.value)} />
          </label>
          <label>
            Phone (optional)
            <input value={phone} onChange={(e) => setPhone(e.target.value)} />
          </label>
          <button onClick={register} disabled={fullName.trim().length < 3}>
            Register patient
          </button>
        </div>
      )}
    </div>
  );
}
