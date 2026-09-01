import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
import AppIcon, { type AppIconName } from './AppIcon'

type ToastVariant = 'success' | 'error' | 'info'
interface ToastItem { id: number; variant: ToastVariant; message: string }

interface ToastContextValue {
  success: (message: string) => void
  error: (message: string) => void
  info: (message: string) => void
}

const ToastContext = createContext<ToastContextValue | undefined>(undefined)
const AUTO_DISMISS_MS = 4000
const VARIANT_ICON: Record<ToastVariant, AppIconName> = { success: 'check', error: 'alert', info: 'notifications' }

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([])
  const nextId = useRef(0)

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  const push = useCallback((variant: ToastVariant, message: string) => {
    const id = nextId.current++
    setToasts((current) => [...current, { id, variant, message }])
    window.setTimeout(() => dismiss(id), AUTO_DISMISS_MS)
  }, [dismiss])

  const value = useMemo<ToastContextValue>(() => ({
    success: (message) => push('success', message),
    error: (message) => push('error', message),
    info: (message) => push('info', message),
  }), [push])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toast-viewport" role="region" aria-live="polite">
        {toasts.map((toast) => <div key={toast.id} className={`toast-item toast-${toast.variant}`} role="status">
          <AppIcon className="h-5 w-5 flex-none" name={VARIANT_ICON[toast.variant]} />
          <span className="toast-message">{toast.message}</span>
          <button type="button" className="toast-close" onClick={() => dismiss(toast.id)} aria-label="Dismiss">
            <AppIcon className="h-4 w-4" name="close" />
          </button>
        </div>)}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast() {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used within ToastProvider')
  return context
}
