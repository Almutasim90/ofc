import { useEffect, useState } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { api, resolveApiAssetUrl } from '../api/client'
import type { QrMenuCategoryDto, QrSessionDto } from '../api/types'

export default function QrLandingPage() {
  const { pointId, token: legacyToken } = useParams()
  const [params] = useSearchParams()
  const { t, i18n } = useTranslation()
  const [session, setSession] = useState<QrSessionDto | null>(null)
  const [menu, setMenu] = useState<QrMenuCategoryDto[]>([])
  const [failed, setFailed] = useState(false)
  const signedToken = params.get('token')
  const name = (value: { nameAr: string; nameEn: string }) => i18n.language === 'ar' ? value.nameAr : value.nameEn

  useEffect(() => {
    const path = pointId && signedToken
      ? `/api/qr-ordering/points/${pointId}/resolve?token=${encodeURIComponent(signedToken)}`
      : legacyToken ? `/api/qr-ordering/resolve/${encodeURIComponent(legacyToken)}` : null
    if (!path) { setFailed(true); return }
    let active = true
    void api.get<QrSessionDto>(path).then(async result => {
      const categories = await api.get<QrMenuCategoryDto[]>(`/api/qr-ordering/sessions/${result.sessionId}/menu`, { 'X-QR-Session': result.accessToken })
      if (active) { setSession(result); setMenu(categories) }
    }).catch(() => { if (active) setFailed(true) })
    return () => { active = false }
  }, [legacyToken, pointId, signedToken])

  if (failed) return <main className="mx-auto grid min-h-screen max-w-xl place-content-center gap-4 p-6 text-center"><span className="brand-mark mx-auto">O</span><h1>{t('app.title')}</h1><p className="error-text">{t('orderingPoints.invalid')}</p></main>
  if (!session) return <main className="grid min-h-screen place-content-center"><span className="brand-mark animate-pulse">O</span></main>

  return <main className="mx-auto min-h-screen max-w-5xl p-4 sm:p-8">
    <header className="mb-8 rounded-3xl bg-primary p-6 text-on-primary shadow-lg">
      <span className="text-sm font-bold uppercase tracking-[.2em]">{t('app.title')}</span>
      <h1 className="mt-2 text-3xl font-extrabold">{session.label}</h1>
      <p className="mt-1 opacity-80">{t('orderingPoints.sessionReady')}</p>
    </header>
    <div className="grid gap-8">
      {menu.map(category => <section key={category.id}>
        <h2 className="mb-3 text-xl font-extrabold">{name(category)}</h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {category.items.map(item => <article className="product-card overflow-hidden p-0" key={item.id}>
            {item.imageUrl && <img className="h-40 w-full object-cover" src={resolveApiAssetUrl(item.imageUrl)} alt="" />}
            <div className="grid gap-2 p-4"><strong>{name(item)}</strong><span className="text-primary font-extrabold">{item.price.toFixed(3)}</span>{item.kind === 'Combo' && <small>{t('restaurant.combo')}</small>}</div>
          </article>)}
        </div>
      </section>)}
    </div>
  </main>
}
