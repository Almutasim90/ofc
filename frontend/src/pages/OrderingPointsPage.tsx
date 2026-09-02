import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import QRCode from 'qrcode'
import { api, ApiError } from '../api/client'
import type { BranchDto, BranchFeatureFlagDto, BranchQrScheduleDto, CarPickupBayDto, OrderingPointDto, RestaurantTableDto } from '../api/types'
import { useToast } from '../components/ToastContext'

function QrImage({ pointId, token, label }: { pointId: string; token: string; label: string }) {
  const [src, setSrc] = useState('')
  const origin = (import.meta.env.VITE_PUBLIC_ORDER_URL || location.origin).replace(/\/$/, '')
  const url = `${origin}/t/${pointId}?token=${encodeURIComponent(token)}`
  useEffect(() => { void QRCode.toDataURL(url, { width: 220, margin: 2, errorCorrectionLevel: 'M' }).then(setSrc) }, [url])
  return <div className="grid justify-items-center gap-2">{src && <img src={src} alt={`QR ${label}`} />}<a download={`OFC-${label}.png`} href={src}>{label}</a><small className="max-w-64 break-all text-muted">{url}</small></div>
}

export default function OrderingPointsPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [tables, setTables] = useState<RestaurantTableDto[]>([])
  const [bays, setBays] = useState<CarPickupBayDto[]>([])
  const [points, setPoints] = useState<OrderingPointDto[]>([])
  const [bayLabel, setBayLabel] = useState('')
  const [type, setType] = useState<'TABLE' | 'CAR_BAY'>('TABLE')
  const [linkedId, setLinkedId] = useState('')
  const [qrEnabled, setQrEnabled] = useState(true)
  const [schedules, setSchedules] = useState<BranchQrScheduleDto[]>([])
  const branchName = (branch: BranchDto) => i18n.language === 'ar' ? branch.nameAr : branch.nameEn
  const load = async (id: string) => {
    const [nextTables, nextBays, nextPoints, flags, nextSchedules] = await Promise.all([
      api.get<RestaurantTableDto[]>(`/api/restaurant-catalog/tables?branchId=${id}`),
      api.get<CarPickupBayDto[]>(`/api/ordering-points/bays?branchId=${id}`),
      api.get<OrderingPointDto[]>(`/api/ordering-points?branchId=${id}`),
      api.get<BranchFeatureFlagDto[]>(`/api/restaurant-catalog/branches/${id}/features`),
      api.get<BranchQrScheduleDto[]>(`/api/ordering-points/branches/${id}/schedules`),
    ])
    setTables(nextTables); setBays(nextBays); setPoints(nextPoints); setQrEnabled(flags.find(x => x.featureKey === 'QR_ORDERING')?.isEnabled ?? true); setSchedules(nextSchedules)
  }
  useEffect(() => { void api.get<BranchDto[]>('/api/branches').then(rows => { setBranches(rows); setBranchId(rows[0]?.id ?? '') }) }, [])
  useEffect(() => { if (branchId) void load(branchId) }, [branchId])
  const report = (error: unknown) => toast.error(error instanceof ApiError ? error.message : t('common.saveError'))
  const addBay = async () => { try { await api.post('/api/ordering-points/bays', { branchId, bayLabel, isActive: true }); setBayLabel(''); await load(branchId) } catch (error) { report(error) } }
  const addPoint = async () => { try { await api.post('/api/ordering-points', { branchId, pointType: type, linkedTableId: type === 'TABLE' ? linkedId : null, linkedCarBayId: type === 'CAR_BAY' ? linkedId : null, isActive: true }); setLinkedId(''); await load(branchId); toast.success(t('orderingPoints.created')) } catch (error) { report(error) } }
  const regenerate = async (id: string) => { try { await api.post(`/api/ordering-points/${id}/regenerate`, {}); await load(branchId); toast.success(t('orderingPoints.regenerated')) } catch (error) { report(error) } }
  const close = async (id: string) => { try { await api.post(`/api/ordering-points/sessions/${id}/close`, {}); await load(branchId); toast.success(t('orderingPoints.closed')) } catch (error) { report(error) } }
  const toggleQr = async () => { try { const next = !qrEnabled; await api.put(`/api/restaurant-catalog/branches/${branchId}/features/QR_ORDERING`, { isEnabled: next }); setQrEnabled(next) } catch (error) { report(error) } }
  const schedule = (day: number) => schedules.find(x => x.dayOfWeek === day) ?? { id: '', branchId, dayOfWeek: day, opensAt: '08:00:00', closesAt: '23:00:00', isEnabled: false }
  const updateSchedule = (day: number, patch: Partial<BranchQrScheduleDto>) => setSchedules(current => [...current.filter(x => x.dayOfWeek !== day), { ...schedule(day), ...patch }])
  const saveSchedules = async () => { try { await Promise.all(Array.from({ length: 7 }, (_, day) => { const row = schedule(day); return api.put(`/api/ordering-points/branches/${branchId}/schedules`, { dayOfWeek: day, opensAt: row.opensAt, closesAt: row.closesAt, isEnabled: row.isEnabled }) })); toast.success(t('orderingPoints.scheduleSaved')) } catch (error) { report(error) } }
  const options = type === 'TABLE'
    ? tables.filter(x => x.isActive).map(x => ({ id: x.id, label: x.label }))
    : bays.filter(x => x.isActive).map(x => ({ id: x.id, label: x.bayLabel }))

  return <section><h1>{t('orderingPoints.title')}</h1>
    <div className="settings-card"><div className="table-toolbar"><h2>{t('orderingPoints.availability')}</h2><label className="checkbox-row"><input type="checkbox" checked={qrEnabled} onChange={() => void toggleQr()} />{t('orderingPoints.enabled')}</label></div><div className="grid gap-2 md:grid-cols-2 xl:grid-cols-4">{Array.from({ length: 7 }, (_, day) => { const row = schedule(day); return <div className="rounded-xl border border-border p-3" key={day}><label className="checkbox-row"><input type="checkbox" checked={row.isEnabled} onChange={event => updateSchedule(day, { isEnabled: event.target.checked })} />{t(`orderingPoints.days.${day}`)}</label><div className="mt-2 flex gap-2"><input type="time" value={row.opensAt.slice(0, 5)} onChange={event => updateSchedule(day, { opensAt: `${event.target.value}:00` })} /><input type="time" value={row.closesAt.slice(0, 5)} onChange={event => updateSchedule(day, { closesAt: `${event.target.value}:00` })} /></div></div> })}</div><button className="mt-4" onClick={() => void saveSchedules()}>{t('common.save')}</button></div>
    <div className="settings-card grid gap-4"><select value={branchId} onChange={event => setBranchId(event.target.value)}>{branches.map(branch => <option key={branch.id} value={branch.id}>{branchName(branch)}</option>)}</select>
      <div className="table-toolbar"><input value={bayLabel} onChange={event => setBayLabel(event.target.value)} placeholder={t('orderingPoints.bayLabel')} /><button disabled={!bayLabel} onClick={addBay}>{t('common.add')}</button></div>
      <div className="table-toolbar"><select value={type} onChange={event => { setType(event.target.value as 'TABLE' | 'CAR_BAY'); setLinkedId('') }}><option value="TABLE">{t('orderingPoints.table')}</option><option value="CAR_BAY">{t('orderingPoints.carBay')}</option></select><select value={linkedId} onChange={event => setLinkedId(event.target.value)}><option value="">—</option>{options.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select><button disabled={!linkedId} onClick={addPoint}>{t('orderingPoints.addPoint')}</button></div>
    </div>
    <h2>{t('orderingPoints.codes')}</h2><div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">{points.map(point => <article className="settings-card" key={point.id}><QrImage pointId={point.id} token={point.qrToken} label={point.label} /><div className="modal-actions"><button className="button-secondary" onClick={() => regenerate(point.id)}>{t('orderingPoints.regenerate')}</button>{point.activeSessionId && <button onClick={() => close(point.activeSessionId!)}>{t('orderingPoints.closeSession')}</button>}</div></article>)}</div>
  </section>
}
