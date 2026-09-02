import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, CreateUserRequest, RoleDto, UpdateUserRequest, UserDto } from '../api/types'
import DataTable from '../components/DataTable'
import { DetailsIcon, EditIcon, IconAction } from '../components/TableTools'
import PasswordField from '../components/PasswordField'
import { useToast } from '../components/ToastContext'

type EditingState = { mode: 'create' } | { mode: 'edit'; user: UserDto } | null

export default function UsersPage() {
  const { t } = useTranslation()
  const { user: currentUser } = useAuth()
  const [users, setUsers] = useState<UserDto[]>([])
  const [roles, setRoles] = useState<RoleDto[]>([])
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<EditingState>(null)

  const load = async () => {
    setLoading(true)
    const [usersData, rolesData, branchesData] = await Promise.all([
      api.get<UserDto[]>('/api/users'),
      api.get<RoleDto[]>('/api/roles'),
      api.get<BranchDto[]>('/api/branches'),
    ])
    setUsers(usersData)
    setRoles(rolesData)
    setBranches(branchesData)
    setLoading(false)
  }

  useEffect(() => {
    load()
  }, [])

  const branchName = (branchId: string | null) => branches.find((b) => b.id === branchId)?.nameEn ?? '-'

  return (
    <section>
      <h1>{t('users.title')}</h1>
      <DataTable rows={users} loading={loading} getRowKey={(user) => user.id}
        getSearchText={(user) => `${user.fullName} ${user.username} ${user.roleName} ${branchName(user.branchId)}`}
        toolbar={<button type="button" onClick={() => setEditing({ mode: 'create' })}>{t('users.create')}</button>}
        columns={[
          { id: 'name', header: t('users.fullName'), cell: (user) => user.fullName, sortValue: (user) => user.fullName },
          { id: 'username', header: t('users.username'), cell: (user) => user.username, sortValue: (user) => user.username },
          { id: 'role', header: t('users.role'), cell: (user) => t(`roles.${user.roleName}`, { defaultValue: user.roleName }), sortValue: (user) => user.roleName },
          { id: 'branch', header: t('users.branchId'), cell: (user) => branchName(user.branchId), sortValue: (user) => branchName(user.branchId) },
          { id: 'active', header: t('users.active'), cell: (user) => user.isActive ? t('users.active') : t('users.inactive'), sortValue: (user) => user.isActive },
          { id: 'actions', header: t('users.actions'), cell: (user) => <div className="row-actions"><IconAction label={t('users.edit')} onClick={() => setEditing({ mode: 'edit', user })}><EditIcon /></IconAction><Link className="icon-action" aria-label={t('users.permissions')} title={t('users.permissions')} to={`/users/${user.id}/permissions`}><DetailsIcon /></Link></div> },
        ]} />

      {editing && (
        <UserForm
          roles={roles}
          branches={branches}
          editing={editing}
          canResetPassword={currentUser?.roleName === 'GeneralManager'}
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

function UserForm({
  roles,
  branches,
  editing,
  canResetPassword,
  onClose,
  onSaved,
}: {
  roles: RoleDto[]
  branches: BranchDto[]
  editing: Exclude<EditingState, null>
  canResetPassword: boolean
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const existing = editing.mode === 'edit' ? editing.user : null

  const [fullName, setFullName] = useState(existing?.fullName ?? '')
  const [username, setUsername] = useState(existing?.username ?? '')
  const [password, setPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [branchId, setBranchId] = useState(existing?.branchId ?? '')
  const [roleId, setRoleId] = useState(existing?.roleId ?? roles[0]?.id ?? '')
  const [preferredLanguage, setPreferredLanguage] = useState(existing?.preferredLanguage ?? 'ar')
  const [isActive, setIsActive] = useState(existing?.isActive ?? true)
  const [submitting, setSubmitting] = useState(false)

  const onSubmit = async () => {
    setSubmitting(true)
    try {
      if (editing.mode === 'create') {
        const request: CreateUserRequest = {
          fullName,
          username,
          password,
          branchId: branchId || null,
          roleId,
          preferredLanguage,
        }
        await api.post('/api/users', request)
      } else {
        const request: UpdateUserRequest = {
          fullName,
          branchId: branchId || null,
          roleId,
          preferredLanguage,
          isActive,
          newPassword: canResetPassword && newPassword ? newPassword : null,
        }
        await api.put(`/api/users/${editing.user.id}`, request)
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
      <div className="modal user-modal" role="dialog" aria-modal="true">
        <h2>{editing.mode === 'create' ? t('users.createTitle') : t('users.editTitle')}</h2>
        <div className="user-form-grid">
        <label>
          {t('users.fullName')}
          <input value={fullName} onChange={(e) => setFullName(e.target.value)} required />
        </label>
        {editing.mode === 'create' && (
          <>
            <label>
              {t('users.username')}
              <input className="credential-input" value={username} onChange={(e) => setUsername(e.target.value)} required autoComplete="username" />
            </label>
            <PasswordField
              label={t('users.password')}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              showLabel={t('common.showPassword')}
              hideLabel={t('common.hidePassword')}
              required
              minLength={8}
              autoComplete="new-password"
            />
          </>
        )}
        <label>
          {t('users.role')}
          <select value={roleId} onChange={(e) => setRoleId(e.target.value)}>
            {roles.map((role) => (
              <option key={role.id} value={role.id}>
                {t(`roles.${role.name}`, { defaultValue: role.name })}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t('users.branchId')}
          <select value={branchId ?? ''} onChange={(e) => setBranchId(e.target.value)}>
            <option value="">{t('users.noBranch')}</option>
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.nameEn}
              </option>
            ))}
          </select>
          <small>{t('users.branchIdHint')}</small>
        </label>
        <label>
          {t('users.language')}
          <select value={preferredLanguage} onChange={(e) => setPreferredLanguage(e.target.value)}>
            <option value="ar">العربية</option>
            <option value="en">English</option>
          </select>
        </label>
        {editing.mode === 'edit' && (
          <>
            {canResetPassword && (
              <PasswordField
                label={t('users.newPassword')}
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                showLabel={t('common.showPassword')}
                hideLabel={t('common.hidePassword')}
                minLength={8}
                placeholder={t('users.newPasswordHint')}
                hint={t('users.passwordOptionalHint')}
                autoComplete="new-password"
              />
            )}
            <label>
              {t('users.active')}
              <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            </label>
          </>
        )}
        </div>
        <div className="modal-actions">
          <button type="button" onClick={onSubmit} disabled={submitting}>
            {t('users.save')}
          </button>
          <button type="button" onClick={onClose}>
            {t('users.cancel')}
          </button>
        </div>
      </div>
    </div>
  )
}
