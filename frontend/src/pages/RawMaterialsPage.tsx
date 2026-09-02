import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { CreateRawMaterialRequest, RawMaterialDto, UpdateRawMaterialRequest } from '../api/types'
import DataTable from '../components/DataTable'
import { EditIcon, IconAction } from '../components/TableTools'
import { useToast } from '../components/ToastContext'

type EditingState = { mode: 'create' } | { mode: 'edit'; material: RawMaterialDto } | null

export default function RawMaterialsPage() {
  const { t } = useTranslation()
  const [materials, setMaterials] = useState<RawMaterialDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<EditingState>(null)

  const load = async () => {
    setLoading(true)
    setMaterials(await api.get<RawMaterialDto[]>('/api/raw-materials'))
    setLoading(false)
  }

  useEffect(() => {
    load()
  }, [])

  return (
    <section>
      <h1>{t('rawMaterials.title')}</h1>
      <DataTable rows={materials} loading={loading} getRowKey={(material) => material.id}
        getSearchText={(material) => `${material.nameAr} ${material.nameEn} ${material.unit}`}
        toolbar={<button type="button" onClick={() => setEditing({ mode: 'create' })}>{t('rawMaterials.create')}</button>}
        columns={[
          { id: 'nameAr', header: t('rawMaterials.nameAr'), cell: (material) => material.nameAr, sortValue: (material) => material.nameAr },
          { id: 'nameEn', header: t('rawMaterials.nameEn'), cell: (material) => material.nameEn, sortValue: (material) => material.nameEn },
          { id: 'unit', header: t('rawMaterials.unit'), cell: (material) => material.unit, sortValue: (material) => material.unit },
          { id: 'actions', header: t('rawMaterials.actions'), cell: (material) => <IconAction label={t('rawMaterials.edit')} onClick={() => setEditing({ mode: 'edit', material })}><EditIcon /></IconAction> },
        ]} />

      {editing && (
        <MaterialForm
          editing={editing}
          onClose={() => setEditing(null)}
          onSaved={async () => {
            setEditing(null)
            await load()
          }}
        />
      )}
    </section>
  )
}

function MaterialForm({
  editing,
  onClose,
  onSaved,
}: {
  editing: Exclude<EditingState, null>
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const existing = editing.mode === 'edit' ? editing.material : null

  const [nameAr, setNameAr] = useState(existing?.nameAr ?? '')
  const [nameEn, setNameEn] = useState(existing?.nameEn ?? '')
  const [unit, setUnit] = useState(existing?.unit ?? '')
  const [measurementType, setMeasurementType] = useState(existing?.measurementType ?? 'Count')
  const [submitting, setSubmitting] = useState(false)

  const onSubmit = async () => {
    setSubmitting(true)
    try {
      const request: CreateRawMaterialRequest | UpdateRawMaterialRequest = { nameAr, nameEn, unit, measurementType }
      if (editing.mode === 'create') {
        await api.post('/api/raw-materials', request)
      } else {
        await api.put(`/api/raw-materials/${editing.material.id}`, request)
      }
      toast.success(editing.mode === 'create' ? t('common.created') : t('common.updated'))
      onSaved()
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.saveError'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <h2>{editing.mode === 'create' ? t('rawMaterials.createTitle') : t('rawMaterials.editTitle')}</h2>
        <label>
          {t('rawMaterials.nameAr')}
          <input value={nameAr} onChange={(e) => setNameAr(e.target.value)} required />
        </label>
        <label>
          {t('rawMaterials.nameEn')}
          <input value={nameEn} onChange={(e) => setNameEn(e.target.value)} required />
        </label>
        <label>
          {t('rawMaterials.unit')}
          <select value={measurementType} onChange={(e) => { const value=e.target.value as 'Weight'|'Volume'|'Count'; setMeasurementType(value); setUnit(value==='Weight'?'g':value==='Volume'?'ml':'piece') }}><option value="Weight">{t('inventory.measureWeight')}</option><option value="Volume">{t('inventory.measureVolume')}</option><option value="Count">{t('inventory.measureCount')}</option></select>
          <input value={unit} readOnly />
        </label>
        <div className="modal-actions">
          <button type="button" onClick={onSubmit} disabled={submitting}>
            {t('rawMaterials.save')}
          </button>
          <button type="button" onClick={onClose}>
            {t('rawMaterials.cancel')}
          </button>
        </div>
      </div>
    </div>
  )
}
