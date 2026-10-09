'use strict';
// Compile/run only the explicit offline entry point after the parent's final
// published source baseline is frozen. Does not run Program.Main, mutate production, use the
// network or read account/runtime cache files.
const fs=require('fs'), path=require('path'), crypto=require('crypto'), cp=require('child_process');
const repository=path.resolve(__dirname,'../..'), source=path.resolve(__dirname,'../src');
const privateTests=path.resolve(repository,'../private-validation/tests');
const buildMap=path.resolve(process.argv[2]||path.join(repository,'audit','baseline.json'));
const evidencePath=path.resolve(process.argv[3]||path.join(privateTests,'news-unread'));
function within(parent,child){const relative=path.relative(parent,child);return relative==='' || (!relative.startsWith('..'+path.sep) && relative!=='..' && !path.isAbsolute(relative));}
function realFuture(target){
  let current=target;const suffix=[];
  while(!fs.existsSync(current)){const parent=path.dirname(current);if(parent===current)throw new Error('No existing artifact ancestor.');suffix.unshift(path.basename(current));current=parent;}
  return path.join(fs.realpathSync(current),...suffix);
}
const realRepo=fs.realpathSync(repository),realPrivate=realFuture(privateTests),realEvidence=realFuture(evidencePath);
if(realEvidence===realPrivate || !within(realPrivate,realEvidence) || within(realRepo,realEvidence) || within(realRepo,realPrivate))
  throw new Error('Artifacts must use a fresh suite folder under sibling private-validation/tests, outside the repository.');
if(fs.existsSync(evidencePath) && !fs.existsSync(path.join(evidencePath,'process.json')))
  throw new Error('Fresh artifact folder required; unknown existing evidence is never reused.');
if(fs.existsSync(path.join(evidencePath,'own-fixtures')))
  throw new Error('Existing synthetic runtime is preserved; execution stopped.');
if(fs.existsSync(path.join(evidencePath,'news-unread-regression-report.json'))) throw new Error('Existing execution evidence is preserved; execution stopped.');
if(fs.existsSync(path.join(evidencePath,'process.json'))){
  const previous=JSON.parse(fs.readFileSync(path.join(evidencePath,'process.json'),'utf8'));
  if(!Number.isInteger(previous.compileExit) || previous.compileExit===0 || previous.exit!==undefined)
    throw new Error('Only an explicit nonzero compile failure without any execution may be retried.');
  const backup=path.join(evidencePath,'compile-default-denied');fs.mkdirSync(backup,{recursive:true});
  for(const name of ['process.json','source-verification.json','compile-output.txt']) if(fs.existsSync(path.join(evidencePath,name))){
    if(fs.existsSync(path.join(backup,name))) throw new Error('Original compile-failure evidence already preserved; execution stopped.');
    fs.copyFileSync(path.join(evidencePath,name),path.join(backup,name));
  }
}

fs.mkdirSync(evidencePath,{recursive:true});
const digest=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const files=fs.readdirSync(source).filter(x=>x.toLowerCase().endsWith('.cs')).sort();
if(files.length!==34) throw new Error('Expected the frozen 34-source baseline, including NewsDiagnostic.');
const getMap=()=>Object.fromEntries(files.map(name=>[name,digest(path.join(source,name))]));
const hashes=getMap();
const mapDocument=JSON.parse(fs.readFileSync(path.resolve(buildMap),'utf8').replace(/^\uFEFF/,''));
function findSourceMap(value){
  if(!value || typeof value!=='object') return null;
  if(!Array.isArray(value)){
    const entries=Object.entries(value);
    if(entries.filter(([key,v])=>key.toLowerCase().endsWith('.cs') && typeof v==='string' && /^[0-9a-f]{64}$/i.test(v)).length===files.length)
      return Object.fromEntries(entries.filter(([key,v])=>key.toLowerCase().endsWith('.cs') && typeof v==='string').map(([key,v])=>[path.basename(key.replace(/\\/g,'/')),v.toLowerCase()]));
    for(const child of Object.values(value)){const result=findSourceMap(child);if(result)return result;}
  } else {
    const possible=value.map(x=>x && typeof x==='object'?{name:path.basename(String(x.file||x.name||x.path||'').replace(/\\/g,'/')),hash:x.sha256||x.hash||x.Hash}:null);
    if(possible.length===files.length && possible.every(x=>x && x.name.toLowerCase().endsWith('.cs') && /^[0-9a-f]{64}$/i.test(String(x.hash)))) return Object.fromEntries(possible.map(x=>[x.name,String(x.hash).toLowerCase()]));
    for(const child of value){const result=findSourceMap(child);if(result)return result;}
  }
  return null;
}
const frozen=findSourceMap(mapDocument);
if(!frozen) throw new Error('The supplied build map has no exact 34-source SHA256 map; execution stopped.');
const matches=files.every(name=>frozen[name]===hashes[name]) && Object.keys(frozen).length===files.length;
if(!matches) throw new Error('Production sources differ from the frozen build map; execution stopped.');
const usageSource=fs.readFileSync(path.join(source,'UsageMini.cs'),'utf8'),cardSource=fs.readFileSync(path.join(source,'ResetNewsCard.cs'),'utf8');
const traceStart=usageSource.indexOf('void TraceNews(string stage)'),traceEnd=usageSource.indexOf('\n    void OpenNews()',traceStart);
const cardStart=cardSource.indexOf('internal object MessageDiagnostic'),cardEnd=cardSource.indexOf('internal string OverflowDiagnostic',cardStart);
const traceBody=traceStart>=0 && traceEnd>traceStart?usageSource.slice(traceStart,traceEnd):'';
const cardBody=cardStart>=0 && cardEnd>cardStart?cardSource.slice(cardStart,cardEnd):'';
const forbidden=/\b(quotaError|quotaClient|AccountKey|email|credentials|StandardOutput|StandardError|rawResponse|responseBody|Environment|Body|Title|Scope|Products)\b/;
const diagnosticPrivacyAudit={scope:'static production TraceNews and MessageDiagnostic bodies',bodiesFound:!!traceBody && !!cardBody,
  forbiddenPrivateOrRawReferencesFound:forbidden.test(traceBody)||forbidden.test(cardBody),quotaOrAccountObjectsSerialized:false};
if(!diagnosticPrivacyAudit.bodiesFound || diagnosticPrivacyAudit.forbiddenPrivateOrRawReferencesFound) throw new Error('Production diagnostic callsite privacy audit requires review before running.');
const fixture=path.join(__dirname,'NewsUnreadRegression118.cs'),exe=path.join(evidencePath,'NewsUnreadRegression118.exe');
const windowsRoot=process.env.WINDIR||process.env.SystemRoot;
if(!windowsRoot) throw new Error('Run this fixture on Windows with WINDIR or SystemRoot set.');
const csc=path.join(windowsRoot,'Microsoft.NET','Framework64','v4.0.30319','csc.exe');
const framework=path.dirname(csc), wpf=path.join(framework,'WPF');
const resources=['quota-tray-dark.ico','quota-tray-light.ico'];
const resourceHashes=Object.fromEntries(resources.map(x=>[x,digest(path.join(source,'assets',x))]));
const frozenAssets=Object.fromEntries(Object.entries(mapDocument.assetHashes||{}).map(([name,hash])=>[path.basename(name.replace(/\\/g,'/')),String(hash).toLowerCase()]));
if(Object.keys(frozenAssets).length!==2 || resources.some(name=>resourceHashes[name]!==frozenAssets[name]))
  throw new Error('Resources differ from the frozen published baseline; no fixture started.');
const fixtureBefore=digest(fixture),runnerBefore=digest(__filename);
const compileArgs=['/nologo','/codepage:65001','/optimize+','/target:winexe','/platform:x64','/main:NewsUnreadRegression118','/out:'+exe,
  '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll',
  '/reference:'+path.join(wpf,'UIAutomationClient.dll'),'/reference:'+path.join(wpf,'UIAutomationTypes.dll'),'/reference:'+path.join(wpf,'WindowsBase.dll'),
  '/resource:'+path.join(source,'assets',resources[0])+',QuotaBar.Tray.Dark.ico',
  '/resource:'+path.join(source,'assets',resources[1])+',QuotaBar.Tray.Light.ico',
  ...files.map(x=>path.join(source,x)),fixture];
const processRecord={scope:'own synthetic files and unshown bitmap/layout controls only',source_sha25634:hashes,sourceCount:files.length,sourceMatchesFinalBuildMap:matches,
  diagnosticPrivacyAudit:diagnosticPrivacyAudit,buildMapPath:path.resolve(buildMap),buildMapSha256:digest(path.resolve(buildMap)),resourceHashes:resourceHashes,
  fixtureSha256:fixtureBefore,runnerSha256:runnerBefore,normalApplicationStarted:false,networkAccessed:false,accountFilesRead:false,
  actualRuntimeCacheRead:false,userProcessTouched:false,physicalUserInputTested:false,compileArguments:compileArgs};
const compile=cp.spawnSync(csc,compileArgs,{cwd:__dirname,windowsHide:true,encoding:'utf8',timeout:30000});
processRecord.compileExit=compile.status;processRecord.compileError=compile.error?String(compile.error):null;
fs.writeFileSync(path.join(evidencePath,'compile-output.txt'),(compile.stdout||'')+(compile.stderr||''));
if(compile.status===0){
  processRecord.fixtureExeSha256=digest(exe);
  const run=cp.spawnSync(exe,[evidencePath],{cwd:__dirname,windowsHide:true,encoding:'utf8',timeout:30000});
  processRecord.exit=run.status;processRecord.runError=run.error?String(run.error):null;
  processRecord.probeExited=run.status!==null;processRecord.timedOut=!!run.error && run.error.code==='ETIMEDOUT';
  const reportFile=path.join(evidencePath,'news-unread-regression-report.json');
  if(fs.existsSync(reportFile)){const report=JSON.parse(fs.readFileSync(reportFile,'utf8'));processRecord.pass=report.passed;processRecord.fail=report.failed;processRecord.knownIssues=report.knownIssues;}
}
const after=getMap();
processRecord.sourceUnchanged=JSON.stringify(after)===JSON.stringify(hashes);
processRecord.resourcesUnchanged=resources.every(name=>digest(path.join(source,'assets',name))===resourceHashes[name]);
processRecord.fixtureUnchanged=fixtureBefore===digest(fixture);
processRecord.runnerUnchanged=runnerBefore===digest(__filename);
processRecord.accepted=processRecord.compileExit===0 && processRecord.exit===0 && processRecord.pass===79 && processRecord.fail===0 &&
  processRecord.sourceUnchanged && processRecord.resourcesUnchanged && processRecord.fixtureUnchanged && processRecord.runnerUnchanged;
fs.writeFileSync(path.join(evidencePath,'source-verification.json'),JSON.stringify(processRecord,null,2));
fs.writeFileSync(path.join(evidencePath,'process.json'),JSON.stringify(processRecord,null,2));
// Only this allowlisted, path-free summary is published. Full compiler paths,
// synthetic cache contents, bitmaps and process evidence stay outside the repo.
const published=path.join(repository,'audit','tests-validation.json');
const previousSummary=fs.existsSync(published)?JSON.parse(fs.readFileSync(published,'utf8').replace(/^\uFEFF/,'')):null;
const summary={schema:1,baselineSha256:digest(buildMap),sourceCount:files.length,sourceHashes:hashes,assetHashes:resourceHashes,suites:{}};
const numberFields=['pass','fail','compileExit','runExit'];
const booleanFields=['accepted','sourceMatchesBaseline','assetsMatchBaseline','sourceUnchanged','resourcesUnchanged','fixtureUnchanged','runnerUnchanged',
  'inputsUnchanged','probeExited','requiredCasesExactlyCompleted','normalProgramMainStarted','networkAccessed','accountFilesRead','actualRuntimeCacheRead',
  'userProcessTouched','physicalGlobalInputSent','nativeAcknowledgementTested','realVisibleNotificationTested','nativeVisiblePaintAcknowledgementRequired'];
const hashFields=['fixtureSha256','runnerSha256','fixtureExeSha256'];
for(const name of ['newsUnread','cardFreeze']){
  const old=previousSummary?.baselineSha256===summary.baselineSha256?previousSummary.suites?.[name]:null;
  if(!old || typeof old!=='object')continue;
  const clean={scope:name==='newsUnread'?'synthetic own-file and unshown bitmap/layout fixture':'non-activating own-window native paint and acknowledgement fixture',
    expectedAssertions:name==='newsUnread'?79:46};
  for(const key of numberFields)if(old[key]===null || Number.isInteger(old[key]))clean[key]=old[key];
  for(const key of booleanFields)if(typeof old[key]==='boolean')clean[key]=old[key];
  for(const key of hashFields)if(old[key]===null || (typeof old[key]==='string' && /^[0-9a-f]{64}$/i.test(old[key])))clean[key]=old[key];
  summary.suites[name]=clean;
}
summary.suites.newsUnread={
  scope:'synthetic own-file and unshown bitmap/layout fixture',
  expectedAssertions:79,pass:processRecord.pass??null,fail:processRecord.fail??null,
  compileExit:processRecord.compileExit,runExit:processRecord.exit??null,accepted:processRecord.accepted,
  sourceMatchesBaseline:matches,assetsMatchBaseline:true,sourceUnchanged:processRecord.sourceUnchanged,
  resourcesUnchanged:processRecord.resourcesUnchanged,fixtureUnchanged:processRecord.fixtureUnchanged,runnerUnchanged:processRecord.runnerUnchanged,
  fixtureSha256:fixtureBefore,runnerSha256:runnerBefore,fixtureExeSha256:processRecord.fixtureExeSha256||null,
  probeExited:processRecord.probeExited===true,normalProgramMainStarted:false,networkAccessed:false,accountFilesRead:false,
  actualRuntimeCacheRead:false,userProcessTouched:false,physicalGlobalInputSent:false,
  nativeAcknowledgementTested:false,realVisibleNotificationTested:false
};
fs.writeFileSync(published,JSON.stringify(summary,null,2)+'\n');
process.stdout.write(JSON.stringify({compileExit:processRecord.compileExit,exit:processRecord.exit,pass:processRecord.pass,fail:processRecord.fail,
  sourceMatchesBaseline:matches,sourceUnchanged:processRecord.sourceUnchanged,probeExited:processRecord.probeExited,accepted:processRecord.accepted})+'\n');
process.exitCode=processRecord.accepted?0:1;

