import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, RawMaterialDto, RecipeLineDto } from '../api/types'

interface EditableLine {
  rawMaterialId: string
  quantityRequired: string
}

export default function ProductRecipePage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const { id } = useParams<{ id: string }>()

  const [branches, setBranches] = useState<BranchDto[]>([])
  const [materials, setMaterials] = useState<RawMaterialDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [lines, setLines] = useState<EditableLine[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const init = async () => {
      const [branchesData, materialsData] = await Promise.all([
        api.get<BranchDto[]>('/api/branches'),
        api.get<RawMaterialDto[]>('/api/raw-materials'),
      ])
      setBranches(branchesData)
      setMaterials(materialsData)
      const defaultBranch = user?.branchId ?? branchesData[0]?.id ?? ''
      setBranchId(defaultBranch)
    }
    init()
  }, [user])

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
    } finally {
      setSaving(false)
    }
  }

  return (
    <div>
      <Link to="/products">{t('recipe.back')}</Link>
      <h1>{t('recipe.title')}</h1>
      <p>{t('recipe.optionalHint')}</p>

      <label>
        {t('recipe.branch')}
        <select value={branchId} onChange={(e) => setBranchId(e.target.value)} disabled={!!user?.branchId}>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.nameEn}
            </option>
          ))}
        </select>
      </label>

      {loading ? (
        <p>{t('common.loading')}</p>
      ) : (
        <>
          {lines.length === 0 && <p>{t('recipe.empty')}</p>}
          <table>
            <thead>
              <tr>
                <th>{t('recipe.rawMaterial')}</th>
                <th>{t('recipe.quantityRequired')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line, index) => (
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
                    <button type="button" onClick={() => removeLine(index)}>
                      {t('recipe.remove')}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" onClick={addLine}>
            {t('recipe.addLine')}
          </button>
          <button type="button" onClick={save} disabled={saving}>
            {t('recipe.save')}
          </button>
        </>
      )}
    </div>
  )
}
