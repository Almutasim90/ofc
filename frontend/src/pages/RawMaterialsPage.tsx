import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api/client'
import type { CreateRawMaterialRequest, RawMaterialDto, UpdateRawMaterialRequest } from '../api/types'
import { EditIcon, IconAction, SearchBox } from '../components/TableTools'

type EditingState = { mode: 'create' } | { mode: 'edit'; material: RawMaterialDto } | null

export default function RawMaterialsPage() {
  const { t } = useTranslation()
  const [materials, setMaterials] = useState<RawMaterialDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<EditingState>(null)
  const [search, setSearch] = useState('')

  const load = async () => {
    setLoading(true)
    setMaterials(await api.get<RawMaterialDto[]>('/api/raw-materials'))
    setLoading(false)
  }

  useEffect(() => {
    load()
  }, [])

  if (loading) return <p>{t('common.loading')}</p>

  return (
    <section>
      <h1>{t('rawMaterials.title')}</h1>
      <div className="table-toolbar"><SearchBox value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('common.search')} /><button type="button" onClick={() => setEditing({ mode: 'create' })}>
        {t('rawMaterials.create')}
      </button></div>

      <div className="table-shell"><table>
        <thead>
          <tr>
            <th>{t('rawMaterials.nameAr')}</th>
            <th>{t('rawMaterials.nameEn')}</th>
            <th>{t('rawMaterials.unit')}</th>
            <th>{t('rawMaterials.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {materials.filter((material) => `${material.nameAr} ${material.nameEn} ${material.unit}`.toLowerCase().includes(search.trim().toLowerCase())).map((material) => (
            <tr key={material.id}>
              <td>{material.nameAr}</td>
              <td>{material.nameEn}</td>
              <td>{material.unit}</td>
              <td>
                <IconAction label={t('rawMaterials.edit')} onClick={() => setEditing({ mode: 'edit', material })}><EditIcon /></IconAction>
              </td>
            </tr>
          ))}
        </tbody>
      </table></div>

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
      onSaved()
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
