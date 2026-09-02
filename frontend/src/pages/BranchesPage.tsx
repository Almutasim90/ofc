import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, CreateBranchRequest, UpdateBranchRequest } from '../api/types'
import DataTable from '../components/DataTable'
import { EditIcon, IconAction } from '../components/TableTools'
import Money from '../components/Money'
import { useToast } from '../components/ToastContext'

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

  return (
    <section>
      <h1>{t('branches.title')}</h1>
      <DataTable rows={branches} loading={loading} getRowKey={(branch) => branch.id}
        getSearchText={(branch) => `${branch.nameAr} ${branch.nameEn} ${branch.code}`}
        toolbar={<button type="button" onClick={() => setEditing({ mode: 'create' })}>{t('branches.create')}</button>}
        columns={[
          { id: 'nameAr', header: t('branches.nameAr'), cell: (branch) => branch.nameAr, sortValue: (branch) => branch.nameAr },
          { id: 'nameEn', header: t('branches.nameEn'), cell: (branch) => branch.nameEn, sortValue: (branch) => branch.nameEn },
          { id: 'code', header: t('branches.code'), cell: (branch) => branch.code, sortValue: (branch) => branch.code },
          { id: 'float', header: t('branches.defaultOpeningFloat'), cell: (branch) => <Money value={branch.defaultOpeningFloat} />, sortValue: (branch) => branch.defaultOpeningFloat },
          { id: 'active', header: t('branches.active'), cell: (branch) => branch.isActive ? t('branches.active') : t('branches.inactive'), sortValue: (branch) => branch.isActive },
          { id: 'actions', header: t('branches.actions'), cell: (branch) => <IconAction label={t('branches.edit')} onClick={() => setEditing({ mode: 'edit', branch })}><EditIcon /></IconAction> },
        ]} />

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
