import { useTranslation } from 'react-i18next'
import Money from './Money'

import { paymentAmounts, validPayment, type PaymentMethod } from '../utils/payments'

export default function PaymentSelector({ method, cash, total, onMethod, onCash, disabled = false }: {
  method: PaymentMethod; cash: string; total: number; onMethod: (method: PaymentMethod) => void; onCash: (cash: string) => void; disabled?: boolean
}) {
  const { t } = useTranslation()
  return <fieldset className="payment-selector" disabled={disabled}>
    <legend>{t('cashier.paymentMethod')}</legend>
    <div className="payment-options">{(['Cash', 'Card', 'Mixed'] as const).map(value =>
      <button type="button" key={value} aria-pressed={method === value} className={method === value ? 'selected' : ''} onClick={() => onMethod(value)}>{t(`cashier.${value.toLowerCase()}`)}</button>)}</div>
    {method === 'Mixed' && <div className="split-payment-fields">
      <label>{t('orders.cashAmount')}<input type="number" min="0.001" max={total} step="0.001" value={cash} onChange={e => onCash(e.target.value)} /></label>
      <div><span>{t('orders.cardAmount')}</span><Money value={Math.max(0, paymentAmounts(method, total, cash).cardAmount)} /></div>
      {!validPayment(method, total, cash) && <p className="error-text">{t('orders.invalidSplit')}</p>}
    </div>}
  </fieldset>
}
