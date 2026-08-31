import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { MenuItemDto, ModifierGroupDto } from '../api/types'
import Money from '../components/Money'
import { useToast } from '../components/ToastContext'

type OptionDraft = { nameAr:string; nameEn:string; priceDelta:number; isActive:boolean }
const empty = { nameAr:'', nameEn:'', minSelect:0, maxSelect:1, isRequired:false, options:[{nameAr:'',nameEn:'',priceDelta:0,isActive:true}] as OptionDraft[], menuItemIds:[] as string[] }

export default function ModifiersPage() {
  const { t } = useTranslation(); const toast=useToast(); const [groups,setGroups]=useState<ModifierGroupDto[]>([]); const [items,setItems]=useState<MenuItemDto[]>([])
  const [editing,setEditing]=useState<string|null>(null); const [form,setForm]=useState({...empty,options:[...empty.options],menuItemIds:[] as string[]})
  const singles=useMemo(()=>items.filter(x=>x.kind==='SingleProduct'),[items])
  const load=async()=>{const [g,i]=await Promise.all([api.get<ModifierGroupDto[]>('/api/modifiers'),api.get<MenuItemDto[]>('/api/restaurant-catalog/items')]);setGroups(g);setItems(i)}
  useEffect(()=>{void load()},[])
  const fail=(e:unknown)=>toast.error(e instanceof ApiError?e.message:t('common.saveError'))
  const edit=(g:ModifierGroupDto)=>{setEditing(g.id);setForm({nameAr:g.nameAr,nameEn:g.nameEn,minSelect:g.minSelect,maxSelect:g.maxSelect,isRequired:g.isRequired,options:g.options.map(o=>({nameAr:o.nameAr,nameEn:o.nameEn,priceDelta:o.priceDelta,isActive:o.isActive})),menuItemIds:g.menuItemIds})}
  const reset=()=>{setEditing(null);setForm({...empty,options:[...empty.options],menuItemIds:[]})}
  const save=async()=>{try{const body={...form,options:form.options.map(o=>({...o,id:null}))};if(editing)await api.put(`/api/modifiers/${editing}`,body);else await api.post('/api/modifiers',body);reset();await load();toast.success(t('common.updated'))}catch(e){fail(e)}}
  const remove=async(id:string)=>{try{await api.delete(`/api/modifiers/${id}`);if(editing===id)reset();await load()}catch(e){fail(e)}}
  const patchOption=(index:number,patch:Partial<OptionDraft>)=>setForm(x=>({...x,options:x.options.map((o,i)=>i===index?{...o,...patch}:o)}))
  return <section><h1>{t('modifiers.title')}</h1><p className="text-muted">{t('modifiers.description')}</p>
    <div className="settings-card grid gap-4"><div className="settings-form-grid"><label>{t('restaurant.nameAr')}<input value={form.nameAr} onChange={e=>setForm(x=>({...x,nameAr:e.target.value}))}/></label><label>{t('restaurant.nameEn')}<input value={form.nameEn} onChange={e=>setForm(x=>({...x,nameEn:e.target.value}))}/></label><label>{t('modifiers.minimum')}<input type="number" min="0" value={form.minSelect} onChange={e=>setForm(x=>({...x,minSelect:Number(e.target.value)}))}/></label><label>{t('modifiers.maximum')}<input type="number" min="1" value={form.maxSelect} onChange={e=>setForm(x=>({...x,maxSelect:Number(e.target.value)}))}/></label><label className="checkbox-row"><input type="checkbox" checked={form.isRequired} onChange={e=>setForm(x=>({...x,isRequired:e.target.checked,minSelect:e.target.checked?Math.max(1,x.minSelect):x.minSelect}))}/>{t('restaurant.required')}</label></div>
      <h2>{t('modifiers.options')}</h2>{form.options.map((o,i)=><div className="table-toolbar" key={i}><input placeholder={t('restaurant.nameAr')} value={o.nameAr} onChange={e=>patchOption(i,{nameAr:e.target.value})}/><input placeholder={t('restaurant.nameEn')} value={o.nameEn} onChange={e=>patchOption(i,{nameEn:e.target.value})}/><input type="number" step="0.001" value={o.priceDelta} onChange={e=>patchOption(i,{priceDelta:Number(e.target.value)})}/><button onClick={()=>setForm(x=>({...x,options:x.options.filter((_,n)=>n!==i)}))}>{t('common.delete')}</button></div>)}
      <button onClick={()=>setForm(x=>({...x,options:[...x.options,{nameAr:'',nameEn:'',priceDelta:0,isActive:true}]}))}>{t('modifiers.addOption')}</button>
      <h2>{t('modifiers.products')}</h2><div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">{singles.map(item=><label className="checkbox-row" key={item.id}><input type="checkbox" checked={form.menuItemIds.includes(item.id)} onChange={()=>setForm(x=>({...x,menuItemIds:x.menuItemIds.includes(item.id)?x.menuItemIds.filter(id=>id!==item.id):[...x.menuItemIds,item.id]}))}/>{item.nameAr} / {item.nameEn}</label>)}</div>
      <div className="modal-actions"><button onClick={reset}>{t('common.cancel')}</button><button onClick={save}>{editing?t('common.save'):t('modifiers.addGroup')}</button></div>
    </div>
    <div className="table-shell"><table><thead><tr><th>{t('restaurant.nameAr')}</th><th>{t('restaurant.nameEn')}</th><th>{t('modifiers.rules')}</th><th>{t('modifiers.options')}</th><th>{t('modifiers.products')}</th><th></th></tr></thead><tbody>{groups.map(g=><tr key={g.id}><td>{g.nameAr}</td><td>{g.nameEn}</td><td>{g.minSelect}–{g.maxSelect}{g.isRequired?` · ${t('restaurant.required')}`:''}</td><td>{g.options.map(o=><span className="block" key={o.id}>{o.nameAr} <Money value={o.priceDelta}/></span>)}</td><td>{g.menuItemIds.length}</td><td><div className="row-actions"><button onClick={()=>edit(g)}>{t('common.edit')}</button><button onClick={()=>remove(g.id)}>{t('common.delete')}</button></div></td></tr>)}</tbody></table></div>
  </section>
}
