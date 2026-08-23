import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api/client'
import type { BranchDto, CreateBranchRequest, UpdateBranchRequest } from '../api/types'

type EditingState = { mode: 'create' } | { mode: 'edit'; branch: BranchDto } | null

export default function BranchesPage() {
  const { t } = useTranslation()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<EditingState>(null)

  const load = async () => {
    setLoading(true)
    setBranches(await api.get<BranchDto[]>('/api/branches'))
    setLoading(false)
  }

  useEffect(() => {
    load()
  }, [])

  if (loading) return <p>{t('common.loading')}</p>

  return (
    <div>
      <h1>{t('branches.title')}</h1>
      <button type="button" onClick={() => setEditing({ mode: 'create' })}>
        {t('branches.create')}
      </button>

      <table>
        <thead>
          <tr>
            <th>{t('branches.nameAr')}</th>
            <th>{t('branches.nameEn')}</th>
            <th>{t('branches.code')}</th>
            <th>{t('branches.active')}</th>
            <th>{t('branches.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {branches.map((branch) => (
            <tr key={branch.id}>
              <td>{branch.nameAr}</td>
              <td>{branch.nameEn}</td>
              <td>{branch.code}</td>
              <td>{branch.isActive ? t('branches.active') : t('branches.inactive')}</td>
              <td>
                <button type="button" onClick={() => setEditing({ mode: 'edit', branch })}>
                  {t('branches.edit')}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {editing && (
        <BranchForm
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

function BranchForm({
  editing,
  onClose,
  onSaved,
}: {
  editing: Exclude<EditingState, null>
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const existing = editing.mode === 'edit' ? editing.branch : null

  const [nameAr, setNameAr] = useState(existing?.nameAr ?? '')
  const [nameEn, setNameEn] = useState(existing?.nameEn ?? '')
  const [code, setCode] = useState(existing?.code ?? '')
  const [isActive, setIsActive] = useState(existing?.isActive ?? true)
  const [submitting, setSubmitting] = useState(false)

  const onSubmit = async () => {
    setSubmitting(true)
    try {
      if (editing.mode === 'create') {
        const request: CreateBranchRequest = { nameAr, nameEn, code }
        await api.post('/api/branches', request)
      } else {
        const request: UpdateBranchRequest = { nameAr, nameEn, code, isActive }
        await api.put(`/api/branches/${editing.branch.id}`, request)
      }
      onSaved()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <h2>{editing.mode === 'create' ? t('branches.createTitle') : t('branches.editTitle')}</h2>
        <label>
          {t('branches.nameAr')}
          <input value={nameAr} onChange={(e) => setNameAr(e.target.value)} required />
        </label>
        <label>
          {t('branches.nameEn')}
          <input value={nameEn} onChange={(e) => setNameEn(e.target.value)} required />
        </label>
        <label>
          {t('branches.code')}
          <input value={code} onChange={(e) => setCode(e.target.value)} required />
        </label>
        {editing.mode === 'edit' && (
          <label>
            {t('branches.active')}
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
          </label>
        )}
        <div className="modal-actions">
          <button type="button" onClick={onSubmit} disabled={submitting}>
            {t('branches.save')}
          </button>
          <button type="button" onClick={onClose}>
            {t('branches.cancel')}
          </button>
        </div>
      </div>
    </div>
  )
}
