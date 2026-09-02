import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, RestaurantFloorDto, TableStatusDto } from '../api/types'
import Money from '../components/Money'
import { useToast } from '../components/ToastContext'

type TableDraft = {
  id: string | null
  label: string
  capacity: string
  positionX: number
  positionY: number
  shape: 'Rectangle' | 'Round'
  isActive: boolean
}

const emptyTable = (): TableDraft => ({ id: null, label: '', capacity: '4', positionX: 50, positionY: 50, shape: 'Rectangle', isActive: true })

export default function TablesPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [floors, setFloors] = useState<RestaurantFloorDto[]>([])
  const [floorId, setFloorId] = useState('')
  const [tables, setTables] = useState<TableStatusDto[]>([])
  const [floorName, setFloorName] = useState('')
  const [draft, setDraft] = useState<TableDraft>(emptyTable)
  const [loading, setLoading] = useState(false)

  const report = (error: unknown) => toast.error(error instanceof ApiError ? error.message : t('common.saveError'))
  const branchName = (branch: BranchDto) => i18n.language === 'ar' ? branch.nameAr : branch.nameEn
  const selectedFloor = floors.find(floor => floor.id === floorId)

  const loadFloors = async (selectedBranch: string, preferredFloor?: string) => {
    const rows = await api.get<RestaurantFloorDto[]>(`/api/tables/floors?branchId=${selectedBranch}`)
    setFloors(rows)
    const nextFloor = preferredFloor && rows.some(x => x.id === preferredFloor) ? preferredFloor : rows.find(x => x.isActive)?.id ?? rows[0]?.id ?? ''
    setFloorId(nextFloor)
    return nextFloor
  }

  const loadBoard = async (selectedBranch: string, selectedFloor: string) => {
    if (!selectedBranch || !selectedFloor) { setTables([]); return }
    setLoading(true)
    try { setTables(await api.get<TableStatusDto[]>(`/api/tables/board?branchId=${selectedBranch}&floorId=${selectedFloor}`)) }
    catch (error) { report(error) }
    finally { setLoading(false) }
  }

  useEffect(() => {
    void api.get<BranchDto[]>('/api/branches').then(rows => { setBranches(rows); setBranchId(rows[0]?.id ?? '') })
      .catch(error => toast.error(error instanceof ApiError ? error.message : t('common.saveError')))
  }, [t, toast])

  useEffect(() => {
    if (!branchId) return
    setDraft(emptyTable())
    let active = true
    void api.get<RestaurantFloorDto[]>(`/api/tables/floors?branchId=${branchId}`).then(rows => {
      if (active) { setFloors(rows); setFloorId(rows.find(x => x.isActive)?.id ?? rows[0]?.id ?? '') }
    }).catch(error => { if (active) toast.error(error instanceof ApiError ? error.message : t('common.saveError')) })
    return () => { active = false }
  }, [branchId, t, toast])

  useEffect(() => {
    if (!branchId || !floorId) { setTables([]); return }
    let active = true
    const refresh = async () => {
      setLoading(true)
      try { const rows = await api.get<TableStatusDto[]>(`/api/tables/board?branchId=${branchId}&floorId=${floorId}`); if (active) setTables(rows) }
      catch (error) { if (active) toast.error(error instanceof ApiError ? error.message : t('common.saveError')) }
      finally { if (active) setLoading(false) }
    }
    void refresh()
    const timer = window.setInterval(() => { void refresh() }, 15000)
    return () => { active = false; window.clearInterval(timer) }
  }, [branchId, floorId, t, toast])

  const createFloor = async () => {
    if (!floorName.trim()) return
    try {
      const floor = await api.post<RestaurantFloorDto>('/api/tables/floors', { branchId, name: floorName, sortOrder: floors.length, isActive: true })
      setFloorName('')
      await loadFloors(branchId, floor.id)
      toast.success(t('common.created'))
    } catch (error) { report(error) }
  }

  const updateFloor = async (patch: Partial<Pick<RestaurantFloorDto, 'name' | 'sortOrder' | 'isActive'>>) => {
    if (!selectedFloor) return
    try {
      await api.put(`/api/tables/floors/${selectedFloor.id}`, { branchId, name: patch.name ?? selectedFloor.name, sortOrder: patch.sortOrder ?? selectedFloor.sortOrder, isActive: patch.isActive ?? selectedFloor.isActive })
      await loadFloors(branchId, selectedFloor.id)
      toast.success(t('common.updated'))
    } catch (error) { report(error) }
  }

  const deleteFloor = async () => {
    if (!selectedFloor || !window.confirm(t('tables.deleteFloorConfirm'))) return
    try { await api.delete(`/api/tables/floors/${selectedFloor.id}`); await loadFloors(branchId); toast.success(t('common.deleted')) }
    catch (error) { report(error) }
  }

  const editTable = (table: TableStatusDto) => setDraft({ id: table.id, label: table.label, capacity: table.capacity?.toString() ?? '', positionX: table.positionX, positionY: table.positionY, shape: table.shape, isActive: table.isActive })
  const tablePayload = (value: TableDraft) => ({ branchId, label: value.label, capacity: value.capacity ? Number(value.capacity) : null, floorId, positionX: value.positionX, positionY: value.positionY, shape: value.shape, isActive: value.isActive })

  const saveTable = async () => {
    if (!draft.label.trim() || !floorId) return
    try {
      if (draft.id) await api.put(`/api/tables/${draft.id}`, tablePayload(draft))
      else await api.post('/api/tables', tablePayload(draft))
      setDraft(emptyTable())
      await loadBoard(branchId, floorId)
      toast.success(t(draft.id ? 'common.updated' : 'common.created'))
    } catch (error) { report(error) }
  }

  const moveTable = async (table: TableStatusDto, dx: number, dy: number) => {
    const moved = { ...table, positionX: Math.max(0, Math.min(100, table.positionX + dx)), positionY: Math.max(0, Math.min(100, table.positionY + dy)) }
    setTables(current => current.map(row => row.id === table.id ? moved : row))
    try { await api.put(`/api/tables/${table.id}`, tablePayload({ id: table.id, label: table.label, capacity: table.capacity?.toString() ?? '', positionX: moved.positionX, positionY: moved.positionY, shape: table.shape, isActive: table.isActive })) }
    catch (error) { report(error); await loadBoard(branchId, floorId) }
  }

  return <section className="tables-page">
    <header className="tables-page-header">
      <div><p className="tables-eyebrow">{t('tables.eyebrow')}</p><h1>{t('tables.title')}</h1><p className="text-muted">{t('tables.description')}</p></div>
      <label>{t('tables.branch')}<select value={branchId} onChange={event => setBranchId(event.target.value)}>{branches.map(branch => <option key={branch.id} value={branch.id}>{branchName(branch)}</option>)}</select></label>
    </header>

    <div className="tables-floor-strip">
      <div className="tables-floor-tabs" role="tablist">{floors.map(floor => <button type="button" role="tab" aria-selected={floor.id === floorId} className={floor.id === floorId ? 'is-active' : ''} key={floor.id} onClick={() => { setFloorId(floor.id); setDraft(emptyTable()) }}>{floor.name}{!floor.isActive && <small>{t('common.inactive')}</small>}</button>)}</div>
      <div className="tables-add-floor"><input value={floorName} onChange={event => setFloorName(event.target.value)} placeholder={t('tables.floorName')} /><button type="button" disabled={!floorName.trim() || !branchId} onClick={() => void createFloor()}>{t('tables.addFloor')}</button></div>
    </div>

    {selectedFloor && <div className="tables-floor-actions">
      <input aria-label={t('tables.floorName')} value={selectedFloor.name} onChange={event => setFloors(current => current.map(floor => floor.id === selectedFloor.id ? { ...floor, name: event.target.value } : floor))} onBlur={() => void updateFloor({ name: selectedFloor.name })} />
      <label className="checkbox-row"><input type="checkbox" checked={selectedFloor.isActive} onChange={event => void updateFloor({ isActive: event.target.checked })} />{t('common.active')}</label>
      <button type="button" className="button-danger" onClick={() => void deleteFloor()}>{t('common.delete')}</button>
    </div>}

    {!floors.length ? <div className="tables-empty settings-card"><strong>{t('tables.noFloors')}</strong><span className="text-muted">{t('tables.noFloorsHint')}</span></div> : <div className="tables-workspace">
      <div className="tables-board" aria-busy={loading}>
        <div className="tables-board-legend"><span><i className="is-free" />{t('tables.available')}</span><span><i className="is-occupied" />{t('tables.occupied')}</span><button type="button" className="button-secondary" onClick={() => void loadBoard(branchId, floorId)}>{t('tables.refresh')}</button></div>
        <div className="tables-canvas">{tables.map(table => <article key={table.id} className={`floor-table floor-table-${table.shape.toLowerCase()} ${table.isOccupied ? 'is-occupied' : 'is-free'} ${!table.isActive ? 'is-inactive' : ''}`} style={{ '--table-x': `${table.positionX}%`, '--table-y': `${table.positionY}%` } as React.CSSProperties} onClick={() => editTable(table)}>
          <div className="floor-table-heading"><strong>{table.label}</strong><span>{table.capacity ? t('tables.seats', { count: table.capacity }) : ''}</span></div>
          <span className="floor-table-state">{t(table.isOccupied ? 'tables.occupied' : 'tables.available')}</span>
          {table.orders.map(order => <div className="floor-table-order" key={order.id}><span>#{order.orderNumber} · {order.status}</span><Money value={order.grandTotal} /></div>)}
          {table.openQrSessionId && <small>{t('tables.qrSession')}</small>}
          <div className="table-position-pad" onClick={event => event.stopPropagation()}><button aria-label={t('tables.moveUp')} onClick={() => void moveTable(table, 0, -5)}>↑</button><button aria-label={t('tables.moveLeft')} onClick={() => void moveTable(table, -5, 0)}>←</button><button aria-label={t('tables.moveRight')} onClick={() => void moveTable(table, 5, 0)}>→</button><button aria-label={t('tables.moveDown')} onClick={() => void moveTable(table, 0, 5)}>↓</button></div>
        </article>)}</div>
      </div>

      <aside className="tables-editor settings-card">
        <div><p className="tables-eyebrow">{draft.id ? t('tables.editTable') : t('tables.newTable')}</p><h2>{draft.id ? draft.label : t('tables.tableDetails')}</h2></div>
        <label>{t('tables.label')}<input value={draft.label} onChange={event => setDraft(current => ({ ...current, label: event.target.value }))} /></label>
        <label>{t('tables.capacity')}<input type="number" min="1" value={draft.capacity} onChange={event => setDraft(current => ({ ...current, capacity: event.target.value }))} /></label>
        <label>{t('tables.shape')}<select value={draft.shape} onChange={event => setDraft(current => ({ ...current, shape: event.target.value as TableDraft['shape'] }))}><option value="Rectangle">{t('tables.rectangle')}</option><option value="Round">{t('tables.round')}</option></select></label>
        <div className="tables-coordinate-grid"><label>X<input type="number" min="0" max="100" value={draft.positionX} onChange={event => setDraft(current => ({ ...current, positionX: Number(event.target.value) }))} /></label><label>Y<input type="number" min="0" max="100" value={draft.positionY} onChange={event => setDraft(current => ({ ...current, positionY: Number(event.target.value) }))} /></label></div>
        <label className="checkbox-row"><input type="checkbox" checked={draft.isActive} onChange={event => setDraft(current => ({ ...current, isActive: event.target.checked }))} />{t('common.active')}</label>
        <div className="modal-actions">{draft.id && <button type="button" className="button-secondary" onClick={() => setDraft(emptyTable())}>{t('common.cancel')}</button>}<button type="button" disabled={!draft.label.trim()} onClick={() => void saveTable()}>{t('common.save')}</button></div>
      </aside>
    </div>}
  </section>
}
