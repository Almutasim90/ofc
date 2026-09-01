/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL: string
  readonly VITE_PUBLIC_ORDER_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
