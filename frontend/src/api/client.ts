const LOOPBACK_HOSTS = new Set(['localhost', '127.0.0.1', '::1'])

function getApiUrl() {
  const configuredUrl = import.meta.env.VITE_API_URL?.replace(/\/$/, '') ?? ''
  if (!configuredUrl || typeof window === 'undefined') return configuredUrl

  try {
    const configured = new URL(configuredUrl)
    if (LOOPBACK_HOSTS.has(configured.hostname) && !LOOPBACK_HOSTS.has(window.location.hostname)) {
      configured.hostname = window.location.hostname
      return configured.toString().replace(/\/$/, '')
    }
  } catch {
    return configuredUrl
  }

  return configuredUrl
}

const API_URL = getApiUrl()

export function resolveApiAssetUrl(url: string | null | undefined) {
  if (!url) return ''
  // Only backend-owned paths (uploaded files) need rewriting onto the API
  // origin. Everything else (bundled frontend assets, absolute URLs, data
  // URIs) already resolves correctly against the current page.
  if (!url.startsWith('/uploads/')) return url

  try {
    const resolved = new URL(url, `${API_URL || window.location.origin}/`)
    if (LOOPBACK_HOSTS.has(resolved.hostname) && API_URL) {
      const apiOrigin = new URL(API_URL)
      resolved.protocol = apiOrigin.protocol
      resolved.host = apiOrigin.host
    }
    return resolved.toString()
  } catch {
    return url
  }
}

export const AUTH_STORAGE_KEY = 'pos.auth'

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message)
  }
}

// Read fresh from localStorage on every request rather than caching in a module
// variable set via a React effect - on a full page load, a deeply-nested page's
// data-fetching effect can fire before a top-level effect that sets a cached
// token, since React fires effects bottom-up (children before parents).
function getStoredToken(): string | null {
  try {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY)
    if (!raw) return null
    return JSON.parse(raw)?.token ?? null
  } catch {
    return null
  }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  if (!(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }
  const token = getStoredToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_URL}${path}`, { ...options, headers })

  if (!response.ok) {
    let message = response.statusText
    try {
      const body = await response.json()
      if (body?.error) message = body.error
    } catch {
      // response had no JSON body
    }
    throw new ApiError(message, response.status)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body ? JSON.stringify(body) : undefined }),
  upload: <T>(path: string, body: FormData) => request<T>(path, { method: 'POST', body }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
