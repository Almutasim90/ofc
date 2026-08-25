import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, StockStatusDto } from '../api/types'
import { EditIcon, IconAction, SearchBox } from '../components/TableTools'

export default function InventoryPage() {
  const { t, i18n } = useTranslation()
  const { user } = useAuth()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [stock, setStock] = useState<StockStatusDto[]>([])
  const [loading, setLoading] = useState(true)
  const [adjusting, setAdjusting] = useState<StockStatusDto | null>(null)
  const [search, setSearch] = useState('')
  const [receiving, setReceiving] = useState(false)
  const [creating, setCreating] = useState(false)

  useEffect(() => {
    const init = async () => {
      const branchesData = await api.get<BranchDto[]>('/api/branches')
      setBranches(branchesData)
      setBranchId(user?.branchId ?? branchesData[0]?.id ?? '')
    }
    init()
  }, [user])

  const load = async () => {
    if (!branchId) return
    setLoading(true)
    setStock(await api.get<StockStatusDto[]>(`/api/inventory/stock?branchId=${branchId}`))
    setLoading(false)
  }

  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId])

  return (
    <section>
      <h1>{t('inventory.title')}</h1>
      <label>
        {t('inventory.branch')}
        <select value={branchId} onChange={(e) => setBranchId(e.target.value)} disabled={!!user?.branchId}>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.nameEn}
            </option>
          ))}
        </select>
      </label>

      {loading ? (
        <p>{t('common.loading')}</p>
      ) : (
        <><div className="table-toolbar"><SearchBox value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('common.search')} /><div className="row-actions"><button type="button" onClick={()=>setCreating(true)}>{t('inventory.addInventoryItem')}</button><button className="button-secondary" type="button" onClick={()=>setReceiving(true)} disabled={!stock.length}>{t('inventory.receiveGoods')}</button></div></div><div className="table-shell"><table>
          <thead>
            <tr>
              <th>{t('inventory.rawMaterial')}</th>
              <th>{t('inventory.currentQuantity')}</th>
              <th>{t('inventory.consumptionUnit')}</th>
              <th>{t('inventory.purchasePackage')}</th>
              <th>{t('inventory.conversionFactor')}</th>
              <th>{t('inventory.lowStockThreshold')}</th>
              <th></th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {stock.filter((item) => `${item.nameAr ?? ''} ${item.nameEn} ${item.unit}`.toLowerCase().includes(search.trim().toLowerCase())).map((item) => (
              <tr key={item.rawMaterialId}>
                <td>
                  {i18n.language === 'ar' ? item.nameAr : item.nameEn} ({item.unit})
                </td>
                <td>{item.currentQuantity}</td>
                <td>{item.unit}</td>
                <td>{i18n.language === 'ar' ? item.packageNameAr : item.packageNameEn}</td>
                <td>{item.baseQuantityPerPackage == null ? '—' : `1 × ${item.baseQuantityPerPackage} ${item.unit}`}</td>
                <td>{item.lowStockThreshold}</td>
                <td>{item.isLowStock && <span className="error-text">{t('inventory.lowStock')}</span>}</td>
                <td>
                  <IconAction label={t('inventory.adjust')} onClick={() => setAdjusting(item)}><EditIcon /></IconAction>
                </td>
              </tr>
            ))}
          </tbody>
        </table></div></>
      )}

      {adjusting && (
        <AdjustModal
          item={adjusting}
          branchId={branchId}
          onClose={() => setAdjusting(null)}
          onSaved={async () => {
            setAdjusting(null)
            await load()
          }}
        />
      )}
      {receiving && <ReceiveModal stock={stock} branchId={branchId} onClose={()=>setReceiving(false)} onSaved={async()=>{setReceiving(false);await load()}} />}
      {creating && <CreateInventoryItemWizard branchId={branchId} onClose={()=>setCreating(false)} onSaved={async()=>{setCreating(false);await load()}} />}
    </section>
  )
}

function CreateInventoryItemWizard({branchId,onClose,onSaved}:{branchId:string;onClose:()=>void;onSaved:()=>void}) {
  const {t}=useTranslation(); const [step,setStep]=useState(1); const [busy,setBusy]=useState(false)
  const [form,setForm]=useState({nameAr:'',nameEn:'',measurementType:'Count',packageNameAr:'',packageNameEn:'',baseQuantityPerPackage:'1',initialPackageCount:'0',lowStockThreshold:'0',note:''})
  const unit=form.measurementType==='Weight'?'kg':form.measurementType==='Volume'?'ml':'piece'; const total=Number(form.baseQuantityPerPackage||0)*Number(form.initialPackageCount||0)
  const submit=async()=>{setBusy(true);try{await api.post('/api/inventory/items',{branchId,...form,baseQuantityPerPackage:Number(form.baseQuantityPerPackage),initialPackageCount:Number(form.initialPackageCount),lowStockThreshold:Number(form.lowStockThreshold),note:form.note||null});onSaved()}finally{setBusy(false)}}
  return <div className="modal-backdrop"><div className="modal product-modal"><div className="product-modal-header"><div><span className="section-kicker">{t('inventory.stepOf',{step,total:3})}</span><h2>{t(`inventory.createStep${step}`)}</h2></div><button className="modal-close" onClick={onClose}>×</button></div>
    <div className="pagination-controls"><span className={step>=1?'text-primary':''}>1</span><span>—</span><span className={step>=2?'text-primary':''}>2</span><span>—</span><span className={step>=3?'text-primary':''}>3</span></div>
    {step===1&&<div className="product-form-grid"><label>{t('rawMaterials.nameAr')}<input autoFocus value={form.nameAr} onChange={e=>setForm({...form,nameAr:e.target.value})}/></label><label>{t('rawMaterials.nameEn')}<input value={form.nameEn} onChange={e=>setForm({...form,nameEn:e.target.value})}/></label><label>{t('inventory.measurementType')}<select value={form.measurementType} onChange={e=>setForm({...form,measurementType:e.target.value})}><option value="Count">{t('inventory.measureCount')}</option><option value="Weight">{t('inventory.measureWeight')}</option><option value="Volume">{t('inventory.measureVolume')}</option></select><small>{t('inventory.baseUnit')}: {unit}</small></label></div>}
    {step===2&&<div className="product-form-grid"><label>{t('inventory.packageNameAr')}<input autoFocus value={form.packageNameAr} onChange={e=>setForm({...form,packageNameAr:e.target.value})} placeholder={t('inventory.packageExampleAr')}/></label><label>{t('inventory.packageNameEn')}<input value={form.packageNameEn} onChange={e=>setForm({...form,packageNameEn:e.target.value})} placeholder={t('inventory.packageExampleEn')}/></label><label>{t('inventory.packageConversion')} ({unit})<input type="number" min="0.001" step="0.001" value={form.baseQuantityPerPackage} onChange={e=>setForm({...form,baseQuantityPerPackage:e.target.value})}/></label></div>}
    {step===3&&<div className="product-form-grid"><label>{t('inventory.initialPackageCount')}<input autoFocus type="number" min="0" step="0.001" value={form.initialPackageCount} onChange={e=>setForm({...form,initialPackageCount:e.target.value})}/></label><label>{t('inventory.lowStockThreshold')} ({unit})<input type="number" min="0" step="0.001" value={form.lowStockThreshold} onChange={e=>setForm({...form,lowStockThreshold:e.target.value})}/></label><label>{t('inventory.note')}<input value={form.note} onChange={e=>setForm({...form,note:e.target.value})}/></label><div className="ui-card"><strong>{t('inventory.quantityToAdd')}: {total.toFixed(3)} {unit}</strong><p>{form.initialPackageCount} × {form.packageNameAr||t('inventory.packageType')}</p></div></div>}
    <div className="modal-actions">{step>1&&<button className="button-secondary" type="button" onClick={()=>setStep(step-1)}>{t('common.previous')}</button>}{step<3?<button type="button" disabled={(step===1&&(!form.nameAr||!form.nameEn))||(step===2&&(!form.packageNameAr||!form.packageNameEn||Number(form.baseQuantityPerPackage)<=0))} onClick={()=>setStep(step+1)}>{t('common.next')}</button>:<button type="button" disabled={busy} onClick={submit}>{t('inventory.createAndAdd')}</button>}<button className="button-secondary" type="button" onClick={onClose}>{t('common.cancel')}</button></div>
  </div></div>
}

function ReceiveModal({stock,branchId,onClose,onSaved}:{stock:StockStatusDto[];branchId:string;onClose:()=>void;onSaved:()=>void}) {
  const {t,i18n}=useTranslation(); const configuredStock=stock.filter(x=>x.supplyPackageId); const [materialId,setMaterialId]=useState(configuredStock[0]?.rawMaterialId??'')
  const now=new Date(); const today=`${now.getFullYear()}-${String(now.getMonth()+1).padStart(2,'0')}-${String(now.getDate()).padStart(2,'0')}`
  const [count,setCount]=useState('1'); const [receivedDate,setReceivedDate]=useState(today); const [note,setNote]=useState(''); const [submitting,setSubmitting]=useState(false)
  const material=stock.find(x=>x.rawMaterialId===materialId)
  const receive=async()=>{if(!material?.supplyPackageId)return;setSubmitting(true);try{await api.post('/api/inventory/receipts',{branchId,supplyPackageId:material.supplyPackageId,packageCount:Number(count),receivedDate,note:note||null});onSaved()}finally{setSubmitting(false)}}
  const total=(material?.baseQuantityPerPackage??0)*Number(count||0)
  return <div className="modal-backdrop"><div className="modal product-modal"><div className="product-modal-header"><h2>{t('inventory.receiveGoods')}</h2><button className="modal-close" onClick={onClose}>×</button></div><div className="product-form-grid">
    <label>{t('inventory.rawMaterial')}<select value={materialId} onChange={e=>setMaterialId(e.target.value)}>{configuredStock.map(x=><option key={x.rawMaterialId} value={x.rawMaterialId}>{i18n.language==='ar'?x.nameAr:x.nameEn}</option>)}</select></label>
    <label>{t('inventory.packageType')}<input readOnly value={`${i18n.language==='ar'?material?.packageNameAr??'':material?.packageNameEn??''} — ${material?.baseQuantityPerPackage??0} ${material?.unit??''}`} /></label>
    <label>{t('inventory.packageCount')}<input type="number" min="0.001" step="0.001" value={count} onChange={e=>setCount(e.target.value)}/></label>
    <label>{t('inventory.receivedDate')}<input type="date" value={receivedDate} onChange={e=>setReceivedDate(e.target.value)} /></label>
    <label>{t('inventory.note')}<input value={note} onChange={e=>setNote(e.target.value)}/></label>
  </div>{material&&<div className="ui-card"><strong>{t('inventory.quantityToAdd')}: {total.toFixed(3)} {material.unit}</strong><p>{count} × {i18n.language==='ar'?material.packageNameAr:material.packageNameEn}</p></div>}
  <div className="modal-actions"><button type="button" disabled={submitting||!material?.supplyPackageId||Number(count)<=0} onClick={receive}>{t('inventory.confirmReceipt')}</button><button type="button" className="button-secondary" onClick={onClose}>{t('common.cancel')}</button></div></div></div>
}

function AdjustModal({
  item,
  branchId,
  onClose,
  onSaved,
}: {
  item: StockStatusDto
  branchId: string
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const [quantityChange, setQuantityChange] = useState('0')
  const [reason, setReason] = useState('')
  const [threshold, setThreshold] = useState(item.lowStockThreshold.toString())
  const [submitting, setSubmitting] = useState(false)

  const submitAdjustment = async () => {
    setSubmitting(true)
    try {
      await api.post('/api/inventory/stock-adjustments', {
        branchId,
        rawMaterialId: item.rawMaterialId,
        quantityChange: Number(quantityChange),
        reason,
      })
      onSaved()
    } finally {
      setSubmitting(false)
    }
  }

  const submitThreshold = async () => {
    setSubmitting(true)
    try {
      await api.put('/api/inventory/stock/low-stock-threshold', {
        branchId,
        rawMaterialId: item.rawMaterialId,
        threshold: Number(threshold),
      })
      onSaved()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <h2>{t('inventory.adjustTitle')}</h2>
        <p>
          {item.nameEn} ({item.unit})
        </p>
        <label>
          {t('inventory.quantityChange')}
          <input type="number" step="0.001" value={quantityChange} onChange={(e) => setQuantityChange(e.target.value)} />
          <small>{t('inventory.quantityChangeHint')}</small>
        </label>
        <label>
          {t('inventory.reason')}
          <input value={reason} onChange={(e) => setReason(e.target.value)} required />
        </label>
        <div className="modal-actions">
          <button type="button" onClick={submitAdjustment} disabled={submitting || !reason}>
            {t('inventory.save')}
          </button>
        </div>

        <hr />

        <label>
          {t('inventory.lowStockThreshold')}
          <input type="number" step="0.001" min="0" value={threshold} onChange={(e) => setThreshold(e.target.value)} />
        </label>
        <div className="modal-actions">
          <button type="button" onClick={submitThreshold} disabled={submitting}>
            {t('inventory.setThreshold')}
          </button>
          <button type="button" onClick={onClose}>
            {t('inventory.cancel')}
          </button>
        </div>
      </div>
    </div>
  )
}
