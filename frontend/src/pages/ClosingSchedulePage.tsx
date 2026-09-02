import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, ClosingScheduleConfigDto, ClosingScheduleExceptionDto } from '../api/types'
import DataTable from '../components/DataTable'
import { DeleteIcon, EditIcon, IconAction } from '../components/TableTools'
import { useToast } from '../components/ToastContext'

const emptyForm = { date: '', overrideCloseTime: '01:00', branchId: '', reason: '' }

export default function ClosingSchedulePage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const [config, setConfig] = useState<ClosingScheduleConfigDto | null>(null)
  const [exceptions, setExceptions] = useState<ClosingScheduleExceptionDto[]>([])
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [defaultTime, setDefaultTime] = useState('23:45')
  const [active, setActive] = useState(true)
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const load = async () => {
    const [configData, exceptionData, branchData] = await Promise.all([
      api.get<ClosingScheduleConfigDto>('/api/closing-schedule/config'),
      api.get<ClosingScheduleExceptionDto[]>('/api/closing-schedule/exceptions'),
      api.get<BranchDto[]>('/api/branches'),
    ])
    setConfig(configData)
    setDefaultTime(configData.defaultCloseTime.slice(0, 5))
    setActive(configData.isActive)
    setExceptions(exceptionData)
    setBranches(branchData.filter((branch) => branch.isActive))
  }

  useEffect(() => { load().catch(() => setError(t('closing.loadError'))) }, [t])

  const saveConfig = async (event: FormEvent) => {
    event.preventDefault(); setSaving(true)
    try {
      const result = await api.put<ClosingScheduleConfigDto>('/api/closing-schedule/config', {
        defaultCloseTime: `${defaultTime}:00`, isActive: active,
      })
      setConfig(result)
      toast.success(t('closing.savedConfig'))
    } catch (err) { toast.error(err instanceof ApiError ? err.message : t('closing.saveError')) }
    finally { setSaving(false) }
  }

  const saveException = async (event: FormEvent) => {
    event.preventDefault(); setSaving(true)
    const payload = {
      date: form.date, overrideCloseTime: `${form.overrideCloseTime}:00`,
      branchId: form.branchId || null, reason: form.reason,
    }
    try {
      if (editingId) await api.put(`/api/closing-schedule/exceptions/${editingId}`, payload)
      else await api.post('/api/closing-schedule/exceptions', payload)
      setEditingId(null); setForm(emptyForm); await load()
      toast.success(t('closing.savedException'))
    } catch (err) { toast.error(err instanceof ApiError ? err.message : t('closing.saveError')) }
    finally { setSaving(false) }
  }

  const editException = (item: ClosingScheduleExceptionDto) => {
    setEditingId(item.id)
    setForm({ date: item.date, overrideCloseTime: item.overrideCloseTime.slice(0, 5), branchId: item.branchId ?? '', reason: item.reason })
  }

  const deleteException = async (id: string) => {
    if (!window.confirm(t('closing.confirmDelete'))) return
    try { await api.delete(`/api/closing-schedule/exceptions/${id}`); await load(); toast.success(t('closing.deletedException')) }
    catch { toast.error(t('closing.saveError')) }
  }

  const branchName = (id: string | null) => {
    if (!id) return t('closing.allBranches')
    const branch = branches.find((item) => item.id === id)
    return branch ? (i18n.language === 'ar' ? branch.nameAr : branch.nameEn) : id
  }

  return (
    <section>
      <h1>{t('closing.title')}</h1>
      <p className="text-muted">{t('closing.timezoneHint')}</p>
      {error && <p className="error-text">{error}</p>}

      <form className="ui-card ui-stack" onSubmit={saveConfig}>
        <h2>{t('closing.defaultSchedule')}</h2>
        <div className="settings-form-grid">
          <label className="flex flex-col gap-1 text-muted">{t('closing.defaultTime')}
            <input type="time" required value={defaultTime} onChange={(e) => setDefaultTime(e.target.value)} />
          </label>
          <label className="flex min-h-11 items-center gap-2 text-muted">
            <input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} />
            {t('closing.active')}
          </label>
        </div>
        <button disabled={saving || !config}>{t('closing.saveConfig')}</button>
      </form>

      <form className="ui-card ui-stack" onSubmit={saveException}>
        <h2>{editingId ? t('closing.editException') : t('closing.addException')}</h2>
        <div className="settings-form-grid">
          <label className="flex flex-col gap-1 text-muted">{t('closing.date')}
            <input type="date" required value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} />
          </label>
          <label className="flex flex-col gap-1 text-muted">{t('closing.overrideTime')}
            <input type="time" required value={form.overrideCloseTime} onChange={(e) => setForm({ ...form, overrideCloseTime: e.target.value })} />
          </label>
          <label className="flex flex-col gap-1 text-muted">{t('closing.branch')}
            <select value={form.branchId} onChange={(e) => setForm({ ...form, branchId: e.target.value })}>
              <option value="">{t('closing.allBranches')}</option>
              {branches.map((b) => <option key={b.id} value={b.id}>{i18n.language === 'ar' ? b.nameAr : b.nameEn}</option>)}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-muted">{t('closing.reason')}
            <input required maxLength={500} value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} />
          </label>
        </div>
        <div className="flex gap-2">
          <button disabled={saving}>{t('closing.saveException')}</button>
          {editingId && <button type="button" onClick={() => { setEditingId(null); setForm(emptyForm) }}>{t('closing.cancel')}</button>}
        </div>
      </form>

      <DataTable rows={exceptions} getRowKey={(item) => item.id} queryPrefix="exceptions"
        getSearchText={(item) => `${item.date} ${item.reason} ${branchName(item.branchId)}`}
        columns={[
          { id: 'date', header: t('closing.date'), cell: (item) => item.date, sortValue: (item) => item.date },
          { id: 'time', header: t('closing.overrideTime'), cell: (item) => item.overrideCloseTime.slice(0, 5), sortValue: (item) => item.overrideCloseTime },
          { id: 'branch', header: t('closing.branch'), cell: (item) => branchName(item.branchId), sortValue: (item) => branchName(item.branchId) },
          { id: 'reason', header: t('closing.reason'), cell: (item) => item.reason, sortValue: (item) => item.reason },
          { id: 'actions', header: t('closing.actions'), cell: (item) => <div className="row-actions"><IconAction label={t('closing.edit')} onClick={() => editException(item)}><EditIcon /></IconAction><IconAction label={t('closing.delete')} onClick={() => deleteException(item.id)}><DeleteIcon /></IconAction></div> },
        ]} />
    </section>
  )
}
