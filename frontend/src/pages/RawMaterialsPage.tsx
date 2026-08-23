import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api/client'
import type { CreateRawMaterialRequest, RawMaterialDto, UpdateRawMaterialRequest } from '../api/types'

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

  if (loading) return <p>{t('common.loading')}</p>

  return (
    <div>
      <h1>{t('rawMaterials.title')}</h1>
      <button type="button" onClick={() => setEditing({ mode: 'create' })}>
        {t('rawMaterials.create')}
      </button>

      <table>
        <thead>
          <tr>
            <th>{t('rawMaterials.nameAr')}</th>
            <th>{t('rawMaterials.nameEn')}</th>
            <th>{t('rawMaterials.unit')}</th>
            <th>{t('rawMaterials.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {materials.map((material) => (
            <tr key={material.id}>
              <td>{material.nameAr}</td>
              <td>{material.nameEn}</td>
              <td>{material.unit}</td>
              <td>
                <button type="button" onClick={() => setEditing({ mode: 'edit', material })}>
                  {t('rawMaterials.edit')}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

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
    </div>
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
  const [submitting, setSubmitting] = useState(false)

  const onSubmit = async () => {
    setSubmitting(true)
    try {
      const request: CreateRawMaterialRequest | UpdateRawMaterialRequest = { nameAr, nameEn, unit }
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
          <input value={unit} onChange={(e) => setUnit(e.target.value)} required placeholder="kg / piece / liter" />
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
