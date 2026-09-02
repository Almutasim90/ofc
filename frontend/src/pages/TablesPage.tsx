import { useEffect, useRef, useState, type MouseEvent, type PointerEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, RestaurantFloorDto, TableStatusDto } from '../api/types'
import Money from '../components/Money'
import { useToast } from '../components/ToastContext'

type TableDraft = {
  id: string | null
  label: string
  capacity: string
  shape: 'Rectangle' | 'Round'
  isActive: boolean
}

const emptyTable = (): TableDraft => ({ id: null, label: '', capacity: '4', shape: 'Rectangle', isActive: true })
const clamp = (value: number, min = 5, max = 95) => Math.max(min, Math.min(max, value))

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
  const [placing, setPlacing] = useState(false)
  const canvasRef = useRef<HTMLDivElement>(null)
  const dragRef = useRef<{ id: string; startX: number; startY: number; origX: number; origY: number; moved: boolean } | null>(null)

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

  const editTable = (table: TableStatusDto) => setDraft({ id: table.id, label: table.label, capacity: table.capacity?.toString() ?? '', shape: table.shape, isActive: table.isActive })
  const tablePayload = (value: TableDraft, positionX: number, positionY: number) => ({ branchId, label: value.label, capacity: value.capacity ? Number(value.capacity) : null, floorId, positionX, positionY, shape: value.shape, isActive: value.isActive })

  const saveTable = async () => {
    if (!draft.label.trim() || !floorId) return
    try {
      if (draft.id) await api.put(`/api/tables/${draft.id}`, tablePayload(draft, tables.find(x => x.id === draft.id)?.positionX ?? 50, tables.find(x => x.id === draft.id)?.positionY ?? 50))
      else await api.post('/api/tables', tablePayload(draft, 50, 50))
      setDraft(emptyTable())
      await loadBoard(branchId, floorId)
      toast.success(t(draft.id ? 'common.updated' : 'common.created'))
    } catch (error) { report(error) }
  }

  const pointFromEvent = (event: MouseEvent<HTMLDivElement>) => {
    const rect = canvasRef.current?.getBoundingClientRect()
    if (!rect) return null
    return {
      x: clamp(Math.round(((event.clientX - rect.left) / rect.width) * 100)),
      y: clamp(Math.round(((event.clientY - rect.top) / rect.height) * 100)),
    }
  }

  const nextLabel = () => `Table ${tables.length + 1}`

  const createTable = async (positionX: number, positionY: number) => {
    if (!floorId) return
    try {
      const created = await api.post<TableStatusDto>('/api/tables', { branchId, label: nextLabel(), capacity: 4, floorId, positionX, positionY, shape: 'Rectangle', isActive: true })
      setDraft(emptyTable())
      await loadBoard(branchId, floorId)
      toast.success(t('common.created'))
      return created
    } catch (error) { report(error) }
  }

  const handleCanvasClick = async (event: MouseEvent<HTMLDivElement>) => {
    if (!placing) return
    const point = pointFromEvent(event)
    if (!point) return
    setPlacing(false)
    await createTable(point.x, point.y)
  }

  const addTables = async (count: number) => {
    if (!floorId) return
    setPlacing(false)
    const spots = Array.from({ length: count }, (_, index) => ({
      x: count === 1 ? 50 : clamp(20 + (index % 3) * 30),
      y: count === 1 ? 50 : clamp(25 + Math.floor(index / 3) * 25),
    }))
    try {
      for (const spot of spots) {
        await api.post('/api/tables', { branchId, label: nextLabel(), capacity: 4, floorId, positionX: spot.x, positionY: spot.y, shape: 'Rectangle', isActive: true })
      }
      setDraft(emptyTable())
      await loadBoard(branchId, floorId)
      toast.success(count === 1 ? t('common.created') : t('tables.bulkCount', { count }))
    } catch (error) { report(error) }
  }

  const onPointerDown = (event: PointerEvent<HTMLElement>, table: TableStatusDto) => {
    if (event.button !== 0) return
    dragRef.current = { id: table.id, startX: event.clientX, startY: event.clientY, origX: table.positionX, origY: table.positionY, moved: false }
    event.currentTarget.setPointerCapture(event.pointerId)
  }
  const onPointerMove = (event: PointerEvent<HTMLElement>) => {
    const drag = dragRef.current
    if (!drag) return
    const dx = event.clientX - drag.startX
    const dy = event.clientY - drag.startY
    if (!drag.moved && Math.hypot(dx, dy) < 4) return
    drag.moved = true
    const rect = canvasRef.current?.getBoundingClientRect()
    if (!rect) return
    const x = clamp(drag.origX + (dx / rect.width) * 100)
    const y = clamp(drag.origY + (dy / rect.height) * 100)
    setTables(current => current.map(row => row.id === drag.id ? { ...row, positionX: x, positionY: y } : row))
  }
  const onPointerUp = async () => {
    const drag = dragRef.current
    dragRef.current = null
    if (!drag) return
    if (drag.moved) {
      const row = tables.find(x => x.id === drag.id)
      if (row) {
        try {
          await api.put(`/api/tables/${drag.id}`, { branchId, label: row.label, capacity: row.capacity, floorId, positionX: row.positionX, positionY: row.positionY, shape: row.shape, isActive: row.isActive })
          toast.success(t('tables.savedPosition'))
        } catch (error) { report(error); await loadBoard(branchId, floorId) }
      }
    } else {
      const table = tables.find(x => x.id === drag.id)
      if (table) editTable(table)
    }
  }

  return <section className="tables-page">
    <header className="tables-page-header">
      <div><p className="tables-eyebrow">{t('tables.eyebrow')}</p><h1>{t('tables.title')}</h1><p className="text-muted">{t('tables.description')}</p></div>
      <div className="tables-header-actions">
        <label>{t('tables.branch')}<select value={branchId} onChange={event => setBranchId(event.target.value)}>{branches.map(branch => <option key={branch.id} value={branch.id}>{branchName(branch)}</option>)}</select></label>
        <div className="tables-add-group">
          <button type="button" className="button-primary" disabled={!floorId} onClick={() => setPlacing(true)}><strong>+</strong>{t('tables.addTable')}</button>
          <button type="button" className="button-secondary" disabled={!floorId} onClick={() => void addTables(5)}>{t('tables.addFive')}</button>
        </div>
      </div>
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

    {placing && <div className="tables-placing-hint" role="status">{t('tables.placing')}</div>}

    {!floors.length ? <div className="tables-empty settings-card"><strong>{t('tables.noFloors')}</strong><span className="text-muted">{t('tables.noFloorsHint')}</span></div> : <div className="tables-workspace">
      <div className="tables-board" aria-busy={loading}>
        <div className="tables-board-legend"><span><i className="is-free" />{t('tables.available')}</span><span><i className="is-occupied" />{t('tables.occupied')}</span><button type="button" className="button-secondary" onClick={() => void loadBoard(branchId, floorId)}>{t('tables.refresh')}</button></div>
        <div ref={canvasRef} onClick={handleCanvasClick} aria-label={t('tables.title')} className="tables-canvas">
          {tables.map(table => <article key={table.id} onPointerDown={event => onPointerDown(event, table)} onPointerMove={onPointerMove} onPointerUp={onPointerUp} className={`floor-table floor-table-${table.shape.toLowerCase()} ${table.isOccupied ? 'is-occupied' : 'is-free'} ${!table.isActive ? 'is-inactive' : ''} ${placing ? 'is-grabbable' : ''}`} style={{ '--table-x': `${table.positionX}%`, '--table-y': `${table.positionY}%` } as React.CSSProperties}>
            <div className="floor-table-heading"><strong>{table.label}</strong><span>{table.capacity ? t('tables.seats', { count: table.capacity }) : ''}</span></div>
            <span className="floor-table-state">{t(table.isOccupied ? 'tables.occupied' : 'tables.available')}</span>
            {table.orders.map(order => <div className="floor-table-order" key={order.id}><span>#{order.orderNumber} · {order.status}</span><Money value={order.grandTotal} /></div>)}
            {table.openQrSessionId && <small>{t('tables.qrSession')}</small>}
          </article>)}
          {!tables.length && !loading && <div className="tables-canvas-empty">{t('tables.noFloorsHint')}</div>}
        </div>
      </div>

      <aside className="tables-editor settings-card">
        <div><p className="tables-eyebrow">{draft.id ? t('tables.editTable') : t('tables.newTable')}</p><h2>{draft.id ? draft.label : t('tables.tableDetails')}</h2></div>
        <label>{t('tables.label')}<input value={draft.label} onChange={event => setDraft(current => ({ ...current, label: event.target.value }))} /></label>
        <label>{t('tables.capacity')}<input type="number" min="1" value={draft.capacity} onChange={event => setDraft(current => ({ ...current, capacity: event.target.value }))} /></label>
        <label>{t('tables.shape')}<select value={draft.shape} onChange={event => setDraft(current => ({ ...current, shape: event.target.value as TableDraft['shape'] }))}><option value="Rectangle">{t('tables.rectangle')}</option><option value="Round">{t('tables.round')}</option></select></label>
        <label className="checkbox-row"><input type="checkbox" checked={draft.isActive} onChange={event => setDraft(current => ({ ...current, isActive: event.target.checked }))} />{t('common.active')}</label>
        <p className="text-muted tables-editor-hint">{t('tables.dragHint')}</p>
        <div className="modal-actions">{draft.id && <button type="button" className="button-secondary" onClick={() => setDraft(emptyTable())}>{t('common.cancel')}</button>}<button type="button" disabled={!draft.label.trim()} onClick={() => void saveTable()}>{t('common.save')}</button></div>
      </aside>
    </div>}
  </section>
}
