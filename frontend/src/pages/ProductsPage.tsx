import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { CreateProductRequest, ProductDto, UpdateProductRequest } from '../api/types'

type EditingState = { mode: 'create' } | { mode: 'edit'; product: ProductDto } | null

export default function ProductsPage() {
  const { t } = useTranslation()
  const [products, setProducts] = useState<ProductDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<EditingState>(null)

  const load = async () => {
    setLoading(true)
    setProducts(await api.get<ProductDto[]>('/api/products'))
    setLoading(false)
  }

  useEffect(() => {
    load()
  }, [])

  if (loading) return <p>{t('common.loading')}</p>

  return (
    <div>
      <h1>{t('products.title')}</h1>
      <button type="button" onClick={() => setEditing({ mode: 'create' })}>
        {t('products.create')}
      </button>

      <table>
        <thead>
          <tr>
            <th>{t('products.nameAr')}</th>
            <th>{t('products.nameEn')}</th>
            <th>{t('products.category')}</th>
            <th>{t('products.price')}</th>
            <th>{t('products.active')}</th>
            <th>{t('products.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {products.map((product) => (
            <tr key={product.id}>
              <td>{product.nameAr}</td>
              <td>{product.nameEn}</td>
              <td>{product.category}</td>
              <td>{product.price.toFixed(3)}</td>
              <td>{product.isActive ? t('products.active') : t('products.inactive')}</td>
              <td>
                <button type="button" onClick={() => setEditing({ mode: 'edit', product })}>
                  {t('products.edit')}
                </button>
                <Link to={`/products/${product.id}/recipe`}>{t('products.recipe')}</Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {editing && (
        <ProductForm
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

function ProductForm({
  editing,
  onClose,
  onSaved,
}: {
  editing: Exclude<EditingState, null>
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const existing = editing.mode === 'edit' ? editing.product : null

  const [nameAr, setNameAr] = useState(existing?.nameAr ?? '')
  const [nameEn, setNameEn] = useState(existing?.nameEn ?? '')
  const [category, setCategory] = useState(existing?.category ?? '')
  const [price, setPrice] = useState(existing?.price?.toString() ?? '0')
  const [iconOrImageUrl, setIconOrImageUrl] = useState(existing?.iconOrImageUrl ?? '')
  const [isActive, setIsActive] = useState(existing?.isActive ?? true)
  const [submitting, setSubmitting] = useState(false)

  const onSubmit = async () => {
    setSubmitting(true)
    try {
      if (editing.mode === 'create') {
        const request: CreateProductRequest = {
          nameAr,
          nameEn,
          category,
          price: Number(price),
          iconOrImageUrl: iconOrImageUrl || null,
        }
        await api.post('/api/products', request)
      } else {
        const request: UpdateProductRequest = {
          nameAr,
          nameEn,
          category,
          price: Number(price),
          iconOrImageUrl: iconOrImageUrl || null,
          isActive,
        }
        await api.put(`/api/products/${editing.product.id}`, request)
      }
      onSaved()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <h2>{editing.mode === 'create' ? t('products.createTitle') : t('products.editTitle')}</h2>
        <label>
          {t('products.nameAr')}
          <input value={nameAr} onChange={(e) => setNameAr(e.target.value)} required />
        </label>
        <label>
          {t('products.nameEn')}
          <input value={nameEn} onChange={(e) => setNameEn(e.target.value)} required />
        </label>
        <label>
          {t('products.category')}
          <input value={category} onChange={(e) => setCategory(e.target.value)} required placeholder="Tea / Sweet / Food" />
        </label>
        <label>
          {t('products.price')}
          <input type="number" step="0.001" min="0" value={price} onChange={(e) => setPrice(e.target.value)} required />
        </label>
        <label>
          {t('products.icon')}
          <input value={iconOrImageUrl} onChange={(e) => setIconOrImageUrl(e.target.value)} placeholder="https://... or emoji" />
        </label>
        {editing.mode === 'edit' && (
          <label>
            {t('products.active')}
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
          </label>
        )}
        <div className="modal-actions">
          <button type="button" onClick={onSubmit} disabled={submitting}>
            {t('products.save')}
          </button>
          <button type="button" onClick={onClose}>
            {t('products.cancel')}
          </button>
        </div>
      </div>
    </div>
  )
}
