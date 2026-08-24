import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, CreateUserRequest, RoleDto, UpdateUserRequest, UserDto } from '../api/types'
import { DetailsIcon, EditIcon, IconAction, SearchBox } from '../components/TableTools'
import PasswordField from '../components/PasswordField'

type EditingState = { mode: 'create' } | { mode: 'edit'; user: UserDto } | null

export default function UsersPage() {
  const { t } = useTranslation()
  const { user: currentUser } = useAuth()
  const [users, setUsers] = useState<UserDto[]>([])
  const [roles, setRoles] = useState<RoleDto[]>([])
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<EditingState>(null)
  const [search, setSearch] = useState('')

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

  if (loading) return <p>{t('common.loading')}</p>

  const branchName = (branchId: string | null) => branches.find((b) => b.id === branchId)?.nameEn ?? '-'

  return (
    <section>
      <h1>{t('users.title')}</h1>
      <div className="table-toolbar"><SearchBox value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('common.search')} /><button type="button" onClick={() => setEditing({ mode: 'create' })}>
        {t('users.create')}
      </button></div>

      <div className="table-shell"><table>
        <thead>
          <tr>
            <th>{t('users.fullName')}</th>
            <th>{t('users.username')}</th>
            <th>{t('users.role')}</th>
            <th>{t('users.branchId')}</th>
            <th>{t('users.active')}</th>
            <th>{t('users.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {users.filter((user) => `${user.fullName} ${user.username} ${user.roleName} ${branchName(user.branchId)}`.toLowerCase().includes(search.trim().toLowerCase())).map((user) => (
            <tr key={user.id}>
              <td>{user.fullName}</td>
              <td>{user.username}</td>
              <td>{t(`roles.${user.roleName}`, { defaultValue: user.roleName })}</td>
              <td>{branchName(user.branchId)}</td>
              <td>{user.isActive ? t('users.active') : t('users.inactive')}</td>
              <td><div className="row-actions">
                <IconAction label={t('users.edit')} onClick={() => setEditing({ mode: 'edit', user })}><EditIcon /></IconAction>
                <Link className="icon-action" aria-label={t('users.permissions')} title={t('users.permissions')} to={`/users/${user.id}/permissions`}><DetailsIcon /></Link>
              </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table></div>

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
      onSaved()
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
