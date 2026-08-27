import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, CreateBranchRequest, UpdateBranchRequest } from '../api/types'
import { EditIcon, IconAction, SearchBox } from '../components/TableTools'
import Money from '../components/Money'
import { useToast } from '../components/ToastContext'

type EditingState = { mode: 'create' } | { mode: 'edit'; branch: BranchDto } | null

export default function BranchesPage() {
  const { t } = useTranslation()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<EditingState>(null)
  const [search, setSearch] = useState('')

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
    <section>
      <h1>{t('branches.title')}</h1>
      <div className="table-toolbar"><SearchBox value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('common.search')} /><button type="button" onClick={() => setEditing({ mode: 'create' })}>
        {t('branches.create')}
      </button></div>

      <div className="table-shell"><table>
        <thead>
          <tr>
            <th>{t('branches.nameAr')}</th>
            <th>{t('branches.nameEn')}</th>
          <th>{t('branches.code')}</th>
            <th>{t('branches.defaultOpeningFloat')}</th>
            <th>{t('branches.active')}</th>
            <th>{t('branches.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {branches.filter((branch) => `${branch.nameAr} ${branch.nameEn} ${branch.code}`.toLowerCase().includes(search.trim().toLowerCase())).map((branch) => (
            <tr key={branch.id}>
              <td>{branch.nameAr}</td>
              <td>{branch.nameEn}</td>
              <td>{branch.code}</td>
              <td><Money value={branch.defaultOpeningFloat} /></td>
              <td>{branch.isActive ? t('branches.active') : t('branches.inactive')}</td>
              <td>
                <IconAction label={t('branches.edit')} onClick={() => setEditing({ mode: 'edit', branch })}><EditIcon /></IconAction>
              </td>
            </tr>
          ))}
        </tbody>
      </table></div>

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
    </section>
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
  const toast = useToast()
  const existing = editing.mode === 'edit' ? editing.branch : null

  const [nameAr, setNameAr] = useState(existing?.nameAr ?? '')
  const [nameEn, setNameEn] = useState(existing?.nameEn ?? '')
  const [code, setCode] = useState(existing?.code ?? '')
  const [defaultOpeningFloat, setDefaultOpeningFloat] = useState(existing?.defaultOpeningFloat?.toString() ?? '0')
  const [isActive, setIsActive] = useState(existing?.isActive ?? true)
  const [submitting, setSubmitting] = useState(false)

  const onSubmit = async () => {
    setSubmitting(true)
    try {
      if (editing.mode === 'create') {
        const request: CreateBranchRequest = { nameAr, nameEn, code, defaultOpeningFloat: Number(defaultOpeningFloat) }
        await api.post('/api/branches', request)
      } else {
        const request: UpdateBranchRequest = { nameAr, nameEn, code, defaultOpeningFloat: Number(defaultOpeningFloat), isActive }
        await api.put(`/api/branches/${editing.branch.id}`, request)
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
      <div className="modal branch-modal">
        <h2>{editing.mode === 'create' ? t('branches.createTitle') : t('branches.editTitle')}</h2>
        <div className="settings-form-grid">
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
          <label>
            {t('branches.defaultOpeningFloat')}
            <input type="number" min="0" step="0.001" value={defaultOpeningFloat} onChange={(e) => setDefaultOpeningFloat(e.target.value)} required />
          </label>
          {editing.mode === 'edit' && (
            <label>
              {t('branches.active')}
              <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            </label>
          )}
        </div>
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
