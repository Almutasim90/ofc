import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { ApiError } from '../api/client'
import ThemeToggle from '../components/ThemeToggle'

// سماكة خط ثابتة لكل الأيقونات بالشاشة — حسب Design System Rules (بند 2)
const ICON_STROKE = 1.75

export default function LoginPage() {
  const { t } = useTranslation()
  const { login } = useAuth()
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login(username.trim(), password)
      navigate('/')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('login.error'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-minimal-page">
      <div className="login-theme-toggle"><ThemeToggle /></div>

      <section className="login-minimal-card" aria-labelledby="login-brand-name">
        <div className="login-minimal-brand">
          <span className="login-minimal-logo" aria-hidden="true">O</span>
          <strong id="login-brand-name">{t('login.brandName')}</strong>
        </div>

        <form className="login-minimal-form" onSubmit={onSubmit}>
          <div className="login-minimal-fields">

            <div className="login-field-row">
              <label className="login-minimal-field login-minimal-password">
                <span className="sr-only">{t('login.username')}</span>
                <input name="login-user-entry" value={username} onChange={(e) => setUsername(e.target.value)} required autoFocus autoComplete="off" autoCapitalize="none" autoCorrect="off" spellCheck={false} inputMode="text" data-1p-ignore data-lpignore="true" data-form-type="other" placeholder={t('login.username')} />
              </label>
            </div>

            <div className="login-field-row">
              <label className="login-minimal-field">
                <span className="sr-only">{t('login.password')}</span>
                <input name="login-secret-entry" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="new-password" autoCapitalize="none" autoCorrect="off" spellCheck={false} data-1p-ignore data-lpignore="true" data-form-type="other" placeholder={t('login.password')} />
              </label>
            </div>
          </div>

          {error && <p className="login-error" role="alert">{error}</p>}

          {/* حالة التحميل: مؤشر دوّار بدل تغيير النص — الشاشة كاملة تُحجب أثناء الإرسال
              (الاستثناء الوحيد المسموح بمؤشر دوّار عام حسب Design System Rules بند 5) */}
          <button className="login-minimal-submit" type="submit" disabled={submitting} aria-busy={submitting}>
            {submitting ? <SpinnerIcon className="login-submit-spinner" /> : t('login.submit')}
          </button>
        </form>
      </section>
    </div>
  )
}

// كل الأيقونات: viewBox موحّد 24×24، حجم مضبوط عبر className (login-field-icon = 20px حسب المقاس القياسي
// لحقول الإدخال والأزرار — بند 2 بالـ Design System)، وسماكة خط ثابتة ICON_STROKE بلا استثناء.

function SpinnerIcon({ className }: { className?: string }) {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" className={className}>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth={ICON_STROKE} opacity="0.25" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth={ICON_STROKE} strokeLinecap="round" />
    </svg>
  )
}
