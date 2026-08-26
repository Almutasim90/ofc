import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import AppIcon from './AppIcon'

export interface BreadcrumbItem {
  label: string
  to?: string
}

export default function Breadcrumb({ items }: { items: BreadcrumbItem[] }) {
  const { t } = useTranslation()
  if (items.length === 0) return null

  return (
    <nav aria-label={t('nav.breadcrumb')} className="breadcrumb">
      <Link to="/" className="breadcrumb-link breadcrumb-home" aria-label={t('nav.home')} title={t('nav.home')}>
        <AppIcon className="h-4 w-4" name="home" />
      </Link>
      {items.map((item, index) => {
        const isLast = index === items.length - 1
        return (
          <span className="breadcrumb-item" key={`${item.label}-${index}`}>
            <AppIcon className="breadcrumb-sep h-3.5 w-3.5 ltr:rotate-180" name="chevron" />
            {!isLast && item.to ? (
              <Link className="breadcrumb-link" to={item.to}>{item.label}</Link>
            ) : (
              <span className="breadcrumb-current" aria-current="page">{item.label}</span>
            )}
          </span>
        )
      })}
    </nav>
  )
}
