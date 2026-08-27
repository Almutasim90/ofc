import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError, resolveApiAssetUrl } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, ProductDto, RawMaterialDto, RecipeLineDto } from '../api/types'
import { DeleteIcon, IconAction, SearchBox } from '../components/TableTools'
import { useToast } from '../components/ToastContext'

interface EditableLine {
  rawMaterialId: string
  quantityRequired: string
}

export default function ProductRecipePage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const toast = useToast()
  const { id } = useParams<{ id: string }>()

  const [branches, setBranches] = useState<BranchDto[]>([])
  const [materials, setMaterials] = useState<RawMaterialDto[]>([])
  const [product, setProduct] = useState<ProductDto | null>(null)
  const [branchId, setBranchId] = useState('')
  const [lines, setLines] = useState<EditableLine[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [search, setSearch] = useState('')

  useEffect(() => {
    const init = async () => {
      const [branchesData, materialsData, productsData] = await Promise.all([
        api.get<BranchDto[]>('/api/branches'),
        api.get<RawMaterialDto[]>('/api/raw-materials'),
        api.get<ProductDto[]>('/api/products'),
      ])
      setBranches(branchesData)
      setMaterials(materialsData)
      setProduct(productsData.find((item) => item.id === id) ?? null)
      const defaultBranch = user?.branchId ?? branchesData[0]?.id ?? ''
      setBranchId(defaultBranch)
    }
    init()
  }, [user, id])

  useEffect(() => {
    if (!id || !branchId) return
    const loadRecipe = async () => {
      setLoading(true)
      const recipe = await api.get<RecipeLineDto[]>(`/api/products/${id}/recipe?branchId=${branchId}`)
      setLines(recipe.map((r) => ({ rawMaterialId: r.rawMaterialId, quantityRequired: r.quantityRequired.toString() })))
      setLoading(false)
    }
    loadRecipe()
  }, [id, branchId])

  const addLine = () => {
    if (materials.length === 0) return
    setLines([...lines, { rawMaterialId: materials[0].id, quantityRequired: '0' }])
  }

  const removeLine = (index: number) => {
    setLines(lines.filter((_, i) => i !== index))
  }

  const updateLine = (index: number, patch: Partial<EditableLine>) => {
    setLines(lines.map((line, i) => (i === index ? { ...line, ...patch } : line)))
  }

  const save = async () => {
    if (!id || !branchId) return
    setSaving(true)
    try {
      await api.put(`/api/products/${id}/recipe`, {
        branchId,
        lines: lines.map((l) => ({ rawMaterialId: l.rawMaterialId, quantityRequired: Number(l.quantityRequired) })),
      })
      toast.success(t('common.updated'))
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t('common.saveError'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="recipe-page">
      <header className="recipe-hero">
        <div>
          <Link className="recipe-back" to="/products">← {t('recipe.back')}</Link>
          <span className="section-kicker">{t('recipe.kicker')}</span>
          <h1>{product ? `${t('recipe.title')}: ${product.nameAr}` : t('recipe.title')}</h1>
          <p>{t('recipe.optionalHint')}</p>
        </div>
        {product && <ProductRecipeImage product={product} />}
      </header>

      <section className="recipe-settings-card">
        <label className="recipe-branch-field">
          <span>{t('recipe.branch')}</span>
          <select value={branchId} onChange={(e) => setBranchId(e.target.value)} disabled={!!user?.branchId}>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.nameAr} · {b.nameEn}
            </option>
          ))}
          </select>
        </label>
        <div className="recipe-count"><strong>{lines.length}</strong><span>{t('recipe.ingredientsCount')}</span></div>
      </section>

      {loading ? (
        <p>{t('common.loading')}</p>
      ) : (
        <>
          <section className="recipe-editor-card">
          <div className="recipe-editor-heading">
            <div><h2>{t('recipe.ingredients')}</h2><p>{t('recipe.editorHint')}</p></div>
            <button type="button" onClick={addLine}>+ {t('recipe.addLine')}</button>
          </div>
          {lines.length === 0 && <div className="recipe-empty"><span>✦</span><p>{t('recipe.empty')}</p><button type="button" onClick={addLine}>{t('recipe.addLine')}</button></div>}
          {lines.length > 0 && <><div className="table-toolbar"><SearchBox value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('common.search')} /></div><div className="table-shell recipe-table-shell"><table>
            <thead>
              <tr>
                <th>{t('recipe.rawMaterial')}</th>
                <th>{t('recipe.quantityRequired')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line, index) => ({ line, index })).filter(({ line }) => { const material = materials.find((m) => m.id === line.rawMaterialId); return `${material?.nameAr ?? ''} ${material?.nameEn ?? ''} ${material?.unit ?? ''}`.toLowerCase().includes(search.trim().toLowerCase()) }).map(({ line, index }) => (
                <tr key={index}>
                  <td>
                    <select
                      value={line.rawMaterialId}
                      onChange={(e) => updateLine(index, { rawMaterialId: e.target.value })}
                    >
                      {materials.map((m) => (
                        <option key={m.id} value={m.id}>
                          {m.nameEn} ({m.unit})
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <input
                      type="number"
                      step="0.001"
                      min="0"
                      value={line.quantityRequired}
                      onChange={(e) => updateLine(index, { quantityRequired: e.target.value })}
                    />
                  </td>
                  <td>
                    <IconAction label={t('recipe.remove')} onClick={() => removeLine(index)}><DeleteIcon /></IconAction>
                  </td>
                </tr>
              ))}
            </tbody>
          </table></div></>}
          <div className="recipe-actions">
            <Link to="/products">{t('recipe.cancel')}</Link>
            <button type="button" onClick={save} disabled={saving}>
            {t('recipe.save')}
            </button>
          </div>
          </section>
        </>
      )}
    </div>
  )
}

function ProductRecipeImage({ product }: { product: ProductDto }) {
  const source = product.iconOrImageUrl?.trim()
  const isImage = source && (source.startsWith('/') || source.startsWith('http://') || source.startsWith('https://') || source.startsWith('data:image/'))
  return <div className="recipe-product-image">{isImage ? <img src={resolveApiAssetUrl(source)} alt={product.nameAr} /> : <span>{source || product.nameAr.charAt(0)}</span>}</div>
}
