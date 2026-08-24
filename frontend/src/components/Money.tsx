export function OmaniRialSymbol({ className = '' }: { className?: string }) {
  return <svg aria-label="ريال عماني" role="img" viewBox="0 0 330 172" className={`inline-block h-[0.8em] w-[1.35em] shrink-0 fill-current ${className}`}><path d="M0 172l20-35h123l-23-31H34l20-22h63c-3-29 5-55 25-71 24-20 54-16 77 12l-14 55c-18-21-34-33-51-33-14 0-24 7-25 19-1 8 4 17 13 26 21 22 50 45 91 45h63l-20 35H0zm143-88h187l-20 35H170l-27-35z"/></svg>
}

export default function Money({ value, className = '' }: { value: number; className?: string }) {
  return <span className={`inline-flex items-baseline gap-1 font-cairo tabular-nums ${className}`}><span>{value.toFixed(3)}</span><OmaniRialSymbol /></span>
}
