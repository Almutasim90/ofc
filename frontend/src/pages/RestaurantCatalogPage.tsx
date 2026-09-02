import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, BranchFeatureFlagDto, ComboComponentDto, MenuCategoryDto, MenuItemDto, RestaurantTableDto } from '../api/types'
import DataTable from '../components/DataTable'
import Money from '../components/Money'
import { useToast } from '../components/ToastContext'

type Tab = 'categories' | 'items' | 'combos' | 'branches'
type SlotDraft = { slotLabel:string; isRequired:boolean; minSelect:number; maxSelect:number; sortOrder:number; options:{menuItemId:string;priceDelta:number;isDefault:boolean}[] }

export default function RestaurantCatalogPage() {
  const { t } = useTranslation(); const toast = useToast()
  const [tab, setTab] = useState<Tab>('categories'); const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState(''); const [categories, setCategories] = useState<MenuCategoryDto[]>([])
  const [items, setItems] = useState<MenuItemDto[]>([]); const [tables, setTables] = useState<RestaurantTableDto[]>([])
  const [flags, setFlags] = useState<BranchFeatureFlagDto[]>([]); const [dragged, setDragged] = useState<string|null>(null)
  const [categoryForm, setCategoryForm] = useState({ nameAr:'', nameEn:'' })
  const [itemForm, setItemForm] = useState({ categoryId:'', nameAr:'', nameEn:'', kind:'SingleProduct' as 'SingleProduct'|'Combo', basePrice:'0' })
  const [tableForm, setTableForm] = useState({ label:'', capacity:'4' }); const [featureKey, setFeatureKey] = useState('CAR_PICKUP')
  const [comboId, setComboId] = useState(''); const [slots, setSlots] = useState<SlotDraft[]>([])

  const fail = (error:unknown) => toast.error(error instanceof ApiError ? error.message : t('common.saveError'))
  const load = useCallback(async () => {
    const branchRows = await api.get<BranchDto[]>('/api/branches'); setBranches(branchRows)
    const selected = branchId || branchRows[0]?.id || ''; if (!branchId && selected) setBranchId(selected)
    const [categoryRows,itemRows] = await Promise.all([
      api.get<MenuCategoryDto[]>(`/api/restaurant-catalog/categories${selected ? `?branchId=${selected}` : ''}`),
      api.get<MenuItemDto[]>('/api/restaurant-catalog/items'),
    ]); setCategories(categoryRows); setItems(itemRows)
    if (!itemForm.categoryId && categoryRows[0]) setItemForm(x => ({...x,categoryId:categoryRows[0].id}))
    if (selected) { const [tableRows,flagRows] = await Promise.all([api.get<RestaurantTableDto[]>(`/api/restaurant-catalog/tables?branchId=${selected}`),api.get<BranchFeatureFlagDto[]>(`/api/restaurant-catalog/branches/${selected}/features`)]); setTables(tableRows); setFlags(flagRows) }
  }, [branchId, itemForm.categoryId])
  useEffect(() => { void load() }, [load])

  const saveCategory = async () => { try { await api.post('/api/restaurant-catalog/categories',{...categoryForm,sortOrder:categories.length,isActive:true}); setCategoryForm({nameAr:'',nameEn:''}); await load(); toast.success(t('common.created')) } catch(e){fail(e)} }
  const reorder = async (target:string) => { if (!dragged || dragged===target) return; const next=[...categories]; const from=next.findIndex(x=>x.id===dragged),to=next.findIndex(x=>x.id===target); const [row]=next.splice(from,1); next.splice(to,0,row); setCategories(next); setDragged(null); try{await api.put('/api/restaurant-catalog/categories/reorder',{categoryIds:next.map(x=>x.id)})}catch(e){fail(e);await load()} }
  const availability = async (row:MenuCategoryDto) => { try{await api.put(`/api/restaurant-catalog/categories/${row.id}/branches/${branchId}`,{isAvailable:!row.isAvailable});await load()}catch(e){fail(e)} }
  const toggleCategoryActive = async (row:MenuCategoryDto) => { try{await api.put(`/api/restaurant-catalog/categories/${row.id}`,{nameAr:row.nameAr,nameEn:row.nameEn,sortOrder:row.sortOrder,isActive:!row.isActive});await load()}catch(e){fail(e)} }
  const saveItem = async () => { try{await api.post('/api/restaurant-catalog/items',{...itemForm,basePrice:Number(itemForm.basePrice),imageUrl:null,sortOrder:items.filter(x=>x.categoryId===itemForm.categoryId).length,isActive:true});setItemForm(x=>({...x,nameAr:'',nameEn:'',basePrice:'0'}));await load();toast.success(t('common.created'))}catch(e){fail(e)} }
  const toggleItemActive = async (row:MenuItemDto) => { try{await api.put(`/api/restaurant-catalog/items/${row.id}`,{categoryId:row.categoryId,nameAr:row.nameAr,nameEn:row.nameEn,kind:row.kind,basePrice:row.basePrice,imageUrl:row.imageUrl,sortOrder:row.sortOrder,isActive:!row.isActive});await load()}catch(e){fail(e)} }
  const saveTable = async () => { try{await api.post('/api/restaurant-catalog/tables',{branchId,label:tableForm.label,capacity:Number(tableForm.capacity),isActive:true});setTableForm({label:'',capacity:'4'});await load();toast.success(t('common.created'))}catch(e){fail(e)} }
  const toggleTableActive = async (row:RestaurantTableDto) => { try{await api.put(`/api/restaurant-catalog/tables/${row.id}`,{branchId:row.branchId,label:row.label,capacity:row.capacity,isActive:!row.isActive,floorId:row.floorId,positionX:row.positionX,positionY:row.positionY,shape:row.shape});await load()}catch(e){fail(e)} }
  const setFlag = async (key:string,enabled:boolean) => { try{await api.put(`/api/restaurant-catalog/branches/${branchId}/features/${encodeURIComponent(key)}`,{isEnabled:enabled});await load()}catch(e){fail(e)} }
  const loadCombo = async (id:string) => { setComboId(id); if(!id){setSlots([]);return} try{const rows=await api.get<ComboComponentDto[]>(`/api/restaurant-catalog/combos/${id}`);setSlots(rows.map(x=>({slotLabel:x.slotLabel,isRequired:x.isRequired,minSelect:x.minSelect,maxSelect:x.maxSelect,sortOrder:x.sortOrder,options:x.options.map(o=>({menuItemId:o.menuItemId,priceDelta:o.priceDelta,isDefault:o.isDefault}))})))}catch(e){fail(e)} }
  const saveCombo = async () => { try{await api.put(`/api/restaurant-catalog/combos/${comboId}`,{components:slots.map((slot,index)=>({...slot,sortOrder:index}))});toast.success(t('common.updated'))}catch(e){fail(e)} }
  const singles=useMemo(()=>items.filter(x=>x.kind==='SingleProduct'&&x.isActive),[items]); const combos=items.filter(x=>x.kind==='Combo')
  const addSlot=()=>setSlots(x=>[...x,{slotLabel:'',isRequired:true,minSelect:1,maxSelect:1,sortOrder:x.length,options:[]}])
  const patchSlot=(index:number,patch:Partial<SlotDraft>)=>setSlots(x=>x.map((s,i)=>i===index?{...s,...patch}:s))
  const toggleOption=(slotIndex:number,itemId:string)=>setSlots(current=>current.map((slot,index)=>{if(index!==slotIndex)return slot;const removed=slot.options.find(option=>option.menuItemId===itemId);if(!removed)return{...slot,options:[...slot.options,{menuItemId:itemId,priceDelta:0,isDefault:slot.options.length===0}]};const options=slot.options.filter(option=>option.menuItemId!==itemId);if(removed.isDefault&&options.length)options[0]={...options[0],isDefault:true};return{...slot,options}}))
  const moveSlot=(index:number,offset:number)=>setSlots(current=>{const target=index+offset;if(target<0||target>=current.length)return current;const next=[...current];[next[index],next[target]]=[next[target],next[index]];return next.map((slot,sortOrder)=>({...slot,sortOrder}))})

  return <section><h1>{t('restaurant.title')}</h1><p className="text-muted">{t('restaurant.description')}</p>
    <div className="table-toolbar"><div className="row-actions">{(['categories','items','combos','branches'] as Tab[]).map(x=><button key={x} className={tab===x?'is-active':''} onClick={()=>setTab(x)}>{t(`restaurant.${x}`)}</button>)}</div><label>{t('restaurant.branch')}<select value={branchId} onChange={e=>setBranchId(e.target.value)}>{branches.map(x=><option key={x.id} value={x.id}>{x.nameAr} / {x.nameEn}</option>)}</select></label></div>
    {tab==='categories'&&<><div className="settings-form-grid"><label>{t('restaurant.nameAr')}<input value={categoryForm.nameAr} onChange={e=>setCategoryForm(x=>({...x,nameAr:e.target.value}))}/></label><label>{t('restaurant.nameEn')}<input value={categoryForm.nameEn} onChange={e=>setCategoryForm(x=>({...x,nameEn:e.target.value}))}/></label><button onClick={saveCategory} disabled={!categoryForm.nameAr||!categoryForm.nameEn}>{t('restaurant.addCategory')}</button></div><DataTable rows={categories} pageSize={0} queryPrefix="categories" getRowKey={x=>x.id} getSearchText={x=>`${x.nameAr} ${x.nameEn}`} rowProps={x=>({draggable:true,onDragStart:()=>setDragged(x.id),onDragOver:e=>e.preventDefault(),onDrop:()=>reorder(x.id)})} columns={[
      {id:'order',header:t('restaurant.order'),cell:(_,i)=>`↕ ${i+1}`},
      {id:'nameAr',header:t('restaurant.nameAr'),cell:x=>x.nameAr},
      {id:'nameEn',header:t('restaurant.nameEn'),cell:x=>x.nameEn},
      {id:'active',header:t('restaurant.active'),cell:x=><input type="checkbox" checked={x.isActive} onChange={()=>toggleCategoryActive(x)}/>},
      {id:'available',header:t('restaurant.branchAvailable'),cell:x=><input type="checkbox" checked={x.isAvailable} onChange={()=>availability(x)}/>},
    ]}/></>}
    {tab==='items'&&<><div className="settings-form-grid"><label>{t('restaurant.category')}<select value={itemForm.categoryId} onChange={e=>setItemForm(x=>({...x,categoryId:e.target.value}))}>{categories.map(x=><option key={x.id} value={x.id}>{x.nameAr} / {x.nameEn}</option>)}</select></label><label>{t('restaurant.nameAr')}<input value={itemForm.nameAr} onChange={e=>setItemForm(x=>({...x,nameAr:e.target.value}))}/></label><label>{t('restaurant.nameEn')}<input value={itemForm.nameEn} onChange={e=>setItemForm(x=>({...x,nameEn:e.target.value}))}/></label><label>{t('restaurant.kind')}<select value={itemForm.kind} onChange={e=>setItemForm(x=>({...x,kind:e.target.value as 'SingleProduct'|'Combo'}))}><option value="SingleProduct">{t('restaurant.single')}</option><option value="Combo">{t('restaurant.combo')}</option></select></label><label>{t('restaurant.price')}<input type="number" min="0" step="0.001" value={itemForm.basePrice} onChange={e=>setItemForm(x=>({...x,basePrice:e.target.value}))}/></label><button onClick={saveItem}>{t('restaurant.addItem')}</button></div><DataTable rows={items} queryPrefix="items" getRowKey={x=>x.id} getSearchText={x=>`${x.nameAr} ${x.nameEn} ${x.kind}`} columns={[
      {id:'nameAr',header:t('restaurant.nameAr'),cell:x=>x.nameAr,sortValue:x=>x.nameAr},
      {id:'nameEn',header:t('restaurant.nameEn'),cell:x=>x.nameEn,sortValue:x=>x.nameEn},
      {id:'kind',header:t('restaurant.kind'),cell:x=>t(x.kind==='Combo'?'restaurant.combo':'restaurant.single'),sortValue:x=>x.kind},
      {id:'price',header:t('restaurant.price'),cell:x=><Money value={x.basePrice}/>,sortValue:x=>x.basePrice},
      {id:'active',header:t('restaurant.active'),cell:x=><input type="checkbox" checked={x.isActive} onChange={()=>toggleItemActive(x)}/>,sortValue:x=>x.isActive},
    ]}/></>}
    {tab==='combos'&&<><label>{t('restaurant.selectCombo')}<select value={comboId} onChange={e=>loadCombo(e.target.value)}><option value="">—</option>{combos.map(x=><option key={x.id} value={x.id}>{x.nameAr} / {x.nameEn}</option>)}</select></label>{comboId&&<><div className="grid gap-4">{slots.map((slot,i)=><div className="settings-card" key={i}><div className="table-toolbar"><input placeholder={t('restaurant.slotLabel')} value={slot.slotLabel} onChange={e=>patchSlot(i,{slotLabel:e.target.value})}/><label>{t('restaurant.minimum')}<input type="number" min="0" max={slot.options.length} value={slot.minSelect} onChange={e=>patchSlot(i,{minSelect:Number(e.target.value)})}/></label><label>{t('restaurant.maximum')}<input type="number" min={slot.minSelect} max={slot.options.length} value={slot.maxSelect} onChange={e=>patchSlot(i,{maxSelect:Number(e.target.value)})}/></label><label><input type="checkbox" checked={slot.isRequired} onChange={e=>patchSlot(i,{isRequired:e.target.checked})}/>{t('restaurant.required')}</label><button disabled={i===0} onClick={()=>moveSlot(i,-1)}>{t('restaurant.moveUp')}</button><button disabled={i===slots.length-1} onClick={()=>moveSlot(i,1)}>{t('restaurant.moveDown')}</button><button onClick={()=>setSlots(x=>x.filter((_,n)=>n!==i).map((value,sortOrder)=>({...value,sortOrder})))}>{t('restaurant.removeSlot')}</button></div><DataTable rows={singles} pageSize={0} queryPrefix={`combo-${i}`} getRowKey={item=>item.id} getSearchText={item=>`${item.nameAr} ${item.nameEn}`} columns={[
        {id:'option',header:t('restaurant.option'),cell:item=>{const option=slot.options.find(o=>o.menuItemId===item.id);return <label><input type="checkbox" checked={!!option} onChange={()=>toggleOption(i,item.id)}/>{item.nameAr} / {item.nameEn}</label>},sortValue:item=>item.nameAr},
        {id:'delta',header:t('restaurant.priceDelta'),cell:item=>{const option=slot.options.find(o=>o.menuItemId===item.id);return option&&<input type="number" step="0.001" value={option.priceDelta} onChange={e=>patchSlot(i,{options:slot.options.map(o=>o.menuItemId===item.id?{...o,priceDelta:Number(e.target.value)}:o)})}/>}},
        {id:'default',header:t('restaurant.default'),cell:item=>{const option=slot.options.find(o=>o.menuItemId===item.id);return option&&<input type="radio" name={`default-${i}`} checked={option.isDefault} onChange={()=>patchSlot(i,{options:slot.options.map(o=>({...o,isDefault:o.menuItemId===item.id}))})}/>}},
      ]}/></div>)}</div><div className="modal-actions"><button onClick={addSlot}>{t('restaurant.addSlot')}</button><button onClick={saveCombo}>{t('restaurant.saveCombo')}</button></div></>}</>}
    {tab==='branches'&&<><h2>{t('restaurant.tables')}</h2><div className="settings-form-grid"><label>{t('restaurant.tableLabel')}<input value={tableForm.label} onChange={e=>setTableForm(x=>({...x,label:e.target.value}))}/></label><label>{t('restaurant.capacity')}<input type="number" min="1" value={tableForm.capacity} onChange={e=>setTableForm(x=>({...x,capacity:e.target.value}))}/></label><button onClick={saveTable}>{t('restaurant.addTable')}</button></div><DataTable rows={tables} queryPrefix="tables" getRowKey={x=>x.id} getSearchText={x=>x.label} columns={[
      {id:'label',header:t('restaurant.tableLabel'),cell:x=>x.label,sortValue:x=>x.label},
      {id:'capacity',header:t('restaurant.capacity'),cell:x=>x.capacity??'—',sortValue:x=>x.capacity},
      {id:'active',header:t('restaurant.active'),cell:x=><input type="checkbox" checked={x.isActive} onChange={()=>toggleTableActive(x)}/>,sortValue:x=>x.isActive},
    ]}/><h2>{t('restaurant.features')}</h2>{flags.map(x=><label className="checkbox-row" key={x.id}><input type="checkbox" checked={x.isEnabled} onChange={e=>setFlag(x.featureKey,e.target.checked)}/>{x.featureKey}</label>)}<div className="table-toolbar"><input value={featureKey} onChange={e=>setFeatureKey(e.target.value.toUpperCase())}/><button onClick={()=>setFlag(featureKey,true)}>{t('restaurant.addFeature')}</button></div></>}
  </section>
}
