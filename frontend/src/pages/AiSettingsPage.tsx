import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'

interface AiSettings { provider:string; model:string; baseUrl:string|null; apiKeyLast4:string|null; isActive:boolean }
const modelSuggestions:Record<string,string[]> = {
  OpenAI:['gpt-4.1-mini','gpt-4.1','gpt-4o-mini','gpt-4o'],
  Anthropic:['claude-3-5-haiku-latest','claude-3-7-sonnet-latest','claude-sonnet-4-20250514'],
  Custom:[],
}

export default function AiSettingsPage(){
  const {t}=useTranslation()
  const [form,setForm]=useState({provider:'OpenAI',model:'gpt-4.1-mini',baseUrl:'',apiKey:'',isActive:true})
  const [last4,setLast4]=useState<string|null>(null)
  const [busy,setBusy]=useState(false)
  const [message,setMessage]=useState<string|null>(null)
  const [error,setError]=useState<string|null>(null)
  useEffect(()=>{api.get<AiSettings>('/api/ai/settings').then(x=>{setForm(f=>({...f,provider:x.provider,model:x.model,baseUrl:x.baseUrl??'',isActive:x.isActive}));setLast4(x.apiKeyLast4)}).catch(err=>setError(err instanceof ApiError?err.message:t('ai.loadError')))},[t])
  const submit=async(e:FormEvent)=>{e.preventDefault();setBusy(true);setError(null);setMessage(null);try{const x=await api.put<AiSettings>('/api/ai/settings',{...form,baseUrl:form.provider==='Custom'?form.baseUrl:null});setLast4(x.apiKeyLast4);setForm(f=>({...f,apiKey:''}));setMessage(t('ai.saved'))}catch(err){setError(err instanceof ApiError?err.message:t('ai.saveError'))}finally{setBusy(false)}}
  const changeProvider=(provider:string)=>{setForm({...form,provider,model:modelSuggestions[provider][0]??''})}
  return <section><h1>{t('ai.title')}</h1><p>{t('ai.description')}</p>{message&&<p className="text-primary">{message}</p>}{error&&<p className="error-text" role="alert">{error}</p>}<form className="ui-card ui-stack max-w-xl" onSubmit={submit}>
    <label className="checkbox-field"><input type="checkbox" checked={form.isActive} onChange={e=>setForm({...form,isActive:e.target.checked})}/><span>{t('ai.active')}</span></label>
    <label>{t('ai.provider')}<select value={form.provider} onChange={e=>changeProvider(e.target.value)}><option value="OpenAI">OpenAI</option><option value="Anthropic">Anthropic</option><option value="Custom">{t('ai.providerCustom')}</option></select></label>
    {form.provider==='Custom'&&<label>{t('ai.baseUrl')}<input type="url" required placeholder="https://api.example.com/v1/chat/completions" value={form.baseUrl} onChange={e=>setForm({...form,baseUrl:e.target.value})}/><small>{t('ai.baseUrlHint')}</small></label>}
    <label>{t('ai.model')}<input required list="ai-model-options" value={form.model} onChange={e=>setForm({...form,model:e.target.value})}/><datalist id="ai-model-options">{modelSuggestions[form.provider].map(model=><option key={model} value={model}/>)}</datalist><small>{t('ai.modelHint')}</small></label>
    <label>{t('ai.apiKey')}<input type="password" required={!last4} value={form.apiKey} placeholder={last4?`••••${last4}`:''} onChange={e=>setForm({...form,apiKey:e.target.value})}/><small>{t('ai.keyHint')}</small></label>
    <button disabled={busy}>{busy?t('common.loading'):t('common.save')}</button>
  </form></section>
}
