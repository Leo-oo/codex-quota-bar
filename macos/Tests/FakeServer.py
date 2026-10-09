#!/usr/bin/python3
# Local protocol fixture. No network, Codex installation or credential access.
import json,sys,os,time
mode=os.getenv('QUOTA_TEST_SCENARIO','ok')
for line in sys.stdin:
 q=json.loads(line);m=q.get('method');i=q.get('id')
 if i is None:continue
 if m=='initialize':r={}
 elif m=='account/read':
  if mode=='logout':r={'account':None}
  else:r={'account':{'type':'chatgpt','id':'fixture-B' if mode=='switch' else 'fixture-A'}}
 elif m=='account/rateLimits/read':
  if mode=='timeout':time.sleep(20);continue
  if mode=='auth':print(json.dumps({'id':i,'error':{'message':'401 auth fixture'}}),flush=True);continue
  if mode=='empty':r={'rateLimits':{'limitId':'codex','primary':None}}
  else:r={'rateLimitsByLimitId':{'codex':{'primary':{'usedPercent':77,'windowDurationMins':10080}}}}
 else:raise RuntimeError('unexpected RPC')
 print(json.dumps({'id':i,'result':r}),flush=True)
