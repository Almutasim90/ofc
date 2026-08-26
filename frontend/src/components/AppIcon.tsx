import type { SVGProps } from 'react'

export type AppIconName = 'cashier' | 'shift' | 'reports' | 'inventory' | 'products' | 'materials' | 'branches' | 'users' | 'schedule' | 'channels' | 'ai' | 'notifications' | 'settings' | 'logout' | 'menu' | 'more' | 'chevron' | 'close' | 'plus' | 'minus' | 'trash' | 'sun' | 'moon' | 'home'

const paths: Record<AppIconName, JSX.Element> = {
  cashier: <><path d="M4 5h16v14H4z"/><path d="M4 9h16M8 13h2M14 13h2M8 16h2"/></>,
  shift: <><circle cx="12" cy="12" r="8"/><path d="M12 8v5l3 2"/></>,
  reports: <><path d="M5 19V9M12 19V5M19 19v-7"/><path d="M3 19h18"/></>,
  inventory: <><path d="M4 7h16v13H4zM7 4h10l3 3H4l3-3Z"/><path d="M9 12h6"/></>,
  products: <><path d="m12 3 8 4.5v9L12 21l-8-4.5v-9L12 3Z"/><path d="m4 7.5 8 4.5 8-4.5M12 12v9"/></>,
  materials: <><path d="M7 4h10l2 5-7 11L5 9l2-5Z"/><path d="M5 9h14"/></>,
  branches: <><path d="M3 21h18M5 21V8l7-5 7 5v13"/><path d="M9 21v-6h6v6"/></>,
  users: <><circle cx="9" cy="8" r="3"/><path d="M3 20c0-4 2-6 6-6s6 2 6 6M16 5a3 3 0 0 1 0 6M17 14c3 .5 4 2.5 4 6"/></>,
  schedule: <><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M8 3v4M16 3v4M3 10h18M12 14v3l2 1"/></>,
  channels: <><circle cx="6" cy="12" r="2"/><circle cx="18" cy="6" r="2"/><circle cx="18" cy="18" r="2"/><path d="m8 11 8-4M8 13l8 4"/></>,
  ai: <><path d="m12 3 1.4 4.1L17 9l-3.6 1.9L12 15l-1.4-4.1L7 9l3.6-1.9L12 3Z"/><path d="m19 14 .7 2.3L22 17l-2.3.7L19 20l-.7-2.3L16 17l2.3-.7L19 14Z"/></>,
  notifications: <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></>,
  settings: <><circle cx="12" cy="12" r="3"/><path d="M19 13.5v-3l-2-.7-.7-1.7.9-1.9-2.1-2.1-1.9.9-1.7-.7L10.5 2h-3l-.7 2-1.7.7-1.9-.9-2.1 2.1.9 1.9-.7 1.7-2 .7v3l2 .7.7 1.7-.9 1.9 2.1 2.1 1.9-.9 1.7.7.7 2h3l.7-2 1.7-.7 1.9.9 2.1-2.1-.9-1.9.7-1.7 2-.4Z" transform="translate(1.5 0) scale(.88)"/></>,
  logout: <><path d="m10 17 5-5-5-5M15 12H3M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4"/></>,
  menu: <path d="M4 7h16M4 12h16M4 17h16"/>,
  more: <><circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/></>,
  chevron: <path d="m14 6-6 6 6 6"/>,
  close: <path d="M6 6l12 12M18 6 6 18"/>,
  plus: <path d="M12 5v14M5 12h14"/>,
  minus: <path d="M5 12h14"/>,
  trash: <><path d="M4 7h16M9 7V4h6v3M18 7l-1 13H7L6 7"/><path d="M10 11v5M14 11v5"/></>,
  sun: <><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M2 12h2M20 12h2M5 5l1.5 1.5M17.5 17.5 19 19M19 5l-1.5 1.5M6.5 17.5 5 19"/></>,
  moon: <path d="M20 15.5A8 8 0 0 1 8.5 4 8.5 8.5 0 1 0 20 15.5Z"/>,
  home: <><path d="m3 11 9-7 9 7"/><path d="M5 10v9h5v-6h4v6h5v-9"/></>,
}

export default function AppIcon({ name, ...props }: { name: AppIconName } & SVGProps<SVGSVGElement>) {
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" {...props}>{paths[name]}</svg>
}
