import { useTranslation } from 'react-i18next'
import type { SaleDto } from '../api/types'
import Money from './Money'

interface ReceiptProps {
  sale: SaleDto
  headerText: string | null
  branchName: string
  cashierName: string
}

export default function Receipt({ sale, headerText, branchName, cashierName }: ReceiptProps) {
  const { t, i18n } = useTranslation()
  const subtotal = sale.items.reduce((sum, item) => sum + item.lineTotal, 0)

  return (
    <div className="receipt-print" dir={i18n.language === 'ar' ? 'rtl' : 'ltr'}>
      {headerText && <div className="receipt-header">{headerText.split('\n').map((line, index) => <div key={index}>{line}</div>)}</div>}
      <div className="receipt-divider" />
      <div className="receipt-meta">
        <div><span>{t('receipt.date')}</span><span>{new Date(sale.createdAt).toLocaleString(i18n.language)}</span></div>
        <div><span>{t('cashier.branch')}</span><span>{branchName}</span></div>
        <div><span>{t('receipt.cashier')}</span><span>{cashierName}</span></div>
      </div>
      <div className="receipt-divider" />
      <div className="receipt-items">
        {sale.items.map((item) => <div className="receipt-item" key={item.productId}>
          <div className="receipt-item-name">{item.productNameSnapshot}</div>
          <div className="receipt-item-line"><span>{item.quantity} × <Money value={item.unitPriceSnapshot} /></span><Money value={item.lineTotal} /></div>
        </div>)}
      </div>
      <div className="receipt-divider" />
      <div className="receipt-totals">
        <div><span>{t('receipt.subtotal')}</span><Money value={subtotal} /></div>
        {sale.discountAmount > 0 && <div><span>{t('cashier.discount')}</span><Money value={sale.discountAmount} /></div>}
        <div className="receipt-total-line"><span>{t('cashier.total')}</span><Money value={sale.totalAmount} /></div>
        <div><span>{t('cashier.paymentMethod')}</span><span>{sale.paymentMethod === 'Cash' ? t('cashier.cash') : t('cashier.card')}</span></div>
      </div>
    </div>
  )
}
