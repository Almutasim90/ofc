import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, CreateSaleRequest, ProductDto, SaleDto, ShiftDto } from '../api/types'

interface CartLine {
  productId: string
  nameAr: string
  nameEn: string
  price: number
  quantity: number
}

export default function CashierPage() {
  const { t, i18n } = useTranslation()
  const { user } = useAuth()

  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [products, setProducts] = useState<ProductDto[]>([])
  const [loading, setLoading] = useState(true)
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null)
  const [cart, setCart] = useState<CartLine[]>([])
  const [cartOpen, setCartOpen] = useState(false)
  const [justAdded, setJustAdded] = useState<string | null>(null)
  const [paymentMethod, setPaymentMethod] = useState<'Cash' | 'Card'>('Cash')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successSale, setSuccessSale] = useState<SaleDto | null>(null)
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null)

  useEffect(() => {
    const init = async () => {
      try {
        const [branchesData, productsData, shiftData] = await Promise.all([
          api.get<BranchDto[]>('/api/branches'),
          api.get<ProductDto[]>('/api/products'),
          api.get<ShiftDto | undefined>('/api/shifts/current'),
        ])
        setBranches(branchesData.filter((branch) => branch.isActive))
        setProducts(productsData.filter((product) => product.isActive))
        setBranchId(shiftData?.branchId ?? user?.branchId ?? branchesData.find((branch) => branch.isActive)?.id ?? '')
        setCurrentShift(shiftData ?? null)
      } catch {
        setError(t('cashier.loadError'))
      } finally {
        setLoading(false)
      }
    }
    init()
  }, [t, user])

  const categories = useMemo(() => Array.from(new Set(products.map((p) => p.category))), [products])
  const visibleProducts = selectedCategory ? products.filter((p) => p.category === selectedCategory) : products

  const productName = (p: { nameAr: string; nameEn: string }) => (i18n.language === 'ar' ? p.nameAr : p.nameEn)
  const branchName = (b: BranchDto) => (i18n.language === 'ar' ? b.nameAr : b.nameEn)
  const categoryName = (category: string) => {
    const key = `cashier.categories.${category.toLowerCase()}`
    return i18n.exists(key) ? t(key) : category
  }

  const addToCart = (product: ProductDto) => {
    if (!currentShift) {
      setError(t('cashier.shiftRequired'))
      return
    }
    setCart((prev) => {
      const existing = prev.find((l) => l.productId === product.id)
      if (existing) {
        return prev.map((l) => (l.productId === product.id ? { ...l, quantity: l.quantity + 1 } : l))
      }
      return [...prev, { productId: product.id, nameAr: product.nameAr, nameEn: product.nameEn, price: product.price, quantity: 1 }]
    })
    setJustAdded(product.id)
    window.setTimeout(() => setJustAdded((current) => (current === product.id ? null : current)), 500)
  }

  const removeFromCart = (productId: string) => {
    setCart((prev) => prev.filter((l) => l.productId !== productId))
  }

  const updateQuantity = (productId: string, quantity: number) => {
    if (quantity <= 0) {
      removeFromCart(productId)
      return
    }
    setCart((prev) => prev.map((l) => (l.productId === productId ? { ...l, quantity } : l)))
  }

  const total = cart.reduce((sum, l) => sum + l.price * l.quantity, 0)
  const itemCount = cart.reduce((sum, l) => sum + l.quantity, 0)

  const checkout = async () => {
    if (cart.length === 0 || !branchId) return
    setSubmitting(true)
    setError(null)
    try {
      const request: CreateSaleRequest = {
        branchId,
        paymentMethod,
        lines: cart.map((l) => ({ productId: l.productId, quantity: l.quantity })),
      }
      const sale = await api.post<SaleDto>('/api/sales', request)
      setSuccessSale(sale)
      setCart([])
      setCartOpen(false)
    } catch (err) {
      if (err instanceof ApiError && err.message.startsWith('Insufficient stock')) {
        setError(t('cashier.insufficientStock'))
      } else if (err instanceof ApiError && err.status === 400) {
        setError(t('cashier.saleRejected'))
      } else {
        setError(t('cashier.saleError'))
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) return <p className="p-6 text-muted">{t('common.loading')}</p>

  const cartContent: ReactNode = (
    <div className="flex h-full flex-col">
      <div className="flex items-center justify-between border-b border-border p-4">
        <h2 className="font-cairo text-lg font-bold text-text">{t('cashier.cart')}</h2>
        <button
          type="button"
          className="border-0 bg-transparent p-1 text-muted lg:hidden"
          onClick={() => setCartOpen(false)}
        >
          ✕
        </button>
      </div>

      <div className="flex-1 space-y-3 overflow-y-auto p-4">
        {cart.length === 0 && <p className="text-sm text-muted">{t('cashier.cartEmpty')}</p>}
        {cart.map((line) => (
          <div key={line.productId} className="flex items-center gap-2 rounded-lg bg-surface2 p-2">
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm text-text">{i18n.language === 'ar' ? line.nameAr : line.nameEn}</div>
              <div className="font-cairo text-sm text-accent">{line.price.toFixed(3)}</div>
            </div>
            <div className="flex items-center gap-1">
              <button
                type="button"
                className="h-8 w-8 rounded-md border border-border bg-surface p-0 text-text"
                onClick={() => updateQuantity(line.productId, line.quantity - 1)}
              >
                −
              </button>
              <span className="w-6 text-center font-cairo text-text">{line.quantity}</span>
              <button
                type="button"
                className="h-8 w-8 rounded-md border border-border bg-surface p-0 text-text"
                onClick={() => updateQuantity(line.productId, line.quantity + 1)}
              >
                +
              </button>
            </div>
            <button
              type="button"
              className="border-0 bg-transparent p-1 text-sm text-danger"
              onClick={() => removeFromCart(line.productId)}
            >
              {t('cashier.remove')}
            </button>
          </div>
        ))}
      </div>

      <div className="space-y-3 border-t border-border p-4">
        <div className="flex flex-col gap-1 text-sm text-muted">
          {t('cashier.paymentMethod')}
          <div className="flex gap-2">
            <button
              type="button"
              className={
                paymentMethod === 'Cash'
                  ? 'flex-1 border-0 bg-primary text-white'
                  : 'flex-1 border border-border bg-surface2 text-text'
              }
              onClick={() => setPaymentMethod('Cash')}
            >
              {t('cashier.cash')}
            </button>
            <button
              type="button"
              className={
                paymentMethod === 'Card'
                  ? 'flex-1 border-0 bg-primary text-white'
                  : 'flex-1 border border-border bg-surface2 text-text'
              }
              onClick={() => setPaymentMethod('Card')}
            >
              {t('cashier.card')}
            </button>
          </div>
        </div>

        <div className="flex items-center justify-between font-cairo text-lg font-bold text-text">
          <span>{t('cashier.total')}</span>
          <span className="text-accent">{total.toFixed(3)}</span>
        </div>

        {error && <p className="text-sm text-danger">{error}</p>}

        <button
          type="button"
          className="w-full border-0 bg-primary text-white"
          disabled={cart.length === 0 || submitting}
          onClick={checkout}
        >
          {submitting ? t('cashier.submitting') : t('cashier.confirm')}
        </button>
      </div>
    </div>
  )

  return (
    <div className="cashier-theme -m-6 flex h-[calc(100vh-65px)] overflow-hidden bg-bg text-text">
      <aside className="flex w-20 flex-shrink-0 flex-col items-center gap-2 overflow-y-auto border-e border-border bg-surface py-3">
        <button
          type="button"
          className="flex flex-col items-center gap-1 border-0 bg-transparent p-0"
          onClick={() => setSelectedCategory(null)}
        >
          <span
            className={`flex h-14 w-14 items-center justify-center rounded-full text-lg font-bold ${
              selectedCategory === null ? 'bg-primary text-white' : 'bg-surface2 text-text'
            }`}
          >
            ★
          </span>
          <span className={`text-xs ${selectedCategory === null ? 'text-primary' : 'text-muted'}`}>
            {t('cashier.allCategories')}
          </span>
        </button>
        {categories.map((cat) => (
          <button
            key={cat}
            type="button"
            className="flex flex-col items-center gap-1 border-0 bg-transparent p-0"
            onClick={() => setSelectedCategory(cat)}
          >
            <span
              className={`flex h-14 w-14 items-center justify-center rounded-full text-lg font-bold ${
                selectedCategory === cat ? 'bg-primary text-white' : 'bg-surface2 text-text'
              }`}
            >
              {cat.charAt(0)}
            </span>
            <span className={`text-xs ${selectedCategory === cat ? 'text-primary' : 'text-muted'}`}>
              {categoryName(cat)}
            </span>
          </button>
        ))}
      </aside>

      <div className="flex-1 overflow-y-auto p-4 pb-24 lg:pb-4">
        {!currentShift && (
          <div className="mb-4 flex items-center justify-between gap-3 rounded-xl border border-danger bg-surface p-3 text-danger">
            <span>{t('cashier.shiftRequired')}</span>
            <Link className="rounded-lg bg-primary px-3 py-2 font-bold text-bg" to="/shift">{t('cashier.openShift')}</Link>
          </div>
        )}
        {!user?.branchId && branches.length > 0 && (
          <label className="mb-4 flex max-w-xs flex-col gap-1 text-sm text-muted">
            {t('cashier.branch')}
            <select value={branchId} onChange={(e) => setBranchId(e.target.value)}>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {branchName(b)}
                </option>
              ))}
            </select>
          </label>
        )}

        {error && products.length === 0 && <p className="p-4 text-danger">{error}</p>}
        {!error && visibleProducts.length === 0 && <p className="p-4 text-muted">{t('cashier.noProducts')}</p>}
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
          {visibleProducts.map((product) => (
            <button
              key={product.id}
              type="button"
              onClick={() => addToCart(product)}
              className={`flex flex-col items-start overflow-hidden rounded-xl border-0 bg-surface p-0 text-start transition-transform active:scale-95 ${
                justAdded === product.id ? 'add-confirm' : ''
              }`}
            >
              <div className="aspect-square w-full bg-surface2">
                {product.iconOrImageUrl ? (
                  <img src={product.iconOrImageUrl} alt={productName(product)} className="h-full w-full object-cover" />
                ) : (
                  <div className="flex h-full w-full items-center justify-center text-3xl">🍵</div>
                )}
              </div>
              <div className="w-full p-2">
                <div className="truncate text-sm font-medium text-text">{productName(product)}</div>
                <div className="font-cairo font-bold text-accent">{product.price.toFixed(3)}</div>
              </div>
            </button>
          ))}
        </div>
      </div>

      <aside className="hidden flex-shrink-0 border-s border-border bg-surface lg:flex lg:w-96">
        {cartContent}
      </aside>

      <div className="lg:hidden">
        {cart.length > 0 && !cartOpen && (
          <button
            type="button"
            className="fixed inset-x-4 bottom-4 z-40 flex items-center justify-between rounded-xl border-0 bg-primary px-4 py-3 text-white shadow-lg"
            onClick={() => setCartOpen(true)}
          >
            <span>
              {itemCount} {t('cashier.items')}
            </span>
            <span className="font-cairo font-bold">{total.toFixed(3)}</span>
          </button>
        )}
        {cartOpen && (
          <div className="fixed inset-0 z-50 flex items-end bg-black/60" onClick={() => setCartOpen(false)}>
            <div
              className="max-h-[85vh] w-full overflow-hidden rounded-t-2xl bg-surface"
              onClick={(e) => e.stopPropagation()}
            >
              {cartContent}
            </div>
          </div>
        )}
      </div>

      {successSale && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={() => setSuccessSale(null)}>
          <div className="rounded-xl bg-surface p-6 text-center" onClick={(e) => e.stopPropagation()}>
            <p className="font-cairo text-xl font-bold text-primary">{t('cashier.saleSuccess')}</p>
            <p className="mt-2 font-cairo text-2xl text-accent">{successSale.totalAmount.toFixed(3)}</p>
            <button type="button" className="mt-4 border-0 bg-primary text-white" onClick={() => setSuccessSale(null)}>
              {t('cashier.close')}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
