import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { CreateProductRequest, ProductDto, UpdateProductRequest } from '../api/types'
import Money from '../components/Money'
import { DetailsIcon, EditIcon, IconAction, SearchBox } from '../components/TableTools'

type EditingState = { mode: 'create' } | { mode: 'edit'; product: ProductDto } | null

export default function ProductsPage() {
  const { t } = useTranslation()
  const [products, setProducts] = useState<ProductDto[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<EditingState>(null)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 8

  const load = async () => {
    setLoading(true)
    setProducts(await api.get<ProductDto[]>('/api/products'))
    setLoading(false)
  }

  useEffect(() => {
    load()
  }, [])

  const filteredProducts = products.filter((product) =>
    `${product.nameAr} ${product.nameEn} ${product.category}`.toLowerCase().includes(search.trim().toLowerCase()),
  )
  const pageCount = Math.max(1, Math.ceil(filteredProducts.length / pageSize))
  const currentPage = Math.min(page, pageCount)
  const visibleProducts = filteredProducts.slice((currentPage - 1) * pageSize, currentPage * pageSize)

  useEffect(() => {
    setPage(1)
  }, [search])

  if (loading) return <p>{t('common.loading')}</p>

  return (
    <div>
      <h1>{t('products.title')}</h1>
      <div className="table-toolbar"><SearchBox value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('common.search')} /><button type="button" onClick={() => setEditing({ mode: 'create' })}>
        {t('products.create')}
      </button></div>

      <div className="table-shell"><table>
        <thead>
          <tr>
            <th>{t('products.image')}</th>
            <th>{t('products.nameAr')}</th>
            <th>{t('products.nameEn')}</th>
            <th>{t('products.category')}</th>
            <th>{t('products.price')}</th>
            <th>{t('products.active')}</th>
            <th>{t('products.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {visibleProducts.map((product) => (
            <tr key={product.id}>
              <td><ProductThumbnail product={product} /></td>
              <td>{product.nameAr}</td>
              <td>{product.nameEn}</td>
              <td>{product.category}</td>
              <td><Money value={product.price} /></td>
              <td>{product.isActive ? t('products.active') : t('products.inactive')}</td>
              <td><div className="row-actions">
                <IconAction label={t('products.edit')} onClick={() => setEditing({ mode: 'edit', product })}><EditIcon /></IconAction>
                <Link className="icon-action" aria-label={t('products.recipe')} title={t('products.recipe')} to={`/products/${product.id}/recipe`}><DetailsIcon /></Link>
              </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table></div>

      {filteredProducts.length > 0 && (
        <nav className="pagination" aria-label={t('products.pagination')}>
          <span className="pagination-summary">
            {t('products.showing', {
              from: (currentPage - 1) * pageSize + 1,
              to: Math.min(currentPage * pageSize, filteredProducts.length),
              total: filteredProducts.length,
            })}
          </span>
          <div className="pagination-controls">
            <button type="button" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={currentPage === 1}>{t('products.previous')}</button>
            <span>{t('products.pageOf', { page: currentPage, total: pageCount })}</span>
            <button type="button" onClick={() => setPage((value) => Math.min(pageCount, value + 1))} disabled={currentPage === pageCount}>{t('products.next')}</button>
          </div>
        </nav>
      )}

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

function ProductThumbnail({ product }: { product: ProductDto }) {
  const source = product.iconOrImageUrl?.trim()
  if (!source) return <span className="product-thumbnail product-thumbnail--fallback">{product.nameEn.charAt(0) || product.nameAr.charAt(0)}</span>
  const isImage = source.startsWith('/') || source.startsWith('http://') || source.startsWith('https://') || source.startsWith('data:image/')
  return isImage
    ? <img className="product-thumbnail" src={source} alt="" />
    : <span className="product-thumbnail product-thumbnail--fallback" aria-hidden="true">{source}</span>
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
      <div className="modal product-modal" role="dialog" aria-modal="true" aria-labelledby="product-form-title">
        <div className="product-modal-header">
          <div>
            <span className="section-kicker">{t('products.catalog')}</span>
            <h2 id="product-form-title">{editing.mode === 'create' ? t('products.createTitle') : t('products.editTitle')}</h2>
          </div>
          <button type="button" className="modal-close" onClick={onClose} aria-label={t('products.cancel')}>×</button>
        </div>
        <div className="product-form-grid">
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
          <label className="checkbox-field">
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            <span>{t('products.active')}</span>
          </label>
        )}
        </div>
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
