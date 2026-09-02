import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api/client'
import DataTable from '../components/DataTable'

interface Alert { id:string; branchNameAr:string; branchNameEn:string; materialNameAr:string; materialNameEn:string; triggeredAt:string; resolvedAt:string|null }

export default function NotificationsPage() {
  const { t, i18n } = useTranslation()
  const [rows, setRows] = useState<Alert[]>([])
  const [loading, setLoading] = useState(true)
  useEffect(() => { api.get<Alert[]>('/api/notifications/low-stock?includeResolved=true').then(setRows).finally(() => setLoading(false)) }, [])
  const material = (row: Alert) => i18n.language === 'ar' ? row.materialNameAr : row.materialNameEn
  const branch = (row: Alert) => i18n.language === 'ar' ? row.branchNameAr : row.branchNameEn
  return <section><h1>{t('notifications.history')}</h1><DataTable rows={rows} loading={loading} getRowKey={(row) => row.id}
    defaultSort={{ id: 'date', direction: 'desc' }} getSearchText={(row) => `${row.materialNameAr} ${row.materialNameEn} ${row.branchNameAr} ${row.branchNameEn}`}
    columns={[
      { id: 'material', header: t('inventory.rawMaterial'), cell: material, sortValue: material },
      { id: 'branch', header: t('inventory.branch'), cell: branch, sortValue: branch },
      { id: 'date', header: t('closing.date'), cell: (row) => new Date(row.triggeredAt).toLocaleString(i18n.language), sortValue: (row) => new Date(row.triggeredAt) },
      { id: 'status', header: t('channels.status'), cell: (row) => row.resolvedAt ? t('notifications.resolved') : t('notifications.active'), sortValue: (row) => !!row.resolvedAt },
    ]} /></section>
}
