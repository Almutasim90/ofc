import { useState, type ChangeEventHandler, type ReactNode } from 'react'

interface PasswordFieldProps {
  label: string
  value: string
  onChange: ChangeEventHandler<HTMLInputElement>
  showLabel: string
  hideLabel: string
  required?: boolean
  minLength?: number
  placeholder?: string
  hint?: ReactNode
  autoComplete?: string
}

export default function PasswordField({
  label,
  value,
  onChange,
  showLabel,
  hideLabel,
  required,
  minLength,
  placeholder,
  hint,
  autoComplete,
}: PasswordFieldProps) {
  const [visible, setVisible] = useState(false)

  return (
    <label>
      {label}
      <span className="password-input-wrap">
        <input
          type={visible ? 'text' : 'password'}
          value={value}
          onChange={onChange}
          required={required}
          minLength={minLength}
          placeholder={placeholder}
          autoComplete={autoComplete}
        />
        <button
          type="button"
          className="password-toggle"
          onClick={() => setVisible((current) => !current)}
          aria-label={visible ? hideLabel : showLabel}
          title={visible ? hideLabel : showLabel}
        >
          {visible ? <EyeOffIcon /> : <EyeIcon />}
        </button>
      </span>
      {hint && <small>{hint}</small>}
    </label>
  )
}

function EyeIcon() {
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6S2.5 12 2.5 12Z"/><circle cx="12" cy="12" r="2.5"/></svg>
}

function EyeOffIcon() {
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="m3 3 18 18M10.6 6.1A10.8 10.8 0 0 1 12 6c6 0 9.5 6 9.5 6a16 16 0 0 1-2.3 3.1M6.3 6.3C3.8 8 2.5 12 2.5 12s3.5 6 9.5 6a9.8 9.8 0 0 0 3.2-.5M10.2 10.2a2.5 2.5 0 0 0 3.6 3.6"/></svg>
}
