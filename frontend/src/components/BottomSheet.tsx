import type { ReactNode } from 'react'
export default function BottomSheet({open,onClose,children}:{open:boolean;onClose:()=>void;children:ReactNode}){if(!open)return null;return <div className="bottom-sheet-backdrop app-scrim" onClick={onClose}><div className="bottom-sheet-panel" onClick={event=>event.stopPropagation()}><div className="bottom-sheet-handle"/>{children}</div></div>}
