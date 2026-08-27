exec(open('.tmp/check-release-status.py',encoding='utf-8').read().split('for path in')[0])
base='https://api.github.com/repos/Almutasim90/lolat-suwaiq'
def get(path):
 req=urllib.request.Request(base+path,headers={'Authorization':'Bearer '+cred.get('password',''),'User-Agent':'release-verification','Accept':'application/vnd.github+json'})
 with urllib.request.urlopen(req,timeout=20) as r:return json.load(r)
try:
 hooks=get('/hooks')
 for hook in hooks:
  print(json.dumps({'id':hook['id'],'active':hook['active'],'events':hook['events'],'target_host':urllib.parse.urlparse(hook.get('config',{}).get('url','')).hostname,'last_response':hook.get('last_response')}))
  deliveries=get('/hooks/'+str(hook['id'])+'/deliveries?per_page=3')
  print(json.dumps([{'id':d['id'],'delivered_at':d['delivered_at'],'event':d['event'],'status':d['status'],'status_code':d['status_code']} for d in deliveries]))
except urllib.error.HTTPError as e:print('Webhook inspection HTTP',e.code)
