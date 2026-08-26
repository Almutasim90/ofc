import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api, ApiError, resolveApiAssetUrl } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import Money from '../components/Money'
import BottomSheet from '../components/BottomSheet'
import AppIcon from '../components/AppIcon'
import Receipt from '../components/Receipt'
import { useToast } from '../components/ToastContext'
import type { BranchDto, CreateSaleRequest, ProductChannelPriceDto, ProductDto, SaleDto, SalesChannelDto, ShiftDto, UpcomingClosingDto } from '../api/types'

interface CartLine {
  productId: string
  nameAr: string
  nameEn: string
  price: number
  quantity: number
}

function CategoryIcon({ category }: { category: string | null }) {
  const iconClass = 'h-5 w-5'

  if (category === null) {
    return <svg className={iconClass} aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><rect x="3" y="3" width="7" height="7" rx="2"/><rect x="14" y="3" width="7" height="7" rx="2"/><rect x="3" y="14" width="7" height="7" rx="2"/><rect x="14" y="14" width="7" height="7" rx="2"/></svg>
  }

  switch (category.toLowerCase()) {
    case 'food':
      return <svg className={iconClass} aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M4 17h16M6 17a6 6 0 0 1 12 0M12 8V6"/><path d="M10 6h4"/><path d="M3 20h18"/></svg>
    case 'sweet':
    case 'sweets':
    case 'dessert':
      return <svg className={iconClass} aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M5 11h14l-1.2 9H6.2L5 11Z"/><path d="M7 11c0-2 1.4-3.5 3.2-3.5.6-2.3 4.6-2.1 4.8.5 1.5 0 2.7 1.3 2.7 3"/><path d="M9 15h.01M15 15h.01"/></svg>
    case 'tea':
      return <svg className={iconClass} aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M5 9h12v5a5 5 0 0 1-5 5h-2a5 5 0 0 1-5-5V9Z"/><path d="M17 11h1.5a2.5 2.5 0 0 1 0 5H17M8 5c0 1 1 1 1 2M12 4c0 1 1 1 1 2"/></svg>
    case 'drink':
    case 'drinks':
    case 'beverage':
    case 'beverages':
      return <svg className={iconClass} aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M7 8h10l-1 12H8L7 8Z"/><path d="m9 4 2 4M11 4h5M9 13h6"/></svg>
    default:
      return <svg className={iconClass} aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M20.59 13.41 12 4.83A2 2 0 0 0 10.59 4.24L4 4a1 1 0 0 0-1 1l.24 6.59a2 2 0 0 0 .59 1.41l8.58 8.58a2 2 0 0 0 2.83 0l5.35-5.35a2 2 0 0 0 0-2.82Z"/><circle cx="7.5" cy="7.5" r="1.1"/></svg>
  }
}

function CashIcon() {
  return <svg className="h-5 w-5" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><rect x="3" y="6" width="18" height="12" rx="2"/><circle cx="12" cy="12" r="2.5"/><path d="M7 9H6v1M17 15h1v-1"/></svg>
}

function CardIcon() {
  return <svg className="h-5 w-5" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18M7 15h4"/></svg>
}

function OnlineOrdersIcon() {
  return <svg className="h-5 w-5" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M4 8h16l-1 12H5L4 8Z"/><path d="M8 8a4 4 0 0 1 8 0M8 13h.01M16 13h.01"/></svg>
}

function StoreIcon() {
  return <svg className="h-5 w-5" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M4 10 5 4h14l1 6"/><path d="M4 10a2 2 0 0 0 4 0 2 2 0 0 0 4 0 2 2 0 0 0 4 0 2 2 0 0 0 4 0"/><path d="M5 10v10h14V10"/><path d="M10 20v-6h4v6"/></svg>
}

export default function CashierPage() {
  const { t, i18n } = useTranslation()
  const { user } = useAuth()
  const toast = useToast()

  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [products, setProducts] = useState<ProductDto[]>([])
  const [channels, setChannels] = useState<SalesChannelDto[]>([])
  const [channelId, setChannelId] = useState('')
  const [channelPrices, setChannelPrices] = useState<Record<string, number>>({})
  const [sidebarView, setSidebarView] = useState<'store' | 'online'>('store')
  const [loading, setLoading] = useState(true)
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null)
  const [cart, setCart] = useState<CartLine[]>([])
  const [cartOpen, setCartOpen] = useState(false)
  const [justAdded, setJustAdded] = useState<string | null>(null)
  const [paymentMethod, setPaymentMethod] = useState<'Cash' | 'Card'>('Cash')
  const [discountType, setDiscountType] = useState<'None' | 'Percentage' | 'FixedAmount'>('None')
  const [discountValue, setDiscountValue] = useState(0)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successSale, setSuccessSale] = useState<SaleDto | null>(null)
  const [receiptHeader, setReceiptHeader] = useState<string | null>(null)
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null)
  const [closingWarning, setClosingWarning] = useState<UpcomingClosingDto | null>(null)

  useEffect(() => {
    const init = async () => {
      try {
        const [branchesData, productsData, shiftData, channelData] = await Promise.all([
          api.get<BranchDto[]>('/api/branches'),
          api.get<ProductDto[]>('/api/products'),
          api.get<ShiftDto | undefined>('/api/shifts/current'),
          api.get<SalesChannelDto[]>('/api/channels?activeOnly=true'),
        ])
        setBranches(branchesData.filter((branch) => branch.isActive))
        setProducts(productsData.filter((product) => product.isActive))
        setBranchId(shiftData?.branchId ?? user?.branchId ?? branchesData.find((branch) => branch.isActive)?.id ?? '')
        setCurrentShift(shiftData ?? null)
        setChannels(channelData)
        setChannelId(channelData.find((channel) => channel.isInStore)?.id ?? '')
      } catch {
        setError(t('cashier.loadError'))
      } finally {
        setLoading(false)
      }
    }
    init()
  }, [t, user])

  useEffect(() => {
    api.get<{ headerText: string | null }>('/api/receipt-settings').then((x) => setReceiptHeader(x.headerText)).catch(() => {})
  }, [])

  useEffect(() => {
    if (!currentShift) { setClosingWarning(null); return }
    const poll = async () => {
      try {
        const [upcoming, shift] = await Promise.all([
          api.get<UpcomingClosingDto>('/api/closing-schedule/upcoming'),
          api.get<ShiftDto | undefined>('/api/shifts/current'),
        ])
        setClosingWarning(upcoming.warning ? upcoming : null)
        if (!shift) {
          setCurrentShift(null)
          setCart([])
          setCartOpen(false)
          setError(t('cashier.autoClosed'))
        }
      } catch { /* the cashier remains usable if this non-critical poll fails */ }
    }
    poll()
    const timer = window.setInterval(poll, 60_000)
    return () => window.clearInterval(timer)
  }, [currentShift, t])

  const categories = useMemo(() => Array.from(new Set(products.map((p) => p.category))), [products])
  const visibleProducts = selectedCategory ? products.filter((p) => p.category === selectedCategory) : products

  const productName = (p: { nameAr: string; nameEn: string }) => (i18n.language === 'ar' ? p.nameAr : p.nameEn)
  const branchName = (b: BranchDto) => (i18n.language === 'ar' ? b.nameAr : b.nameEn)
  const categoryName = (category: string) => {
    const key = `cashier.categories.${category.toLowerCase()}`
    return i18n.exists(key) ? t(key) : category
  }
  const selectedChannel = channels.find((channel) => channel.id === channelId)
  const onlineChannels = channels.filter((channel) => !channel.isInStore)
  const storeChannel = channels.find((channel) => channel.isInStore)
  const selectChannel = async (channel: SalesChannelDto) => {
    const prices = channel.isInStore ? [] : await api.get<ProductChannelPriceDto[]>(`/api/channels/${channel.id}/catalog-prices`)
    setChannelPrices(Object.fromEntries(prices.filter((price) => price.price != null).map((price) => [price.productId, price.price!])))
    setChannelId(channel.id)
    setSelectedCategory(null)
    setCart([])
    setSidebarView(channel.isInStore ? 'store' : 'online')
  }
  const goToStore = () => {
    if (selectedChannel && !selectedChannel.isInStore && storeChannel) {
      void selectChannel(storeChannel)
    } else {
      setSidebarView('store')
    }
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
      return [...prev, { productId: product.id, nameAr: product.nameAr, nameEn: product.nameEn, price: channelPrices[product.id] ?? product.price, quantity: 1 }]
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

  const subtotal = cart.reduce((sum, l) => sum + l.price * l.quantity, 0)
  const discountAmount = discountType === 'Percentage'
    ? Math.min(subtotal, subtotal * discountValue / 100)
    : discountType === 'FixedAmount' ? Math.min(subtotal, discountValue) : 0
  const total = Math.max(0, subtotal - discountAmount)
  const itemCount = cart.reduce((sum, l) => sum + l.quantity, 0)

  const checkout = async () => {
    if (cart.length === 0 || !branchId) return
    setSubmitting(true)
    try {
      const request: CreateSaleRequest = {
        branchId,
        paymentMethod,
        discountType,
        discountValue,
        channelId,
        lines: cart.map((l) => ({ productId: l.productId, quantity: l.quantity })),
      }
      const sale = await api.post<SaleDto>('/api/sales', request)
      setSuccessSale(sale)
      setCart([])
      setDiscountType('None')
      setDiscountValue(0)
      setCartOpen(false)
    } catch (err) {
      if (err instanceof ApiError && err.message.startsWith('Insufficient stock')) {
        toast.error(t('cashier.insufficientStock'))
      } else if (err instanceof ApiError && err.status === 400) {
        toast.error(t('cashier.saleRejected'))
      } else {
        toast.error(t('cashier.saleError'))
      }
    } finally {
      setSubmitting(false)
    }
  }

  const successBranch = branches.find((branch) => branch.id === successSale?.branchId)
  const successBranchName = successBranch ? (i18n.language === 'ar' ? successBranch.nameAr : successBranch.nameEn) : ''

  if (loading) return <p className="p-6 text-muted">{t('common.loading')}</p>

  const cartContent: ReactNode = (
    <div className="cashier-cart flex h-full flex-col">
      <div className="cashier-cart-header flex items-center justify-between border-b border-border p-4">
        <h2 className="font-cairo text-lg font-bold text-text">{t('cashier.cart')}</h2>
        <button
          type="button"
          className="flex min-h-14 min-w-11 items-center justify-center border-0 bg-transparent p-0 text-muted md:hidden"
          onClick={() => setCartOpen(false)}
          aria-label={t('cashier.close')}
        >
          <AppIcon className="h-5 w-5" name="close" />
        </button>
      </div>

      <div className="flex flex-1 flex-col gap-2 overflow-y-auto p-3">
        {cart.length === 0 && <p className="text-sm text-muted">{t('cashier.cartEmpty')}</p>}
        {cart.map((line) => (
          <div key={line.productId} className="cashier-cart-line rounded-xl bg-surface2 p-2.5">
            <div className="cashier-cart-line-heading">
              <div className="truncate text-sm font-medium text-text">{i18n.language === 'ar' ? line.nameAr : line.nameEn}</div>
              <Money className="cashier-cart-line-price text-sm font-bold text-accent" value={line.price} />
            </div>
            <div className="cashier-cart-line-actions">
              <div className="cashier-cart-quantity">
                <button
                  type="button"
                  className="cashier-quantity-button flex items-center justify-center rounded-lg border border-border bg-surface p-0 text-text"
                  onClick={() => updateQuantity(line.productId, line.quantity - 1)}
                >
                  <AppIcon className="h-4 w-4" name="minus" />
                </button>
                <span className="w-7 text-center font-cairo text-sm font-bold text-text">{line.quantity}</span>
                <button
                  type="button"
                  className="cashier-quantity-button flex items-center justify-center rounded-lg border border-border bg-surface p-0 text-text"
                  onClick={() => updateQuantity(line.productId, line.quantity + 1)}
                >
                  <AppIcon className="h-4 w-4" name="plus" />
                </button>
              </div>
              <button
                type="button"
                className="cashier-remove flex items-center justify-center border-0 bg-transparent p-0 text-danger"
                onClick={() => removeFromCart(line.productId)}
              >
                <AppIcon className="h-4 w-4" name="trash" />
              </button>
            </div>
          </div>
        ))}
      </div>

      <div className="flex flex-col gap-2.5 border-t border-border p-3">
        <div className="cashier-discount-control">
          <label>{t('cashier.discountType')}
            <select value={discountType} onChange={(event) => { setDiscountType(event.target.value as typeof discountType); setDiscountValue(0) }}>
              <option value="None">{t('cashier.noDiscount')}</option>
              <option value="Percentage">{t('cashier.percentageDiscount')}</option>
              <option value="FixedAmount">{t('cashier.fixedDiscount')}</option>
            </select>
          </label>
          {discountType !== 'None' && <label>{t('cashier.discountValue')}
            <input type="number" min="0" max={discountType === 'Percentage' ? 100 : subtotal} step="0.001" value={discountValue} onChange={(event) => setDiscountValue(Math.max(0, Number(event.target.value) || 0))} />
          </label>}
        </div>
        {discountAmount > 0 && <div className="flex items-center justify-between text-sm text-muted"><span>{t('cashier.discount')}</span><Money value={discountAmount} /></div>}
        <div className="flex flex-col gap-2 text-sm text-muted">
          {t('cashier.paymentMethod')}
          <div className="flex gap-2">
            <button
              type="button"
              className={
                paymentMethod === 'Cash'
                  ? 'text-on-primary inline-flex min-h-14 flex-1 items-center justify-center gap-2 border-0 bg-primary active:scale-[0.98]'
                  : 'inline-flex min-h-14 flex-1 items-center justify-center gap-2 border border-border bg-surface2 text-text active:scale-[0.98]'
              }
              onClick={() => setPaymentMethod('Cash')}
            >
              <CashIcon />
              <span>{t('cashier.cash')}</span>
            </button>
            <button
              type="button"
              className={
                paymentMethod === 'Card'
                  ? 'text-on-primary inline-flex min-h-14 flex-1 items-center justify-center gap-2 border-0 bg-primary active:scale-[0.98]'
                  : 'inline-flex min-h-14 flex-1 items-center justify-center gap-2 border border-border bg-surface2 text-text active:scale-[0.98]'
              }
              onClick={() => setPaymentMethod('Card')}
            >
              <CardIcon />
              <span>{t('cashier.card')}</span>
            </button>
          </div>
        </div>

        <div className="flex items-center justify-between font-cairo text-lg font-bold text-text">
          <span>{t('cashier.total')}</span>
          <Money className="text-accent" value={total} />
        </div>

        {error && <p className="text-sm text-danger">{error}</p>}

        <button
          type="button"
          className="text-on-primary min-h-14 w-full border-0 bg-primary active:scale-[0.98]"
          disabled={cart.length === 0 || submitting}
          onClick={checkout}
        >
          {submitting ? t('cashier.submitting') : t('cashier.confirm')}
        </button>
      </div>
    </div>
  )

  return (
    <div className="cashier-theme flex h-full min-h-0 w-full flex-col overflow-hidden text-text md:flex-row">
      <aside className="cashier-categories flex flex-shrink-0 items-center gap-2 overflow-x-auto border-b border-border bg-surface px-3 py-2 md:w-24 md:flex-col md:overflow-x-hidden md:overflow-y-auto md:border-b-0 md:border-e md:px-2 md:py-4">
        {sidebarView === 'store' ? (
          <div key="store" className="cashier-sidebar-panel flex items-center gap-2 md:flex-col md:items-stretch">
            <button
              type="button"
              className={`cashier-category-button flex min-h-14 min-w-[4.5rem] flex-col items-center gap-2 border-0 bg-transparent p-2 ${selectedCategory === null ? 'is-selected' : ''}`}
              onClick={() => setSelectedCategory(null)}
            >
              <span
                className={`cashier-category-icon flex h-9 w-9 items-center justify-center rounded-xl md:h-10 md:w-10 ${
                  selectedCategory === null ? 'text-on-primary bg-primary' : 'bg-surface2 text-text'
                }`}
              >
                <CategoryIcon category={null} />
              </span>
              <span className={`text-xs ${selectedCategory === null ? 'text-primary' : 'text-muted'}`}>
                {t('cashier.allCategories')}
              </span>
            </button>
            {categories.map((cat) => (
              <button
                key={cat}
                type="button"
                className={`cashier-category-button flex min-h-14 min-w-[4.5rem] flex-col items-center gap-2 border-0 bg-transparent p-2 ${selectedCategory === cat ? 'is-selected' : ''}`}
                onClick={() => setSelectedCategory(cat)}
              >
                <span
                  className={`cashier-category-icon flex h-9 w-9 items-center justify-center rounded-xl md:h-10 md:w-10 ${
                    selectedCategory === cat ? 'text-on-primary bg-primary' : 'bg-surface2 text-text'
                  }`}
                >
                  <CategoryIcon category={cat} />
                </span>
                <span className={`text-xs ${selectedCategory === cat ? 'text-primary' : 'text-muted'}`}>
                  {categoryName(cat)}
                </span>
              </button>
            ))}
            {onlineChannels.length > 0 && (
              <button
                type="button"
                className="cashier-category-button flex min-h-14 min-w-[4.5rem] flex-col items-center gap-2 border-0 bg-transparent p-2"
                onClick={() => setSidebarView('online')}
              >
                <span className="cashier-category-icon flex h-9 w-9 items-center justify-center rounded-xl bg-surface2 text-text md:h-10 md:w-10"><OnlineOrdersIcon /></span>
                <span className="text-xs text-muted">{t('cashier.onlineOrders')}</span>
              </button>
            )}
          </div>
        ) : (
          <div key="online" className="cashier-sidebar-panel flex items-center gap-2 md:flex-col md:items-stretch">
            <button type="button" className="cashier-channel-back flex min-h-14 min-w-[4.5rem] flex-col items-center gap-2 border-0 bg-transparent p-2" onClick={goToStore}>
              <span className="cashier-category-icon flex h-9 w-9 items-center justify-center rounded-xl md:h-10 md:w-10"><StoreIcon /></span>
              <span className="text-xs">{t('cashier.backToStore')}</span>
            </button>
            {onlineChannels.map((channel) => (
              <button
                key={channel.id}
                type="button"
                className={`cashier-category-button flex min-h-14 min-w-[4.5rem] flex-col items-center gap-2 border-0 bg-transparent p-2 ${channel.id === channelId ? 'is-selected' : ''}`}
                onClick={() => void selectChannel(channel)}
              >
                <span className={`cashier-category-icon flex h-9 w-9 items-center justify-center overflow-hidden rounded-xl md:h-10 md:w-10 ${channel.id === channelId ? 'text-on-primary bg-primary' : 'bg-surface2 text-text'}`}>
                  {channel.logoUrl ? <img src={resolveApiAssetUrl(channel.logoUrl)} alt="" className="h-full w-full object-contain" /> : <OnlineOrdersIcon />}
                </span>
                <span className={`text-xs ${channel.id === channelId ? 'text-primary' : 'text-muted'}`}>{productName(channel)}</span>
              </button>
            ))}
          </div>
        )}
      </aside>

      <div className="cashier-catalog flex flex-1 flex-col gap-4 overflow-y-auto p-3 pb-24 sm:p-4 xl:pb-4">
        {selectedChannel && !selectedChannel.isInStore && <div className="cashier-channel-context"><span>{selectedChannel.logoUrl && <img src={resolveApiAssetUrl(selectedChannel.logoUrl)} alt="" />}{t('cashier.currentOrder')}: <strong>{productName(selectedChannel)}</strong></span><button type="button" onClick={goToStore}>{t('cashier.backToStore')}</button></div>}
        {closingWarning && (
          <div className="rounded-xl border border-primary bg-surface p-3 text-primary">
            {t('cashier.closingWarning', { minutes: closingWarning.minutesRemaining })}
          </div>
        )}
        {!currentShift && (
          <div className="flex items-center justify-between gap-3 rounded-xl border border-danger bg-surface p-3 text-danger">
            <span>{t('cashier.shiftRequired')}</span>
            <Link className="inline-flex min-h-14 items-center rounded-xl bg-primary px-3 py-2 font-bold text-bg" to="/shift">{t('cashier.openShift')}</Link>
          </div>
        )}
        {branches.length > 0 && (
          <div className="cashier-toolbar flex flex-wrap items-end justify-between gap-3 rounded-2xl border border-border bg-surface p-3 sm:p-4">
          <label className="flex w-full max-w-xs flex-col gap-1 text-sm font-semibold text-muted sm:w-auto sm:min-w-64">
            {t('cashier.branch')}
            <select value={branchId} disabled={Boolean(user?.branchId)} onChange={(e) => setBranchId(e.target.value)}>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {branchName(b)}
                </option>
              ))}
            </select>
          </label>
          <div className="cashier-products-count"><strong>{visibleProducts.length}</strong><span>{t('nav.products')}</span></div>
          </div>
        )}

        {error && products.length === 0 && <p className="p-4 text-danger">{error}</p>}
        {!error && visibleProducts.length === 0 && <p className="p-4 text-muted">{t('cashier.noProducts')}</p>}
        <div className="cashier-products-grid">
          {visibleProducts.map((product) => (
            <button
              key={product.id}
              type="button"
              onClick={() => addToCart(product)}
              className={`product-card group relative flex min-w-0 flex-col items-start overflow-hidden rounded-2xl border border-border bg-surface p-0 text-start active:scale-[0.98] ${
                justAdded === product.id ? 'add-confirm' : ''
              }`}
            >
              <div className="product-card-image aspect-square w-full bg-surface2">
                {product.iconOrImageUrl ? (
                  <img src={resolveApiAssetUrl(product.iconOrImageUrl)} alt={productName(product)} className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105" />
                ) : (
                  <div className="flex h-full w-full items-center justify-center font-cairo text-3xl font-bold text-primary">{productName(product).charAt(0)}</div>
                )}
              </div>
              <div className="flex w-full flex-col gap-1 p-3">
                <div className="truncate text-sm font-bold text-text">{productName(product)}</div>
                <Money className="font-bold text-primary" value={channelPrices[product.id] ?? product.price} />
              </div>
              <span className="product-add-indicator text-on-primary absolute end-2 top-2 flex h-8 w-8 items-center justify-center rounded-full bg-primary shadow-lg" aria-hidden="true"><AppIcon className="h-4 w-4" name="plus" /></span>
            </button>
          ))}
        </div>
      </div>

      <aside className="cashier-cart-panel hidden flex-shrink-0 border-s border-border bg-surface md:flex">
        {cartContent}
      </aside>

      <div className="md:hidden">
        {cart.length > 0 && !cartOpen && (
          <button
            type="button"
            className="cashier-cart-fab text-on-primary fixed inset-x-4 bottom-4 z-40 flex items-center justify-between rounded-2xl border-0 bg-primary px-4 py-3 shadow-lg md:start-auto md:w-80"
            onClick={() => setCartOpen(true)}
          >
            <span>
              {itemCount} {t('cashier.items')}
            </span>
            <Money className="font-bold" value={total} />
          </button>
        )}
        <BottomSheet open={cartOpen} onClose={() => setCartOpen(false)}>{cartContent}</BottomSheet>
      </div>

      {successSale && (
        <div className="app-scrim fixed inset-0 z-50 flex items-center justify-center" onClick={() => setSuccessSale(null)}>
          <div className="flex flex-col gap-4 rounded-3xl bg-surface p-6 text-center" onClick={(e) => e.stopPropagation()}>
            <p className="font-cairo text-xl font-bold text-primary">{t('cashier.saleSuccess')}</p>
            <p className="text-2xl text-accent"><Money value={successSale.totalAmount} /></p>
            <button type="button" className="min-h-14" onClick={() => window.print()}>
              {t('receipt.print')}
            </button>
            <button type="button" className="text-on-primary min-h-14 border-0 bg-primary" onClick={() => setSuccessSale(null)}>
              {t('cashier.close')}
            </button>
          </div>
          <Receipt sale={successSale} headerText={receiptHeader} branchName={successBranchName} cashierName={user?.fullName ?? ''} />
        </div>
      )}
    </div>
  )
}
