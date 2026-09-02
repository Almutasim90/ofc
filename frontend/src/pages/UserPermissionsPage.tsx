import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { PermissionOverrideDto } from '../api/types'
import DataTable from '../components/DataTable'
import { useToast } from '../components/ToastContext'

export default function UserPermissionsPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const { id } = useParams<{ id: string }>()
  const [overrides, setOverrides] = useState<PermissionOverrideDto[]>([])
  const [loading, setLoading] = useState(true)

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
    try {
      await api.put(`/api/users/${id}/permission-overrides`, { permissionId, isGranted })
      await load()
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.saveError'))
    }
  }

  return (
    <section>
      <Link to="/users">{t('permissions.back')}</Link>
      <h1>{t('permissions.title')}</h1>
      <DataTable rows={overrides} loading={loading} getRowKey={(override) => override.permissionId}
        getSearchText={(override) => `${override.permissionKey} ${t(`permissionKeys.${override.permissionKey}`, { defaultValue: override.permissionKey })}`}
        columns={[
          { id: 'permission', header: t('permissions.permission'), cell: (o) => t(`permissionKeys.${o.permissionKey}`, { defaultValue: o.permissionKey }), sortValue: (o) => o.permissionKey },
          { id: 'inherit', header: t('permissions.inherit'), cell: (o) => <label className="permission-radio-target"><input
                  type="radio"
                  name={o.permissionId}
                  checked={o.isGranted === null}
                  onChange={() => setOverride(o.permissionId, null)}
                  aria-label={t('permissions.inherit')}
                /></label> },
          { id: 'grant', header: t('permissions.grant'), cell: (o) => <label className="permission-radio-target"><input
                  type="radio"
                  name={o.permissionId}
                  checked={o.isGranted === true}
                  onChange={() => setOverride(o.permissionId, true)}
                  aria-label={t('permissions.grant')}
                /></label> },
          { id: 'deny', header: t('permissions.deny'), cell: (o) => <label className="permission-radio-target"><input
                  type="radio"
                  name={o.permissionId}
                  checked={o.isGranted === false}
                  onChange={() => setOverride(o.permissionId, false)}
                  aria-label={t('permissions.deny')}
                /></label> },
        ]} />
    </section>
  )
}
