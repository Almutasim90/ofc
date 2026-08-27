import subprocess,urllib.request,json,os
sha=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
env=dict(os.environ,GIT_TERMINAL_PROMPT='0')
p=subprocess.run(['git','credential','fill'],input='protocol=https\nhost=github.com\n\n',text=True,capture_output=True,env=env)
if p.returncode: print('No configured GitHub API credential');raise SystemExit()
cred=dict(l.split('=',1) for l in p.stdout.splitlines() if '=' in l)
for path in [f'commits/{sha}/status',f'actions/runs?head_sha={sha}',f'deployments?sha={sha}']:
 request=urllib.request.Request('https://api.github.com/repos/Almutasim90/lolat-suwaiq/'+path,headers={'Authorization':'Bearer '+cred.get('password',''),'User-Agent':'release-verification','Accept':'application/vnd.github+json'})
 try:
  with urllib.request.urlopen(request,timeout=20) as r: data=json.load(r)
  if '/status' in path: print(json.dumps({'state':data.get('state'),'statuses':[{k:s.get(k) for k in ['context','state','description','target_url']} for s in data.get('statuses',[])]}))
  elif path.startswith('actions'): print(json.dumps({'runs':[{k:s.get(k) for k in ['name','status','conclusion','html_url']} for s in data.get('workflow_runs',[])]}))
  else: print(json.dumps({'deployments':[{k:s.get(k) for k in ['id','environment','statuses_url']} for s in data]}))
 except urllib.error.HTTPError as e: print('GitHub API HTTP',e.code)
