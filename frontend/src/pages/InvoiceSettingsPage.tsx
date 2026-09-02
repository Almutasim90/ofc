import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto } from '../api/types'
import { useToast } from '../components/ToastContext'

interface InvoiceSettings {
  branchId: string
  legalNameAr: string
  legalNameEn: string
  taxRegistrationNumber: string | null
  commercialRegistrationNumber: string | null
  addressAr: string | null
  addressEn: string | null
  phone: string | null
  currency: string
  pricesIncludeTax: boolean
  defaultTaxRate: number
  footer: string | null
}

const empty = (branchId = ''): InvoiceSettings => ({ branchId, legalNameAr: '', legalNameEn: '', taxRegistrationNumber: '', commercialRegistrationNumber: '', addressAr: '', addressEn: '', phone: '', currency: 'OMR', pricesIncludeTax: false, defaultTaxRate: 0, footer: '' })

export default function InvoiceSettingsPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [value, setValue] = useState<InvoiceSettings>(empty())
  const [busy, setBusy] = useState(false)
  const branchName = (branch: BranchDto) => i18n.language === 'ar' ? branch.nameAr : branch.nameEn

  useEffect(() => { void api.get<BranchDto[]>('/api/branches').then(rows => { setBranches(rows); if (rows[0]) setValue(empty(rows[0].id)) }) }, [])
  useEffect(() => {
    if (!value.branchId) return
    const requestedBranch = value.branchId
    let active = true
    void api.get<InvoiceSettings>(`/api/invoice-settings?branchId=${requestedBranch}`)
      .then(result => { if (active && result.branchId === requestedBranch) setValue(result) })
      .catch(error => { if (active) toast.error(error instanceof ApiError ? error.message : t('invoiceSettings.loadError')) })
    return () => { active = false }
  }, [value.branchId, t, toast])
  const set = <K extends keyof InvoiceSettings>(key: K, next: InvoiceSettings[K]) => setValue(current => ({ ...current, [key]: next }))
  const save = async (event: FormEvent) => { event.preventDefault(); setBusy(true); try { setValue(await api.put<InvoiceSettings>('/api/invoice-settings', value)); toast.success(t('invoiceSettings.saved')) } catch (error) { toast.error(error instanceof ApiError ? error.message : t('common.saveError')) } finally { setBusy(false) } }

  return <section><h1>{t('invoiceSettings.title')}</h1><p>{t('invoiceSettings.description')}</p><form className="settings-card grid gap-4" onSubmit={save}>
    <label>{t('restaurant.branch')}<select value={value.branchId} onChange={event => setValue(empty(event.target.value))}>{branches.map(branch => <option key={branch.id} value={branch.id}>{branchName(branch)}</option>)}</select></label>
    <div className="grid gap-4 md:grid-cols-2"><label>{t('invoiceSettings.legalNameAr')}<input required maxLength={200} value={value.legalNameAr} onChange={event => set('legalNameAr', event.target.value)} /></label><label>{t('invoiceSettings.legalNameEn')}<input required maxLength={200} value={value.legalNameEn} onChange={event => set('legalNameEn', event.target.value)} /></label><label>{t('invoiceSettings.taxNumber')}<input maxLength={100} value={value.taxRegistrationNumber ?? ''} onChange={event => set('taxRegistrationNumber', event.target.value)} /></label><label>{t('invoiceSettings.crNumber')}<input maxLength={100} value={value.commercialRegistrationNumber ?? ''} onChange={event => set('commercialRegistrationNumber', event.target.value)} /></label><label>{t('invoiceSettings.addressAr')}<textarea maxLength={500} value={value.addressAr ?? ''} onChange={event => set('addressAr', event.target.value)} /></label><label>{t('invoiceSettings.addressEn')}<textarea maxLength={500} value={value.addressEn ?? ''} onChange={event => set('addressEn', event.target.value)} /></label><label>{t('invoiceSettings.phone')}<input maxLength={50} value={value.phone ?? ''} onChange={event => set('phone', event.target.value)} /></label><label>{t('invoiceSettings.currency')}<input required maxLength={3} value={value.currency} onChange={event => set('currency', event.target.value.toUpperCase())} /></label><label>{t('invoiceSettings.taxRate')}<input type="number" min="0" max="100" step="0.001" value={value.defaultTaxRate} onChange={event => set('defaultTaxRate', Number(event.target.value))} /></label><label className="checkbox-row"><input type="checkbox" checked={value.pricesIncludeTax} onChange={event => set('pricesIncludeTax', event.target.checked)} />{t('invoiceSettings.inclusive')}</label></div>
    <label>{t('invoiceSettings.footer')}<textarea maxLength={1000} value={value.footer ?? ''} onChange={event => set('footer', event.target.value)} /></label><button disabled={busy || !value.branchId}>{busy ? t('common.loading') : t('common.save')}</button>
  </form></section>
}
