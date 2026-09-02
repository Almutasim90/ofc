import { useEffect, useMemo, useRef, useState, type HTMLAttributes, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { SearchBox } from './TableTools'

export type SortDirection = 'asc' | 'desc'

export interface DataTableColumn<T> {
  id: string
  header: ReactNode
  cell: (row: T, index: number) => ReactNode
  sortValue?: (row: T) => string | number | boolean | Date | null | undefined
  className?: string
}

interface DataTableProps<T> {
  rows: T[]
  columns: DataTableColumn<T>[]
  getRowKey: (row: T, index: number) => string | number
  getSearchText?: (row: T) => string
  loading?: boolean
  emptyMessage?: ReactNode
  pageSize?: number
  defaultSort?: { id: string; direction: SortDirection }
  queryPrefix?: string
  toolbar?: ReactNode
  searchPlaceholder?: string
  shellClassName?: string
  tableClassName?: string
  rowProps?: (row: T, index: number) => HTMLAttributes<HTMLTableRowElement>
}

export default function DataTable<T>({
  rows,
  columns,
  getRowKey,
  getSearchText,
  loading = false,
  emptyMessage = '—',
  pageSize = 10,
  defaultSort,
  queryPrefix = '',
  toolbar,
  searchPlaceholder,
  shellClassName,
  tableClassName,
  rowProps,
}: DataTableProps<T>) {
  const { t, i18n } = useTranslation()
  const [searchParams, setSearchParams] = useSearchParams()
  const parameter = (name: string) => queryPrefix ? `${queryPrefix}-${name}` : name
  const initialSortId = searchParams.get(parameter('sort')) ?? defaultSort?.id ?? ''
  const initialDirection = searchParams.get(parameter('dir')) === 'asc' ? 'asc' : searchParams.get(parameter('dir')) === 'desc' ? 'desc' : defaultSort?.direction ?? 'asc'
  const initialPage = Math.max(1, Number(searchParams.get(parameter('page'))) || 1)
  const [searchInput, setSearchInput] = useState(searchParams.get(parameter('q')) ?? '')
  const [search, setSearch] = useState(searchInput)
  const [sort, setSort] = useState({ id: initialSortId, direction: initialDirection as SortDirection })
  const [page, setPage] = useState(initialPage)
  const initializedSearch = useRef(false)

  const updateQuery = (updates: Record<string, string | null>) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      Object.entries(updates).forEach(([key, value]) => value ? next.set(parameter(key), value) : next.delete(parameter(key)))
      return next
    }, { replace: true })
  }

  useEffect(() => {
    if (!initializedSearch.current) {
      initializedSearch.current = true
      return
    }
    const timeout = window.setTimeout(() => {
      setSearch(searchInput)
      setPage(1)
      updateQuery({ q: searchInput.trim() || null, page: null })
    }, 300)
    return () => window.clearTimeout(timeout)
    // Query parameter names are fixed for the lifetime of a mounted table.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchInput])

  useEffect(() => {
    const urlSearch = searchParams.get(parameter('q')) ?? ''
    const urlSort = searchParams.get(parameter('sort')) ?? defaultSort?.id ?? ''
    const directionValue = searchParams.get(parameter('dir'))
    const urlDirection = directionValue === 'asc' || directionValue === 'desc' ? directionValue : defaultSort?.direction ?? 'asc'
    const urlPage = Math.max(1, Number(searchParams.get(parameter('page'))) || 1)
    if (urlSearch !== search) {
      initializedSearch.current = false
      setSearchInput(urlSearch)
      setSearch(urlSearch)
    }
    if (urlSort !== sort.id || urlDirection !== sort.direction) setSort({ id: urlSort, direction: urlDirection })
    if (urlPage !== page) setPage(urlPage)
    // Local state is intentionally excluded: this effect responds only to URL navigation.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams])

  const processedRows = useMemo(() => {
    const query = search.trim().toLocaleLowerCase(i18n.language)
    const filtered = query && getSearchText
      ? rows.filter((row) => getSearchText(row).toLocaleLowerCase(i18n.language).includes(query))
      : rows
    const column = columns.find((candidate) => candidate.id === sort.id && candidate.sortValue)
    if (!column?.sortValue) return filtered.map((row, sourceIndex) => ({ row, sourceIndex }))
    const direction = sort.direction === 'asc' ? 1 : -1
    return filtered.map((row, sourceIndex) => ({ row, sourceIndex })).sort((a, b) => {
      const left = column.sortValue?.(a.row)
      const right = column.sortValue?.(b.row)
      if (left == null && right == null) return a.sourceIndex - b.sourceIndex
      if (left == null) return direction
      if (right == null) return -direction
      const result = typeof left === 'string' && typeof right === 'string'
        ? left.localeCompare(right, i18n.language, { numeric: true, sensitivity: 'base' })
        : Number(left instanceof Date ? left.getTime() : left) - Number(right instanceof Date ? right.getTime() : right)
      return result === 0 ? a.sourceIndex - b.sourceIndex : result * direction
    })
  }, [columns, getSearchText, i18n.language, rows, search, sort])

  const pageCount = pageSize > 0 ? Math.max(1, Math.ceil(processedRows.length / pageSize)) : 1
  const currentPage = Math.min(page, pageCount)
  const visibleRows = pageSize > 0 ? processedRows.slice((currentPage - 1) * pageSize, currentPage * pageSize) : processedRows

  useEffect(() => {
    if (!loading && page > pageCount) {
      setPage(pageCount)
      updateQuery({ page: pageCount > 1 ? String(pageCount) : null })
    }
    // updateQuery is recreated because useSearchParams supplies the current URL.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loading, page, pageCount])

  const changeSort = (id: string) => {
    const direction = sort.id === id && sort.direction === 'asc' ? 'desc' : 'asc'
    setSort({ id, direction })
    setPage(1)
    updateQuery({ sort: id, dir: direction, page: null })
  }

  const changePage = (nextPage: number) => {
    setPage(nextPage)
    updateQuery({ page: nextPage > 1 ? String(nextPage) : null })
  }

  const placeholder = searchPlaceholder ?? t('common.search')
  return <div className="data-table">
    {(getSearchText || toolbar) && <div className="table-toolbar">
      {getSearchText && <SearchBox value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder={placeholder} />}
      {toolbar}
    </div>}
    <div className={`table-shell${shellClassName ? ` ${shellClassName}` : ''}`} aria-busy={loading}>
      <table className={tableClassName}>
        <thead><tr>{columns.map((column) => {
          const sortable = !!column.sortValue
          const active = sort.id === column.id
          return <th key={column.id} className={column.className} aria-sort={sortable ? (active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none') : undefined}>
            {sortable ? <button type="button" className="data-table-sort" onClick={() => changeSort(column.id)}>
              <span>{column.header}</span><span aria-hidden="true">{active ? (sort.direction === 'asc' ? '↑' : '↓') : '↕'}</span>
            </button> : column.header}
          </th>
        })}</tr></thead>
        <tbody>
          {loading ? <tr><td className="data-table-message" role="status" colSpan={columns.length}>{t('common.loading')}</td></tr>
            : visibleRows.length === 0 ? <tr><td className="data-table-message" role="status" colSpan={columns.length}>{emptyMessage}</td></tr>
              : visibleRows.map(({ row, sourceIndex }) => <tr key={getRowKey(row, sourceIndex)} {...rowProps?.(row, sourceIndex)}>
                {columns.map((column) => <td key={column.id} className={column.className}>{column.cell(row, sourceIndex)}</td>)}
              </tr>)}
        </tbody>
      </table>
    </div>
    {!loading && pageSize > 0 && processedRows.length > 0 && <nav className="pagination" aria-label={t('products.pagination')}>
      <span className="pagination-summary">{(currentPage - 1) * pageSize + 1}–{Math.min(currentPage * pageSize, processedRows.length)} / {processedRows.length}</span>
      <div className="pagination-controls">
        <button type="button" onClick={() => changePage(currentPage - 1)} disabled={currentPage === 1}>{t('common.previous')}</button>
        <span>{currentPage} / {pageCount}</span>
        <button type="button" onClick={() => changePage(currentPage + 1)} disabled={currentPage === pageCount}>{t('common.next')}</button>
      </div>
    </nav>}
  </div>
}
