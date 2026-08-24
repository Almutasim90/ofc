import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import type { PermissionOverrideDto } from '../api/types'
import { SearchBox } from '../components/TableTools'

export default function UserPermissionsPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const [overrides, setOverrides] = useState<PermissionOverrideDto[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')

  const load = async () => {
    if (!id) return
    setLoading(true)
    const data = await api.get<PermissionOverrideDto[]>(`/api/users/${id}/permission-overrides`)
    setOverrides(data)
    setLoading(false)
  }

  useEffect(() => {
    load()
  }, [id])

  const setOverride = async (permissionId: string, isGranted: boolean | null) => {
    if (!id) return
    await api.put(`/api/users/${id}/permission-overrides`, { permissionId, isGranted })
    await load()
  }

  if (loading) return <p>{t('common.loading')}</p>

  return (
    <div>
      <Link to="/users">{t('permissions.back')}</Link>
      <h1>{t('permissions.title')}</h1>
      <div className="table-toolbar"><SearchBox value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('common.search')} /></div><div className="table-shell"><table>
        <thead>
          <tr>
            <th>Permission</th>
            <th>{t('permissions.inherit')}</th>
            <th>{t('permissions.grant')}</th>
            <th>{t('permissions.deny')}</th>
          </tr>
        </thead>
        <tbody>
          {overrides.filter((o) => o.permissionKey.toLowerCase().includes(search.trim().toLowerCase())).map((o) => (
            <tr key={o.permissionId}>
              <td>{o.permissionKey}</td>
              <td>
                <input
                  type="radio"
                  name={o.permissionId}
                  checked={o.isGranted === null}
                  onChange={() => setOverride(o.permissionId, null)}
                />
              </td>
              <td>
                <input
                  type="radio"
                  name={o.permissionId}
                  checked={o.isGranted === true}
                  onChange={() => setOverride(o.permissionId, true)}
                />
              </td>
              <td>
                <input
                  type="radio"
                  name={o.permissionId}
                  checked={o.isGranted === false}
                  onChange={() => setOverride(o.permissionId, false)}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table></div>
    </div>
  )
}
