import { useEffect, useRef, useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api, ApiError, resolveApiAssetUrl } from '../api/client'
import type { CreateProductRequest, ProductDto, UpdateProductRequest } from '../api/types'
import Money from '../components/Money'
import DataTable from '../components/DataTable'
import { DetailsIcon, EditIcon, IconAction } from '../components/TableTools'
import { useToast } from '../components/ToastContext'

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

  return (
    <section>
      <h1>{t('products.title')}</h1>
      <DataTable rows={products} loading={loading} pageSize={8} getRowKey={(product) => product.id}
        getSearchText={(product) => `${product.nameAr} ${product.nameEn} ${product.category}`}
        toolbar={<button type="button" onClick={() => setEditing({ mode: 'create' })}>{t('products.create')}</button>}
        columns={[
          { id: 'image', header: t('products.image'), cell: (product) => <ProductThumbnail product={product} /> },
          { id: 'nameAr', header: t('products.nameAr'), cell: (product) => product.nameAr, sortValue: (product) => product.nameAr },
          { id: 'nameEn', header: t('products.nameEn'), cell: (product) => product.nameEn, sortValue: (product) => product.nameEn },
          { id: 'category', header: t('products.category'), cell: (product) => product.category, sortValue: (product) => product.category },
          { id: 'price', header: t('products.price'), cell: (product) => <Money value={product.price} />, sortValue: (product) => product.price },
          { id: 'active', header: t('products.active'), cell: (product) => product.isActive ? t('products.active') : t('products.inactive'), sortValue: (product) => product.isActive },
          { id: 'actions', header: t('products.actions'), cell: (product) => <div className="row-actions"><IconAction label={t('products.edit')} onClick={() => setEditing({ mode: 'edit', product })}><EditIcon /></IconAction><Link className="icon-action" aria-label={t('products.recipe')} title={t('products.recipe')} to={`/products/${product.id}/recipe`}><DetailsIcon /></Link></div> },
        ]} />

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
    </section>
  )
}

function ProductThumbnail({ product }: { product: ProductDto }) {
  const source = product.iconOrImageUrl?.trim()
  if (!source) return <span className="product-thumbnail product-thumbnail--fallback">{product.nameEn.charAt(0) || product.nameAr.charAt(0)}</span>
  const isImage = source.startsWith('/') || source.startsWith('http://') || source.startsWith('https://') || source.startsWith('data:image/')
  return isImage
    ? <img className="product-thumbnail" src={resolveApiAssetUrl(source)} alt="" />
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
  const toast = useToast()
  const existing = editing.mode === 'edit' ? editing.product : null

  const [nameAr, setNameAr] = useState(existing?.nameAr ?? '')
  const [nameEn, setNameEn] = useState(existing?.nameEn ?? '')
  const [category, setCategory] = useState(existing?.category ?? '')
  const [price, setPrice] = useState(existing?.price?.toString() ?? '0')
  const [iconOrImageUrl, setIconOrImageUrl] = useState(existing?.iconOrImageUrl ?? '')
  const [isActive, setIsActive] = useState(existing?.isActive ?? true)
  const [submitting, setSubmitting] = useState(false)
  const [uploading, setUploading] = useState(false)
  const fileInput = useRef<HTMLInputElement>(null)

  const uploadImage = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return
    setUploading(true)
    try {
      const body = new FormData()
      body.append('file', file)
      const result = await api.upload<{ url: string }>('/api/uploads/product-image', body)
      setIconOrImageUrl(result.url)
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.saveError'))
    } finally {
      setUploading(false)
      event.target.value = ''
    }
  }

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
        <div className="channel-logo-field">
          <span className="channel-field-label">{t('products.image')}</span>
          <div className="channel-logo-picker">
            <div className="channel-logo-preview-box">
              {iconOrImageUrl ? <img src={resolveApiAssetUrl(iconOrImageUrl)} alt="" /> : <span>+</span>}
            </div>
            <div>
              <button className="button-secondary channel-upload-button" type="button" disabled={uploading} onClick={() => fileInput.current?.click()}>
                {uploading ? t('products.uploading') : t('products.uploadImage')}
              </button>
              <small>{t('products.imageHint')}</small>
            </div>
            <input ref={fileInput} className="sr-only" type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" onChange={uploadImage} />
          </div>
        </div>
        {editing.mode === 'edit' && (
          <label className="checkbox-field">
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            <span>{t('products.active')}</span>
          </label>
        )}
        </div>
        <div className="modal-actions">
          <button type="button" onClick={onSubmit} disabled={submitting || uploading}>
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
