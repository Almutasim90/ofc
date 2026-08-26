import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError, resolveApiAssetUrl } from '../api/client'
import type { ProductChannelPriceDto, ProductDto, SalesChannelDto } from '../api/types'
import { useToast } from '../components/ToastContext'

const emptyForm = { nameAr: '', nameEn: '', logoUrl: '', isActive: true }

export default function ChannelsPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const fileInput = useRef<HTMLInputElement>(null)
  const [channels, setChannels] = useState<SalesChannelDto[]>([])
  const [products, setProducts] = useState<ProductDto[]>([])
  const [selected, setSelected] = useState<SalesChannelDto | null>(null)
  const [pricingChannel, setPricingChannel] = useState<SalesChannelDto | null>(null)
  const [prices, setPrices] = useState<Record<string, string>>({})
  const [form, setForm] = useState(emptyForm)
  const [uploading, setUploading] = useState(false)

  const load = () => Promise.all([api.get<SalesChannelDto[]>('/api/channels'), api.get<ProductDto[]>('/api/products')]).then(([channelRows, productRows]) => { setChannels(channelRows); setProducts(productRows) })
  useEffect(() => { void load() }, [])

  const uploadLogo = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return
    if (file.size > 5 * 1024 * 1024) {
      toast.error(t('channels.logoTooLarge'))
      event.target.value = ''
      return
    }
    setUploading(true)
    try {
      const body = new FormData(); body.append('file', file)
      const result = await api.upload<{ url: string }>('/api/uploads/channel-logo', body)
      const nextForm = { ...form, logoUrl: result.url }
      setForm(nextForm)
      if (selected) {
        await api.put(`/api/channels/${selected.id}`, nextForm)
        setSelected(current => current ? { ...current, logoUrl: result.url } : current)
        await load()
      }
      toast.success(selected ? t('channels.logoSaved') : t('channels.logoUploaded'))
    } catch (error) {
      toast.error(error instanceof ApiError ? `${t('channels.uploadError')} (${error.status}: ${error.message})` : t('channels.uploadError'))
    } finally { setUploading(false); event.target.value = '' }
  }

  const save = async (event: FormEvent) => {
    event.preventDefault()
    try {
      if (selected) await api.put(`/api/channels/${selected.id}`, form); else await api.post('/api/channels', form)
      setSelected(null); setForm(emptyForm); await load()
      toast.success(selected ? t('common.updated') : t('common.created'))
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.saveError'))
    }
  }

  const toggleActive = async (channel: SalesChannelDto) => {
    try {
      await api.put(`/api/channels/${channel.id}`, { nameAr: channel.nameAr, nameEn: channel.nameEn, logoUrl: channel.logoUrl, isActive: !channel.isActive })
      await load()
      toast.success(t('common.updated'))
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.saveError'))
    }
  }

  const deleteChannel = async (channelId: string) => {
    try {
      await api.delete(`/api/channels/${channelId}`)
      await load()
      toast.success(t('common.deleted'))
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.deleteError'))
    }
  }

  const editPrices = async (channel: SalesChannelDto) => {
    setPricingChannel(channel)
    const rows = await api.get<ProductChannelPriceDto[]>(`/api/channels/${channel.id}/prices`)
    setPrices(Object.fromEntries(rows.map(row => [row.productId, row.price?.toString() ?? ''])))
  }

  const savePrices = async () => {
    try {
      if (!pricingChannel) return
      await api.put(`/api/channels/${pricingChannel.id}/prices`, { prices: products.map(product => ({ productId: product.id, price: prices[product.id] ? Number(prices[product.id]) : null })) })
      setPricingChannel(null)
      toast.success(t('common.updated'))
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.saveError'))
    }
  }

  return <section className="channels-page">
    <div className="page-heading"><div><h1>{t('channels.title')}</h1><p>{t('channels.description')}</p></div></div>
    <form className="channel-form-card" onSubmit={save}>
      <div className="channel-form-fields">
        <label>{t('channels.nameAr')}<input required value={form.nameAr} onChange={event => setForm({ ...form, nameAr: event.target.value })} /></label>
        <label>{t('channels.nameEn')}<input required value={form.nameEn} onChange={event => setForm({ ...form, nameEn: event.target.value })} /></label>
        <div className="channel-logo-field"><span className="channel-field-label">{t('channels.logo')}</span><div className="channel-logo-picker">
          <div className="channel-logo-preview-box">{form.logoUrl ? <img src={resolveApiAssetUrl(form.logoUrl)} alt="" /> : <span>{form.nameAr.charAt(0) || '+'}</span>}</div>
          <div><button className="button-secondary channel-upload-button" type="button" disabled={uploading} onClick={() => fileInput.current?.click()}>{uploading ? t('channels.uploading') : t('channels.uploadLogo')}</button><small>{t('channels.logoHint')}</small></div>
          <input ref={fileInput} className="sr-only" type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" onChange={uploadLogo} />
        </div></div>
        <label className="channel-active-toggle"><input type="checkbox" checked={form.isActive} disabled={selected?.isInStore} onChange={event => setForm({ ...form, isActive: event.target.checked })} /><span>{t('channels.active')}</span></label>
      </div>
      <div className="channel-form-actions"><button type="submit" disabled={uploading}>{selected ? t('common.update') : t('common.save')}</button>{selected && <button className="button-secondary" type="button" onClick={() => { setSelected(null); setForm(emptyForm) }}>{t('common.cancel')}</button>}</div>
    </form>
    <div className="table-shell channel-table"><table><thead><tr><th>{t('channels.logo')}</th><th>{t('channels.channel')}</th><th>{t('channels.status')}</th><th>{t('common.actions')}</th></tr></thead><tbody>{channels.map(channel => <tr key={channel.id}>
      <td><div className="channel-table-logo">{channel.logoUrl ? <img src={resolveApiAssetUrl(channel.logoUrl)} alt="" /> : <span>{(i18n.language === 'ar' ? channel.nameAr : channel.nameEn).charAt(0)}</span>}</div></td>
      <td><strong>{i18n.language === 'ar' ? channel.nameAr : channel.nameEn}</strong></td><td><span className={`channel-status ${channel.isActive ? 'is-active' : ''}`}>{channel.isActive ? t('channels.active') : t('channels.inactive')}</span></td>
      <td><div className="channel-row-actions"><button onClick={() => void editPrices(channel)}>{t('channels.pricing')}</button><button className="button-secondary" onClick={() => { setSelected(channel); setForm({ nameAr: channel.nameAr, nameEn: channel.nameEn, logoUrl: channel.logoUrl ?? '', isActive: channel.isActive }); window.scrollTo({ top: 0, behavior: 'smooth' }) }}>{t('common.edit')}</button>{!channel.isInStore && <button className="button-secondary" onClick={() => toggleActive(channel)}>{channel.isActive ? t('channels.stop') : t('channels.enable')}</button>}{!channel.isInStore && <button className="button-danger" onClick={() => deleteChannel(channel.id)}>{t('common.delete')}</button>}</div></td>
    </tr>)}</tbody></table></div>
    {pricingChannel && <div className="channel-pricing-card"><div className="channel-pricing-heading"><h2>{t('channels.pricing')}: {i18n.language === 'ar' ? pricingChannel.nameAr : pricingChannel.nameEn}</h2><button className="button-secondary" onClick={() => setPricingChannel(null)}>{t('common.cancel')}</button></div><div className="settings-card-grid">{products.map(product => <label key={product.id}>{i18n.language === 'ar' ? product.nameAr : product.nameEn}<input type="number" step="0.001" placeholder={product.price.toString()} value={prices[product.id] ?? ''} onChange={event => setPrices({ ...prices, [product.id]: event.target.value })} /></label>)}</div><button onClick={savePrices}>{t('common.save')}</button></div>}
  </section>
}
