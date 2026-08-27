import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cashFromSplitInput, paymentAmounts, validPayment, type PaymentMethod } from '../utils/payments'

export default function PaymentSelector({ method, cash, total, onMethod, onCash, disabled = false }: {
  method: PaymentMethod; cash: string; total: number; onMethod: (method: PaymentMethod) => void; onCash: (cash: string) => void; disabled?: boolean
}) {
  const { t } = useTranslation()
  const errorId = useId()
  const [touched, setTouched] = useState(false)
  const [rejected, setRejected] = useState(false)
  const [cardDraft, setCardDraft] = useState<{ value: string; cash: string; total: number } | null>(null)
  const card = cardDraft?.cash === cash && cardDraft.total === total ? cardDraft.value
    : cash === '' ? '' : String(Math.max(0, paymentAmounts('Mixed', total, cash).cardAmount))
  const showError = rejected || ((touched || cash !== '') && !validPayment(method, total, cash))
  const changeAmount = (field: 'cash' | 'card', value: string) => {
    setTouched(true)
    const nextCash = cashFromSplitInput(field, value, total)
    setRejected(nextCash === null)
    if (nextCash === null) return
    setCardDraft(field === 'card' ? { value, cash: nextCash, total } : null)
    onCash(nextCash)
  }
  return <fieldset className="payment-selector" disabled={disabled}>
    <legend>{t('cashier.paymentMethod')}</legend>
    <div className="payment-options">{(['Cash', 'Card', 'Mixed'] as const).map(value =>
      <button type="button" key={value} aria-pressed={method === value} className={method === value ? 'selected' : ''} onClick={() => { setTouched(false); setRejected(false); setCardDraft(null); onMethod(value) }}>{t(`cashier.${value.toLowerCase()}`)}</button>)}</div>
    {method === 'Mixed' && <div className="split-payment-fields">
      <label>{t('orders.cashAmount')}<input type="number" inputMode="decimal" min="0.001" max={total} step="0.001" value={cash} aria-invalid={showError} aria-describedby={showError ? errorId : undefined} onChange={e => changeAmount('cash', e.target.value)} /></label>
      <label>{t('orders.cardAmount')}<input type="number" inputMode="decimal" min="0.001" max={total} step="0.001" value={card} aria-invalid={showError} aria-describedby={showError ? errorId : undefined} onChange={e => changeAmount('card', e.target.value)} /></label>
      {showError && <p id={errorId} role="alert" className="error-text">{t('orders.invalidSplit')}</p>}
    </div>}
  </fieldset>
}
