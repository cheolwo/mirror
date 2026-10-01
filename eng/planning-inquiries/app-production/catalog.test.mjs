import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { buildIndex, hashFile, safePath } from './catalog.mjs';

const BINDINGS = 'eng/planning-inquiries/app-production/role-app-links.json';
const PLAN = 'docs/AI/Planning/시스템/PLAN-SYSTEM-EXAMPLE/README.md';

function fixture(t, files = {}, items = []) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'planning-catalog-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const write = (ref, content) => {
    const target = path.join(root, ref);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, typeof content === 'string' ? content : JSON.stringify(content));
  };
  write('docs/AI/PLANNING.md', `# 기획 목차\n\n## 시스템\n\n| 기획 ID | 원문 | 상태 |\n| --- | --- | --- |\n| \`PLAN-SYSTEM-EXAMPLE\` | [기본 r1](Planning/시스템/PLAN-SYSTEM-EXAMPLE/README.md) | \`Draft\` |\n`);
  write(PLAN, '# 기본 기획\n\n- 기획 ID: `PLAN-SYSTEM-EXAMPLE`\n- 판본: `r1`\n- 상태: `Draft`\n');
  write('eng/planning-inquiries/sources.json', { schemaVersion: 'planning-inquiry-sources.v1', extraSources: [] });
  write(BINDINGS, { schemaVersion: 'planning-app-production-links.v1', items });
  for (const [ref, body] of Object.entries(files)) write(ref, body);
  return { root, write };
}

test('inventory includes registered, recursive and configured legacy documents without choosing the largest revision', t => {
  const { root } = fixture(t, {
    'docs/AI/Planning/시스템/PLAN-SYSTEM-EXAMPLE/later.r999.md': '# 미래 제안\n- 기획 ID: `PLAN-SYSTEM-EXAMPLE`\n- 상태: `Asked`',
    'docs/AI/Planning/기타/PLAN-UNREGISTERED/README.md': '# 독립 문서\n- 기획 ID: `PLAN-UNREGISTERED`',
    'docs/AI/legacy.md': '# 예전 원문\n- 기획 ID: `PLAN-LEGACY`',
    'eng/planning-inquiries/sources.json': { extraSources: [{ path: 'docs/AI/legacy.md' }] }
  });
  const index = buildIndex(root);
  assert.equal(index.scope.registeredPlanCount, 1);
  assert.equal(index.scope.discoveredPlanCount, 3);
  assert.equal(index.scope.documentCount, 4);
  const plan = index.plans.find(p => p.id === 'PLAN-SYSTEM-EXAMPLE');
  assert.deepEqual(plan.canonicalRefs.map(ref => ref.path), [PLAN]);
  assert.equal(plan.declaredState, 'Draft');
  assert.equal(plan.codePresence, 'Unknown');
  assert.ok(plan.sourceRefs.some(ref => ref.path.endsWith('later.r999.md')));
});

test('compatibility comment maps the old directory ID to one registered plan and preserves its canonical anchor', t => {
  const old = 'docs/AI/Planning/스토리/PLAN-STORY-OLD/README.md';
  const current = 'docs/AI/Planning/스토리/PLAN-STORY-CURRENT/README.md';
  const { root } = fixture(t, {
    'docs/AI/PLANNING.md': '| `PLAN-STORY-CURRENT`<!-- compatibility-id: PLAN-STORY-OLD --> | [정본](Planning/스토리/PLAN-STORY-CURRENT/README.md) | `Confirmed` |',
    [old]: '# 호환 안내\n- 호환 기획 ID: `PLAN-STORY-OLD`\n- 현행 정본: [현재](../PLAN-STORY-CURRENT/README.md#scene)',
    [current]: '# 현재\n- 기획 ID: `PLAN-STORY-CURRENT`'
  });
  const index = buildIndex(root);
  assert.ok(!index.plans.some(plan => plan.id === 'PLAN-STORY-OLD'));
  const plan = index.plans.find(p => p.id === 'PLAN-STORY-CURRENT');
  assert.deepEqual(plan.compatibilityIds, ['PLAN-STORY-OLD']);
  assert.ok(plan.canonicalRefs.some(ref => ref.path === current && ref.anchor === 'scene'));
  assert.deepEqual(index.documents.find(doc => doc.path === old).planIds, ['PLAN-STORY-CURRENT']);
});

test('duplicate IDs, conflicting registrations and missing documents are diagnostics', t => {
  const { root } = fixture(t, { 'docs/AI/PLANNING.md': [
    '| `PLAN-SYSTEM-EXAMPLE` | [a](Planning/시스템/PLAN-SYSTEM-EXAMPLE/README.md) | `Draft` |',
    '| `PLAN-SYSTEM-EXAMPLE` | [b](missing.md) | `Draft` |',
    '| `PLAN-OTHER` | [a](Planning/시스템/PLAN-SYSTEM-EXAMPLE/README.md) | `Draft` |'
  ].join('\n') });
  const index = buildIndex(root);
  for (const code of ['DuplicateRegisteredId', 'ConflictingRegisteredDocument', 'MissingReference']) assert.ok(index.diagnostics.some(d => d.code === code), code);
  assert.ok(index.plans.find(p => p.id === 'PLAN-SYSTEM-EXAMPLE').sourceRefs.some(ref => ref.path === 'docs/AI/missing.md' && ref.sha256 === null));
});

test('result, generated and tool references are inventoried without becoming semantic plans', t => {
  const { root } = fixture(t, {
    'docs/AI/PLANNING.md': `| \`PLAN-SYSTEM-EXAMPLE\` | [기본](Planning/시스템/PLAN-SYSTEM-EXAMPLE/README.md) | \`Draft\` |\n[도구](../../eng/helper.md)\n[생성](generated/state.md)`,
    'eng/helper.md': '# 도구\n- 기획 ID: `PLAN-TOOL-NOT-A-PLAN`',
    'docs/AI/generated/state.md': '# 생성\n- 기획 ID: `PLAN-GENERATED-NOT-A-PLAN`',
    'docs/AI/Planning/시스템/PLAN-RESULT-NOT-A-PLAN/check.r1.result.md': '# 반환 결과\n- 기획 ID: `PLAN-RESULT-NOT-A-PLAN`'
  });
  const index = buildIndex(root);
  assert.equal(index.plans.length, 1);
  assert.deepEqual(index.documents.filter(doc => doc.kind !== 'PlanDocument').map(doc => doc.kind).sort(), ['GeneratedReference', 'ResultReference', 'ToolReference']);
});

test('product source existence, test source existence and execution are independent', t => {
  const items = [
    { id: 'menu', title: '메뉴', planRefs: [{ path: PLAN, revision: 'r1' }], roles: ['restaurant'],
      codeRefs: ['RestaurantDeskApp/Menu.razor'], testRefs: ['Ssalddel.Tests/MenuTests.cs'],
      roleLinks: [{ role: 'restaurant', codeRefs: ['RestaurantDeskApp/Menu.razor'], note: '메뉴 화면' }], evidenceRefs: [] },
    { id: 'only-test', planRefs: [{ path: PLAN }], codeRefs: ['Ssalddel.Tests/MenuTests.cs'], testRefs: ['Ssalddel.Tests/MenuTests.cs'] }
  ];
  const { root } = fixture(t, { 'RestaurantDeskApp/Menu.razor': '<p>메뉴</p>', 'Ssalddel.Tests/MenuTests.cs': 'class MenuTests {}' }, items);
  const index = buildIndex(root);
  const item = index.workItems.find(i => i.id === 'menu');
  assert.equal(item.codePresence, 'Present');
  assert.equal(item.intake.state, 'NotReviewed');
  assert.equal(item.testCodePresence, 'Present');
  assert.deepEqual(item.evidence, []);
  assert.equal(item.roleLinks[0].codeRefs[0].state, 'Present');
  assert.equal(index.workItems.find(i => i.id === 'only-test').codePresence, 'Unknown');
  assert.deepEqual(index.plans[0].workItemIds, ['menu', 'only-test']);
});

test('optional intake exposes readiness and nested source changes in the index without approving execution', t => {
  const { root, write } = fixture(t, { 'App/project.csproj':'<Project />', 'App/source.cs':'// input basis' });
  const source = { path:'App/source.cs',sha256:hashFile(root,'App/source.cs') };
  const fact = { status:'ExistingConfirmed',value:'기존 코드 대조',critical:true,sources:[source] };
  write('eng/input/profile.json',{schemaVersion:'planning-app-profile.v1',appId:'restaurant',projectPath:'App/project.csproj',platforms:['Android'],fields:Object.fromEntries(['navigation','presentation','authentication','server','testEnvironment'].map(k=>[k,fact]))});
  write('eng/input/task.json',{schemaVersion:'planning-app-intake.v1',workItemId:'menu',targetAppIds:['restaurant'],impactedRoles:['restaurant'],sections:Object.fromEntries(['purpose','information','workflow','uiStates','recovery','deliverables'].map(k=>[k,fact])),questions:[],conflicts:[],deliverables:[{id:'test',kind:'AutomatedTest',required:true}]});
  write('eng/input/review.json',{schemaVersion:'planning-app-intake-review.v1',workItemId:'menu',reviewedInputs:[],confirmation:{state:'Pending'},executionApproval:{state:'NotGranted',workOrderRefs:[]},environment:[]});
  const refs=Object.fromEntries([['appProfileRef','profile'],['intakeRef','task'],['intakeReviewRef','review']].map(([key,name])=>[key,{path:`eng/input/${name}.json`,sha256:hashFile(root,`eng/input/${name}.json`)}]));
  write(BINDINGS,{schemaVersion:'planning-app-production-links.v1',items:[{id:'menu',planRefs:[{path:PLAN,revision:'r1'}],roles:['restaurant'],...refs}]});
  const before=buildIndex(root);assert.equal(before.workItems[0].intake.state,'NeedsInformation');
  assert.equal(before.workItems[0].intake.approval.grantsExecution,false);
  assert.equal(before.workItems[0].intake.sourceRefs.length,3);assert.ok(before.inputs.some(ref=>ref.path==='App/source.cs'));
  write('App/source.cs','// changed basis');const after=buildIndex(root);
  assert.equal(after.workItems[0].intake.state,'Changed');assert.notEqual(after.fingerprint,before.fingerprint);
});

test('historical report stays Unknown and does not expose raw content', t => {
  const { root } = fixture(t, { 'docs/AI/result.md': '# 과거 검증\nPassed 42 tests\nsecret: do-not-copy-me' }, [{
    id: 'historical', planRefs: [{ path: PLAN }], evidenceRefs: [{ path: 'docs/AI/result.md', kind: 'UnitTest', format: 'HistoricalReport', summary: '과거 보고서' }]
  }]);
  const index = buildIndex(root);
  assert.equal(index.workItems[0].evidence[0].freshness, 'Unknown');
  assert.ok(!JSON.stringify(index).includes('do-not-copy-me'));
  assert.ok(!JSON.stringify(index).includes('Passed 42 tests'));
});

test('work-order plan hash mismatch is reported without changing source status', t => {
  const order = 'docs/AI/Planning/시스템/PLAN-SYSTEM-EXAMPLE/first.work-order.json';
  const { root } = fixture(t, { [order]: { designDocumentRef: 'README.md', designDocumentSha256: '0'.repeat(64) } }, [{
    id: 'order', planRefs: [{ path: PLAN }], workOrderRefs: [order]
  }]);
  const index = buildIndex(root);
  assert.ok(index.diagnostics.some(d => d.code === 'WorkOrderPlanHashMismatch'));
  assert.equal(index.plans[0].declaredState, 'Draft');
});

test('identical input is deterministic and edited input changes the fingerprint', t => {
  const { root, write } = fixture(t);
  const a = buildIndex(root);
  assert.deepEqual(buildIndex(root), a);
  write(PLAN, '# 수정\n- 기획 ID: `PLAN-SYSTEM-EXAMPLE`');
  const b = buildIndex(root);
  assert.notEqual(a.fingerprint, b.fingerprint);
  assert.notEqual(a.documents[0].sha256, b.documents[0].sha256);
  assert.equal(hashFile(root, PLAN), b.documents[0].sha256);
});

test('outside paths, absolute paths and external links cannot enter the inventory', t => {
  const { root } = fixture(t, { 'docs/AI/PLANNING.md': '[outside](../../../outside.md)\n[web](https://example.org/plan.md)\n<script>alert(1)</script>' }, [{
    id: 'unsafe', planRefs: [{ path: '../private.md' }], codeRefs: ['C:/private.cs', '../secret.cs']
  }]);
  for (const ref of ['../private.md', 'C:/private.cs', '/tmp/private', '\\\\server\\private']) assert.throws(() => safePath(root, ref));
  const index = buildIndex(root);
  assert.ok(index.diagnostics.some(d => d.code === 'UnsafePath'));
  assert.ok(!index.inputs.some(input => /outside|private|secret|example\.org/.test(input.path)));
  assert.ok(!JSON.stringify(index).includes('alert(1)'));
});

test('symlink escape is rejected, including a missing file below the escaped directory', t => {
  const { root } = fixture(t);
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'planning-outside-'));
  t.after(() => fs.rmSync(outside, { recursive: true, force: true }));
  try { fs.symlinkSync(outside, path.join(root, 'escaped'), process.platform === 'win32' ? 'junction' : 'dir'); }
  catch (error) { if (error.code === 'EPERM') { t.skip('Symbolic link permission unavailable'); return; } throw error; }
  assert.throws(() => safePath(root, 'escaped/missing.txt'));
  assert.throws(() => hashFile(root, 'escaped/missing.txt'));
});

test('incidental PLAN mentions in the body do not reassign a bound work item', t => {
  const { root } = fixture(t, { [PLAN]: '# 기본\n- 기획 ID: `PLAN-SYSTEM-EXAMPLE`\n\n관련 기획 `PLAN-UNRELATED`를 참고한다.' }, [{ id: 'scoped', planRefs: [{ path: PLAN }] }]);
  const index = buildIndex(root);
  assert.deepEqual(index.plans.map(plan => plan.id), ['PLAN-SYSTEM-EXAMPLE']);
  assert.deepEqual(index.plans[0].workItemIds, ['scoped']);
});

test('generated output references cannot invalidate their own fingerprint', t => {
  const { root, write } = fixture(t, {
    'docs/AI/PLANNING.md': `| \`PLAN-SYSTEM-EXAMPLE\` | [기본](Planning/시스템/PLAN-SYSTEM-EXAMPLE/README.md) | \`Draft\` |\n[생성](generated/planning-app-production.md)`,
    [PLAN]: '# 기본\n- 기획 ID: `PLAN-SYSTEM-EXAMPLE`\n현행 정본: [이 문서](README.md). [생성](../../../generated/planning-app-production.md)을 참고한다.'
  });
  const initial = buildIndex(root);
  write('docs/AI/generated/planning-app-production.md', '# 생성 결과\nfirst content');
  const generated = buildIndex(root);
  assert.equal(generated.fingerprint, initial.fingerprint);
  write('docs/AI/generated/planning-app-production.md', '# 다른 생성 결과');
  assert.equal(buildIndex(root).fingerprint, initial.fingerprint);
  assert.ok(generated.documents.some(d => d.path.endsWith('planning-app-production.md') && d.kind === 'GeneratedReference' && d.sha256 === null));
  assert.ok(!generated.inputs.some(input => input.path.endsWith('planning-app-production.md')));
  assert.ok(!generated.diagnostics.some(d => d.code === 'SelfGeneratedInputExcluded'));
});

test('shared supplemental links retain semantic ownership and are not conflicting registrations', t => {
  const other = 'docs/AI/Planning/시스템/PLAN-OTHER/README.md';
  const { root } = fixture(t, {
    'docs/AI/PLANNING.md': `| \`PLAN-SYSTEM-EXAMPLE\` | [기본](Planning/시스템/PLAN-SYSTEM-EXAMPLE/README.md) | \`Draft\` |\n| \`PLAN-OTHER\` | [다른](Planning/시스템/PLAN-OTHER/README.md) · [참고](Planning/시스템/PLAN-SYSTEM-EXAMPLE/README.md) | \`Draft\` |`,
    [other]: '# 다른 기획\n- 기획 ID: `PLAN-OTHER`'
  }, [{ id: 'source-owned', planRefs: [{ path: PLAN }] }]);
  const index = buildIndex(root);
  assert.ok(!index.diagnostics.some(d => d.code === 'ConflictingRegisteredDocument'));
  assert.deepEqual(index.documents.find(d => d.path === PLAN).planIds, ['PLAN-SYSTEM-EXAMPLE']);
  assert.deepEqual(index.plans.find(p => p.id === 'PLAN-OTHER').workItemIds, []);
});

test('missing bound files are errors and fixed hashes compare case-insensitively', t => {
  const { root, write } = fixture(t);
  write(BINDINGS, { schemaVersion: 'planning-app-production-links.v1', items: [{ id: 'hashes', planRefs: [{ path: PLAN, sha256: hashFile(root, PLAN).toLowerCase() }], codeRefs: ['Product/missing.cs'] }] });
  const index = buildIndex(root);
  assert.ok(index.diagnostics.some(d => d.code === 'BindingReferenceMissing' && d.severity === 'Error'));
  assert.ok(!index.diagnostics.some(d => d.code === 'ReferenceHashMismatch'));
});
