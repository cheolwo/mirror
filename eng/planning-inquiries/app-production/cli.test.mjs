import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import vm from 'node:vm';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { inside, renderHtml, renderMarkdown, writeOutputs, checkOutputs, exportHandoff, createReadOnlyServer, parseArgs, viewerFileRefs, handoff, intakeLines } from './cli.mjs';

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'mirror-planning-cli-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  return root;
}
function sample() {
  return { schemaVersion:'planning-app-production-index.v1', fingerprint:'abc',scope:{roots:['docs/AI/Planning'],boundary:'InventoryOnly'},
    plans:[{id:'PLAN-APP',title:'음식점 기획',category:'시스템',roles:['restaurant'],declaredState:'Draft',sourceRefs:[{path:'docs/plan.md',sha256:'123'}],canonicalRefs:[{path:'docs/plan.md',sha256:'123'}],workItemIds:['menu'],compatibilityIds:['PLAN-APP-001']}],
    documents:[{path:'docs/plan.md',sha256:'123'}],workItems:[{id:'menu',title:'메뉴 등록',roles:['restaurant'],codePresence:'Present',testCodePresence:'Present',planRefs:[{path:'docs/plan.md',revision:'r1',state:'Matched'}],codeRefs:[{path:'App/Menu.cs',state:'Present'}],testRefs:[],resultRefs:[],workOrderRefs:[],remaining:['Android 화면 미검증'],evidence:[{kind:'AutomatedTest',result:'Passed',freshness:'Unknown',summary:'과거 시험 기록',path:'docs/result.md'}]}],diagnostics:[] };
}

// Pure DOM contract fixture, not a browser/layout/screenshot or mobile-device test.
function viewerFixture(data) {
  class Element {
    constructor(tag) { this.tag=tag;this.children=[];this.textContent='';this.value='';this.dataset={};this.attributes={};this.events={}; }
    append(...values) { this.children.push(...values); }
    replaceChildren(...values) { this.children=[...values]; }
    setAttribute(key,value) { this.attributes[key]=value; }
    addEventListener(event,fn) { this.events[event]=fn; }
  }
  const ids=new Map();
  const document={createElement:tag=>new Element(tag),getElementById:id=>{if(!ids.has(id))ids.set(id,new Element('div'));return ids.get(id);}};
  document.getElementById('index-data').textContent=JSON.stringify({...data,viewerFileRefs:[...viewerFileRefs(data)]});
  const html=renderHtml(data),script=html.match(/<script>\s*([\s\S]*?)<\/script>/)[1];
  vm.runInNewContext(script,{document},{timeout:1000});
  return {get:id=>document.getElementById(id),text:element=>[element.textContent,...element.children.map(child=>[child.textContent,...child.children.map(grandchild=>grandchild.textContent)].join(' '))].join(' ')};
}

test('화면 스크립트의 검색·역할 필터와 빈 결과 상태를 DOM 계약으로 시험한다',()=>{
  const ui=viewerFixture(sample());assert.equal(ui.get('plans').children[0].dataset.id,'PLAN-APP');
  ui.get('role').value='driver';ui.get('role').events.change();assert.match(ui.text(ui.get('plans')),/검색 결과가 없습니다/);
  ui.get('role').value='restaurant';ui.get('role').events.change();assert.equal(ui.get('plans').children[0].dataset.id,'PLAN-APP');
  ui.get('search').value='메뉴 등록';ui.get('search').events.input();assert.equal(ui.get('count').textContent,'1개');
  ui.get('search').value='없는 기획';ui.get('search').events.input();assert.equal(ui.get('count').textContent,'0개');
});

test('업무 미연결 필터와 선택 상세가 미구현이나 승인으로 오인되지 않는다',()=>{
  const data=sample();data.plans.push({...data.plans[0],id:'PLAN-OTHER',title:'후속 기획',workItemIds:[],roles:['admin']});
  const ui=viewerFixture(data);ui.get('status').value='unreviewed';ui.get('status').events.change();
  assert.equal(ui.get('count').textContent,'1개');assert.equal(ui.get('plans').children[0].dataset.id,'PLAN-OTHER');
  assert.match(ui.text(ui.get('detail')),/상세 연결 미검토/);assert.match(ui.text(ui.get('detail')),/코드가 없다는 뜻이 아닙니다/);
  ui.get('status').value='';ui.get('status').events.change();ui.get('plans').children[0].events.click();
  assert.equal(ui.get('plans').children[0].attributes['aria-pressed'],'true');assert.match(ui.text(ui.get('detail')),/메뉴 등록/);
});

test('같은 입력으로 JSON/Markdown/HTML이 동일하며 변조를 확인한다', t => {
  const root=fixture(t), data=sample();writeOutputs(root,data);assert.deepEqual(checkOutputs(root,data),[]);
  fs.appendFileSync(path.join(root,'docs/AI/generated/planning-app-production.md'),'changed');
  assert.deepEqual(checkOutputs(root,data),['StaleOrModifiedOutput:.md']);writeOutputs(root,data);assert.deepEqual(checkOutputs(root,data),[]);
});
test('HTML 데이터의 스크립트 종료와 태그 삽입을 escape한다',()=>{
  const data=sample();data.plans[0].title='</script><script>alert(1)</script>';
  const html=renderHtml(data);assert.ok(!html.includes(data.plans[0].title));assert.ok(html.includes('\\u003c/script\\u003e'));
  assert.ok(html.includes("connect-src 'none'"));assert.ok(!html.includes('innerHTML'));
});
test('문서 출력은 코드 존재와 과거 결과·현재성을 분리한다',()=>{
  const text=renderMarkdown(sample());assert.match(text,/코드: Present/);assert.match(text,/결과 Passed \/ 현재성 Unknown/);assert.match(text,/Android 화면 미검증/);
});
test('상태판은 입력된 업무·역할·근거 필터와 안전한 링크를 가진다',()=>{
  const html=renderHtml(sample());for(const id of ['search','role','category','status','detail'])assert.ok(html.includes(`id="${id}"`));assert.ok(html.includes("a.rel='noopener'"));
});
test('저장소 밖·드라이브·상위 폴더·역슬래시를 거부한다',t=>{
  const root=fixture(t);for(const ref of ['../secret','C:/secret','/secret','dir\\secret','docs/../../secret'])assert.throws(()=>inside(root,ref));
});

test('끊어진 출력 심볼릭 링크는 파일 생성 전에 거부한다',t=>{
  const root=fixture(t),target=path.join(root,'dangling.json'),original=fs.lstatSync.bind(fs);
  t.mock.method(fs,'lstatSync',(value,...args)=>value===target?{isSymbolicLink:()=>true}:original(value,...args));
  assert.throws(()=>inside(root,'dangling.json'),/DanglingSymlink/);
  assert.equal(fs.existsSync(target),false);
});
test('export는 선택된 기획 참조와 검토 요약만 내보내고 원시 파일을 복사하지 않는다',t=>{
  const root=fixture(t);fs.mkdirSync(path.join(root,'docs'));fs.writeFileSync(path.join(root,'docs/plan.md'),'password=private-value');
  const dest=exportHandoff(root,sample(),'PLAN-APP-001','artifacts/local/exports/sample');
  assert.deepEqual(fs.readdirSync(dest).sort(),['handoff.json','handoff.md']);assert.ok(!fs.readFileSync(path.join(dest,'handoff.json'),'utf8').includes('private-value'));
  assert.match(fs.readFileSync(path.join(dest,'handoff.md'),'utf8'),/미첨부 원문/);assert.throws(()=>exportHandoff(root,sample(),'PLAN-APP','artifacts/local/exports/sample'),/AlreadyExists/);
  assert.throws(()=>exportHandoff(root,sample(),'PLAN-APP','docs/export'),/ExportMustBeLocalArtifact/);
});
test('없는 기획의 export는 파일을 만들지 않는다',t=>{
  const root=fixture(t);assert.throws(()=>exportHandoff(root,sample(),'UNKNOWN','artifacts/local/nope'),/PlanNotFound/);assert.equal(fs.existsSync(path.join(root,'artifacts/local/nope')),false);
});
test('로컬 서버는 참조 allowlist만 읽고 쓰기·비밀 파일 조회를 거부한다',async t=>{
  const root=fixture(t),data=sample();data.workItems[0].workOrderRefs=[{path:'docs/app.work-order.json'}];data.workItems[0].testRefs=[{path:'eng/test.mjs'}];
  data.workItems[0].evidence.push({path:'artifacts/raw.json',kind:'Validation',result:'Unknown'});
  writeOutputs(root,data);fs.mkdirSync(path.join(root,'docs'),{recursive:true});fs.writeFileSync(path.join(root,'docs/plan.md'),'문서');fs.writeFileSync(path.join(root,'.env'),'SECRET');
  fs.writeFileSync(path.join(root,'docs/app.work-order.json'),'{}');fs.mkdirSync(path.join(root,'eng'));fs.writeFileSync(path.join(root,'eng/test.mjs'),'// test');
  const server=createReadOnlyServer(root,data);await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));t.after(()=>new Promise(resolve=>server.close(resolve)));
  const base='http://127.0.0.1:'+server.address().port;
  assert.equal((await fetch(base)).status,200);assert.equal((await fetch(base+'/docs/plan.md')).status,200);assert.equal((await fetch(base+'/.env')).status,404);
  assert.equal((await fetch(base,{method:'POST'})).status,405);assert.equal((await fetch(base+'/%2e%2e%2fsecret')).status,404);
  assert.equal((await fetch(base+'/docs/plan.md')).headers.get('content-type'),'text/plain; charset=utf-8');
  assert.equal((await fetch(base+'/docs/app.work-order.json')).status,200);assert.equal((await fetch(base+'/eng/test.mjs')).status,200);
  assert.equal((await fetch(base+'/artifacts/raw.json')).status,404);
  const linked=new Set(JSON.parse(renderHtml(data).match(/type="application\/json">([\s\S]*?)<\/script>/)[1]).viewerFileRefs);
  assert.ok(linked.has('docs/app.work-order.json'));assert.ok(linked.has('eng/test.mjs'));assert.ok(!linked.has('artifacts/raw.json'));
  const hostileStatus=await new Promise(resolve=>http.get(base,{headers:{host:'untrusted.example'}},r=>{r.resume();resolve(r.statusCode);}));assert.equal(hostileStatus,403);
});
test('임의 command나 잘못된 CLI 모드는 거부한다',()=>{
  assert.throws(()=>parseArgs(['run']),/UnknownMode/);assert.throws(()=>parseArgs(['write','--command','anything']),/InvalidArguments/);assert.equal(parseArgs(['check']).mode,'check');
});

function intakeSample() {
  return { state:'NeedsInformation',inputState:'Incomplete',reviewState:'Pending',targetAppIds:['restaurant'],impactedRoles:['restaurant','orderer'],
    approval:{state:'NotGranted',grantsExecution:false},environment:{state:'Unknown',checks:[{id:'signing',state:'Unknown',reason:'외부 서명 환경 미확인',deliverableIds:['apk']}],blockedDeliverableIds:['apk']},
    sections:{purpose:{status:'ExistingConfirmed',value:'메뉴 기존 코드 재사용',sources:[{path:'docs/plan.md',sha256:'123'}]}},
    questions:[{id:'selection',text:'미정 선택',critical:true}],conflicts:[{id:'limits',description:'길이 제한 대조 필요',blocking:true}],
    blockers:['사람 검토 대기'],deliverables:[{id:'apk',kind:'AndroidPackage',required:true}] };
}

test('제작 입력 필터는 코드 존재·실행 증거 필터와 독립이다',()=>{
  const data=sample();data.workItems[0].intake=intakeSample();const ui=viewerFixture(data);
  ui.get('intake-status').value='Confirmed';ui.get('intake-status').events.change();assert.equal(ui.get('count').textContent,'0개');
  ui.get('intake-status').value='NeedsInformation';ui.get('intake-status').events.change();assert.equal(ui.get('count').textContent,'1개');
  const allText=e=>[e.textContent,...e.children.map(allText)].join(' ');
  const detail=allText(ui.get('detail'));for(const expected of ['입력·검토 보완','미승인','외부 서명 환경 미확인','실행 결과 아님'])assert.ok(detail.includes(expected),expected);
});

test('인계와 현황표에 대상·미정·차단 결과물을 포함하지만 실행 승인은 생성하지 않는다',()=>{
  const data=sample();data.workItems[0].intake=intakeSample();const output=handoff(data,'PLAN-APP');
  assert.equal(output.packet.authority,'ReferenceOnly_NotDevelopmentApproval');assert.equal(output.packet.workItems[0].intake.approval.state,'NotGranted');
  for(const text of [renderMarkdown(data),output.markdown])for(const expected of ['NeedsInformation','대상 앱: restaurant','길이 제한 대조 필요','환경 차단 결과물: apk','미정 선택'])assert.ok(text.includes(expected),expected);
  assert.match(intakeLines().join(' '),/NotReviewed/);
  assert.equal(parseArgs(['intake-check','--work-item','menu'])['work-item'],'menu');
});

test('변경된 입력의 충돌은 화면·문서·인계에서 과거 입력 기록으로 표시한다',t=>{
  const root=fixture(t),data=sample();data.workItems[0].intake={...intakeSample(),state:'Changed',inputState:'Changed'};
  const allText=e=>[e.textContent,...e.children.map(allText)].join(' ');
  const ui=viewerFixture(data),output=handoff(data,'PLAN-APP');
  for(const text of [allText(ui.get('detail')),renderMarkdown(data),output.markdown]){
    assert.match(text,/이전 입력에 기록된 충돌/);
    assert.match(text,/현재 코드의 미해결 판정이 아닙니다/);
    assert.match(text,/길이 제한 대조 필요/);
  }
  const dest=exportHandoff(root,data,'PLAN-APP','artifacts/local/changed-intake');
  assert.match(fs.readFileSync(path.join(dest,'handoff.md'),'utf8'),/이전 입력에 기록된 충돌/);
  const packet=JSON.parse(fs.readFileSync(path.join(dest,'handoff.json'),'utf8'));
  assert.equal(packet.workItems[0].intake.state,'Changed');
  assert.equal(packet.workItems[0].intake.conflicts[0].blocking,true);
  assert.equal(packet.workItems[0].intake.approval.grantsExecution,false);
});

test('현재 입력의 미해결 충돌은 과거 기록으로 숨기지 않는다',()=>{
  const data=sample();data.workItems[0].intake=intakeSample();
  const allText=e=>[e.textContent,...e.children.map(allText)].join(' ');
  for(const text of [allText(viewerFixture(data).get('detail')),renderMarkdown(data),handoff(data,'PLAN-APP').markdown]){
    assert.doesNotMatch(text,/이전 입력에 기록된 충돌/);
    assert.doesNotMatch(text,/현재 코드의 미해결 판정이 아닙니다/);
    assert.match(text,/길이 제한 대조 필요/);
  }
});

test('제작 입력 JSON과 환경 원문은 브라우저 파일 allowlist에 자동 추가되지 않는다',()=>{
  const data=sample();data.workItems[0].intake=intakeSample();data.workItems[0].intake.profile={sources:[{path:'eng/inputs/profile.json'}]};
  assert.ok(!viewerFileRefs(data).has('eng/inputs/profile.json'));
});

test('실제 CLI 입력 점검은 미검토를 종료3으로 반환하고 파일을 생성하지 않는다',t=>{
  const root=fixture(t);fs.mkdirSync(path.join(root,'eng'),{recursive:true});
  fs.writeFileSync(path.join(root,'eng/links.json'),JSON.stringify({schemaVersion:'planning-app-production-links.v1',items:[{id:'menu',roles:[],planRefs:[]}]}));
  const cli=fileURLToPath(new URL('./cli.mjs',import.meta.url));
  const run=id=>spawnSync(process.execPath,[cli,'intake-check','--root',root,'--bindings','eng/links.json','--work-item',id],{encoding:'utf8'});
  const result=run('menu');assert.equal(result.status,3,result.stderr);assert.equal(JSON.parse(result.stdout).intake.state,'NotReviewed');
  assert.equal(fs.existsSync(path.join(root,'docs/AI/generated')),false);assert.equal(run('absent').status,1);
});
