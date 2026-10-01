import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { beginEvidence, completeEvidence, evaluateEvidence, evidencePath } from './evidence.mjs';

const sha = value => crypto.createHash('sha256').update(value).digest('hex');
function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'planning-evidence-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  function put(relative, value) {
    const target = path.join(root, relative);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, typeof value === 'string' ? value : JSON.stringify(value));
  }
  put('plan.md', '# 승인 r1'); put('App/Main.cs', 'class Main {}'); put('Tests/MainTests.cs', 'class MainTests {}');
  put('eng/planning-inquiries/app-production/evidence.mjs', '// fixture tool');
  put('eng/planning-inquiries/app-production/intake.mjs', '// fixture intake tool');
  put('eng/common/planning-evidence.ps1', '# fixture bridge');
  const item = { id: 'APP-TEST', title: '테스트', planRefs: [{ path: 'plan.md', revision: 'r1', sha256: sha('# 승인 r1') }],
    roles: ['restaurant'], codeRefs: ['App/Main.cs'], testRefs: ['Tests/MainTests.cs'], roleLinks: [], workOrderRefs: [], resultRefs: [], evidenceRefs: [], remaining: [] };
  const linksPath = 'links.json';
  const links = () => put(linksPath, { schemaVersion: 'planning-app-production-links.v1', items: [item] });
  links();
  const options = { workItemId: item.id, linksPath, startPath: 'results/run.start.json', kind: 'Validation',
    scope: { level: 'Fast', testProjects: ['Tests/Tests.csproj'], testModes: ['Targeted'], password: 'must-not-serialize' },
    environment: { token: 'must-not-serialize', powershell: '7.5.0' } };
  const start = () => beginEvidence(root, options);
  const finish = (extra = {}) => completeEvidence(root, options.startPath, { result: 'Passed', checks: [{ name: 'focused fixture tests', result: 'Passed', exitCode: 0 }], ...extra });
  const ref = { path: 'results/run.json', kind: 'Validation', format: 'EvidenceManifest' };
  return { root, item, put, links, options, start, finish, ref, evaluate: () => evaluateEvidence(root, ref, item) };
}

function attachIntake(f) {
  f.put('requirements/menu.md', '# 메뉴 입력 근거');
  const source = { path: 'requirements/menu.md', sha256: sha('# 메뉴 입력 근거'), anchor: 'menu' };
  f.put('App/App.csproj', '<Project/>');
  const profile = { schemaVersion: 'planning-app-profile.v1', appId: 'restaurant', projectPath: 'App/App.csproj',
    fields: { navigation: { sources: [source] } } };
  const intake = { schemaVersion: 'planning-app-intake.v1', workItemId: f.item.id,
    sections: { purpose: { sources: [source] } } };
  f.put('inputs/profile.json', profile); f.put('inputs/task.json', intake);
  f.item.appProfileRef = { path: 'inputs/profile.json', sha256: sha(JSON.stringify(profile)) };
  f.item.intakeRef = { path: 'inputs/task.json', sha256: sha(JSON.stringify(intake)) };
  const review = { schemaVersion: 'planning-app-intake-review.v1', status: 'Pending',
    reviewedInputs: [f.item.appProfileRef, f.item.intakeRef], executionApproval: { workOrderRefs: [] } };
  f.put('inputs/review.json', review);
  f.item.intakeReviewRef = { path: 'inputs/review.json', sha256: sha(JSON.stringify(review)) };
  f.links();
  return { profile, intake, review };
}

test('start is persisted before execution and cannot be displayed as a pass', t => {
  const f = fixture(t); f.start();
  assert.ok(fs.existsSync(path.join(f.root, f.options.startPath)));
  const draft = evaluateEvidence(f.root, { ...f.ref, path: f.options.startPath }, f.item);
  assert.equal(draft.result, 'Unknown'); assert.equal(draft.freshness, 'Unknown');
  assert.equal(fs.existsSync(path.join(f.root, f.ref.path)), false);
});

test('completed scoped pass has exact inputs and sanitized environment', t => {
  const f = fixture(t); f.start(); f.finish();
  const evidence = f.evaluate();
  assert.equal(evidence.result, 'Passed'); assert.equal(evidence.freshness, 'Current');
  const raw = fs.readFileSync(path.join(f.root, f.ref.path), 'utf8');
  assert.equal(raw.includes('must-not-serialize'), false);
  assert.ok(evidence.inputs.some(input => input.path === 'App/Main.cs' && input.sha256 === sha('class Main {}')));
  assert.equal(evidence.scope.coverage, 'DeclaredInputsOnly');
});

test('failed run can be current without becoming passed', t => {
  const f = fixture(t); f.start(); f.finish({ result: 'Failed', checks: [{ name: 'fixture test', result: 'Failed', exitCode: 1, rawLog: 'secret' }] });
  assert.equal(f.evaluate().result, 'Failed'); assert.equal(f.evaluate().freshness, 'Current');
  assert.equal(fs.readFileSync(path.join(f.root, f.ref.path), 'utf8').includes('rawLog'), false);
});

test('a validation manifest cannot be relabeled as current device evidence', t => {
  const f = fixture(t); f.start(); f.finish();
  const evidence = evaluateEvidence(f.root, { ...f.ref, kind: 'Device' }, f.item);
  assert.equal(evidence.kind, 'Validation');
  assert.equal(evidence.result, 'Unknown'); assert.equal(evidence.freshness, 'Unknown');
  assert.equal(evidence.diagnostics[0].code, 'EvidenceKindMismatch');
});

test('actual manifest kind is used when attachment omits a kind', t => {
  const f = fixture(t); f.start(); f.finish();
  const evidence = evaluateEvidence(f.root, { path: f.ref.path, format: f.ref.format }, f.item);
  assert.equal(evidence.kind, 'Validation');
  assert.equal(evidence.result, 'Passed'); assert.equal(evidence.freshness, 'Current');
});

test('changing final manifest kind cannot bypass the execution-start kind', t => {
  const f = fixture(t); f.start(); f.finish();
  const manifest = JSON.parse(fs.readFileSync(path.join(f.root, f.ref.path), 'utf8'));
  f.put(f.ref.path, { ...manifest, kind: 'Device' });
  const evidence = evaluateEvidence(f.root, { ...f.ref, kind: 'Device' }, f.item);
  assert.equal(evidence.result, 'Unknown'); assert.equal(evidence.freshness, 'Unknown');
  assert.equal(evidence.diagnostics[0].code, 'EvidenceBaselineMismatch');
});

test('failed check cannot produce an aggregate passed result', t => {
  const f = fixture(t); f.start(); f.finish({ checks: [{ name: 'fixture test', result: 'Failed', exitCode: 1 }] });
  assert.equal(f.evaluate().result, 'Failed');
});

test('historical reports are never retroactively current or passed', t => {
  const f = fixture(t); f.put('old.md', '# Passed 100/100\nSECRET_RAW_LOG');
  const evidence = evaluateEvidence(f.root, { path: 'old.md', kind: 'Test', format: 'HistoricalReport', summary: '과거 보고' }, f.item);
  assert.equal(evidence.result, 'Unknown'); assert.equal(evidence.freshness, 'Unknown');
  assert.equal(JSON.stringify(evidence).includes('SECRET_RAW_LOG'), false);
  assert.equal(evidence.summary, '과거 보고');
});

test('missing and undeclared formats remain distinct', t => {
  const f = fixture(t);
  assert.equal(evaluateEvidence(f.root, { path: 'absent.md', format: 'HistoricalReport' }, f.item).freshness, 'Missing');
  assert.equal(evaluateEvidence(f.root, { path: 'plan.md' }, f.item).freshness, 'Unknown');
});

test('source modified during run remains stale even if later restored', t => {
  const f = fixture(t); f.start(); f.put('App/Main.cs', 'class Changed {}'); f.finish();
  assert.equal(f.evaluate().freshness, 'Stale');
  f.put('App/Main.cs', 'class Main {}');
  assert.equal(f.evaluate().freshness, 'Stale');
});

test('source modified after execution becomes stale', t => {
  const f = fixture(t); f.start(); f.finish(); f.put('Tests/MainTests.cs', 'class NewTests {}');
  assert.equal(f.evaluate().freshness, 'Stale');
});

test('added critical source under declared directory invalidates freshness', t => {
  const f = fixture(t); f.start(); f.finish(); f.put('App/New.cs', 'class New {}');
  assert.equal(f.evaluate().freshness, 'Stale');
});

test('deleted critical source invalidates freshness', t => {
  const f = fixture(t); f.start(); f.finish(); fs.unlinkSync(path.join(f.root, 'App/Main.cs'));
  assert.equal(f.evaluate().freshness, 'Stale');
});

test('build outputs excluded from source directory inventories', t => {
  const f = fixture(t); f.start(); f.put('App/obj/generated.cs', '// generated'); f.finish();
  assert.equal(f.evaluate().freshness, 'Current');
});

test('artifact mutation and disappearance are distinct from successful result', t => {
  const f = fixture(t); f.start(); f.put('results/app.apk', 'fixture artifact'); f.finish({ artifactPaths: ['results/app.apk'] });
  assert.equal(f.evaluate().freshness, 'Current');
  f.put('results/app.apk', 'tampered'); assert.equal(f.evaluate().freshness, 'Stale');
  fs.unlinkSync(path.join(f.root, 'results/app.apk')); assert.equal(f.evaluate().freshness, 'Missing');
  assert.equal(f.evaluate().result, 'Passed');
});

test('missing pre-execution baseline cannot be recreated from current input', t => {
  const f = fixture(t); f.start(); f.finish(); fs.unlinkSync(path.join(f.root, f.options.startPath));
  assert.equal(f.evaluate().freshness, 'Missing');
  assert.equal(f.evaluate().result, 'Unknown');
});

test('changed pre-execution baseline is rejected', t => {
  const f = fixture(t); f.start(); f.finish(); f.put(f.options.startPath, '{}');
  assert.equal(f.evaluate().freshness, 'Stale'); assert.equal(f.evaluate().result, 'Unknown');
});

test('adding evidence attachment does not change the work definition', t => {
  const f = fixture(t); f.start(); f.finish(); f.item.evidenceRefs.push(f.ref); f.links();
  assert.equal(f.evaluate().freshness, 'Current');
});

test('changing role mapping or plan revision invalidates the binding', t => {
  const f = fixture(t); f.start(); f.finish(); f.item.roles.push('driver'); f.links();
  assert.equal(f.evaluate().freshness, 'Stale');
});

test('plan hash mismatch blocks capture before work starts', t => {
  const f = fixture(t); f.put('plan.md', '# changed');
  assert.throws(f.start, /PlanningPlanHashMismatch/);
  assert.equal(fs.existsSync(path.join(f.root, f.options.startPath)), false);
});

test('path traversal, absolute paths, streams and unsafe URI references are rejected', t => {
  const f = fixture(t);
  for (const value of ['../outside', '/etc/passwd', 'C:\\secret', 'plan.md:secret', 'https://example.org', 'App/../plan.md', 'App/Main.cs.']) {
    assert.throws(() => evidencePath(f.root, value));
    const result = evaluateEvidence(f.root, { path: value, format: 'EvidenceManifest' }, f.item);
    assert.equal(result.freshness, 'Unknown'); assert.equal(result.path, '');
  }
});

test('junction escape for existing and missing paths is rejected', t => {
  const f = fixture(t); const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'planning-outside-'));
  t.after(() => fs.rmSync(outside, { recursive: true, force: true }));
  fs.writeFileSync(path.join(outside, 'secret.json'), '{}');
  try { fs.symlinkSync(outside, path.join(f.root, 'external'), 'junction'); }
  catch (error) { if (error.code === 'EPERM') { t.skip('junction creation requires permission'); return; } throw error; }
  assert.throws(() => evidencePath(f.root, 'external/secret.json'), /EvidencePathEscape/);
  assert.throws(() => evidencePath(f.root, 'external/missing.json'), /EvidencePathEscape/);
});

test('malformed manifest, wrong work item, invalid dates and tampered source baseline are not current', t => {
  const f = fixture(t); f.start(); f.finish(); const original = JSON.parse(fs.readFileSync(path.join(f.root, f.ref.path), 'utf8'));
  const variants = [ { ...original, schemaVersion: 'old' }, { ...original, workItemId: 'wrong' },
    { ...original, execution: { ...original.execution, finishedAt: '1900-01-01' } },
    { ...original, sourceInputs: [] }, { ...original, sourceInputs: [{ path: 'plan.md', state: 'Present', sha256: 'bad' }] } ];
  for (const manifest of variants) { f.put(f.ref.path, manifest); assert.equal(f.evaluate().freshness, 'Unknown'); }
});

test('result absence, not run and not applicable remain independent states', t => {
  const f = fixture(t); f.start(); f.finish({ result: 'NotRun', checks: [] });
  assert.equal(f.evaluate().result, 'NotRun'); assert.equal(f.evaluate().freshness, 'Current');
});

test('capture cannot overwrite an existing start or completed manifest', t => {
  const f = fixture(t); f.start(); assert.throws(f.start, /EvidenceOutputAlreadyExists/);
  f.finish(); assert.throws(f.finish, /EvidenceOutputAlreadyExists/);
});

test('unsafe artifact cannot leak through a stale manifest', t => {
  const f = fixture(t); f.start(); f.finish();
  const manifest = JSON.parse(fs.readFileSync(path.join(f.root, f.ref.path), 'utf8'));
  manifest.artifacts = [{ path: '../secret.txt', state: 'Present', sha256: sha('secret') }];
  f.put(f.ref.path, manifest); f.put('App/Main.cs', 'changed');
  const evidence = f.evaluate();
  assert.equal(evidence.freshness, 'Unknown'); assert.equal(evidence.artifacts, undefined);
  assert.equal(JSON.stringify(evidence).includes('../secret.txt'), false);
});

test('selected registry file in a source directory permits adding only an attachment', t => {
  const f = fixture(t); f.options.linksPath = 'App/links.json';
  f.put(f.options.linksPath, { schemaVersion: 'planning-app-production-links.v1', items: [f.item] });
  f.start(); f.finish();
  f.item.evidenceRefs.push(f.ref);
  f.put(f.options.linksPath, { schemaVersion: 'planning-app-production-links.v1', items: [f.item] });
  assert.equal(f.evaluate().freshness, 'Current');
});

test('optional PowerShell validation bridge captures a real scoped diff check without building apps', t => {
  const f = fixture(t);
  const actualRoot = fileURLToPath(new URL('../../../', import.meta.url));
  for (const relative of ['eng/planning-inquiries/app-production/evidence.mjs', 'eng/planning-inquiries/app-production/intake.mjs',
    'eng/common/planning-evidence.ps1', 'eng/validate-changes.ps1']) {
    f.put(relative, fs.readFileSync(path.join(actualRoot, relative), 'utf8'));
  }
  execFileSync('git', ['init', '--quiet', f.root], { stdio: 'pipe' });
  const result = execFileSync('pwsh', ['-NoProfile', '-File', path.join(f.root, 'eng/validate-changes.ps1'), '-Paths', 'plan.md',
    '-PlanningWorkItemId', f.item.id, '-PlanningLinksPath', 'links.json'], { encoding: 'utf8', cwd: f.root, stdio: 'pipe' });
  assert.match(result, /Planning evidence:/);
  const runDir = fs.readdirSync(path.join(f.root, 'artifacts/local/validation'))[0];
  assert.match(runDir, /^\d{8}-\d{6}-[a-f0-9]{32}$/, 'optional evidence runs cannot collide within one second');
  const ref = { ...f.ref, path: `artifacts/local/validation/${runDir}/planning-evidence.json` };
  const evidence = evaluateEvidence(f.root, ref, f.item);
  assert.equal(evidence.result, 'Passed'); assert.equal(evidence.freshness, 'Current');
  assert.equal(evidence.scope.buildTargets.length, 0); assert.equal(evidence.scope.testProjects.length, 0);
  assert.equal(evidence.artifacts.length, 1); assert.match(evidence.artifacts[0].path, /diff-check\.log$/);
});

test('unlinked legacy work keeps its r8 selected-binding hash without optional intake fields', t => {
  const f = fixture(t); f.start();
  const stable = value => JSON.stringify(value, (_key, item) => item && typeof item === 'object' && !Array.isArray(item)
    ? Object.fromEntries(Object.entries(item).sort(([a], [b]) => a.localeCompare(b, 'en'))) : item);
  const legacyBinding = { id: f.item.id, planRefs: f.item.planRefs, roles: f.item.roles, codeRefs: f.item.codeRefs,
    testRefs: f.item.testRefs, roleLinks: [], workOrderRefs: [] };
  const manifest = JSON.parse(fs.readFileSync(path.join(f.root, f.options.startPath), 'utf8'));
  assert.equal(manifest.binding.itemSha256, sha(stable(legacyBinding)));
  assert.equal(manifest.sourceInputs.some(input => input.path.endsWith('/intake.mjs')), false);
});

test('dangling junction evidence paths are rejected instead of treated as missing inputs', t => {
  const f = fixture(t); const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'planning-dangling-'));
  const linked = path.join(f.root, 'dangling');
  try { fs.symlinkSync(outside, linked, 'junction'); }
  catch (error) { fs.rmSync(outside, { recursive: true, force: true }); if (error.code === 'EPERM') { t.skip('junction unavailable'); return; } throw error; }
  fs.rmSync(outside, { recursive: true, force: true });
  assert.throws(() => evidencePath(f.root, 'dangling/manifest.json'), /EvidencePathEscape/);
});

test('linked intake documents and nested sources are fixed before evidence without granting approval', t => {
  const f = fixture(t); attachIntake(f); f.start(); f.finish();
  assert.equal(f.evaluate().freshness, 'Current');
  const manifest = JSON.parse(fs.readFileSync(path.join(f.root, f.options.startPath), 'utf8'));
  for (const relative of ['inputs/profile.json', 'inputs/task.json', 'inputs/review.json',
    'requirements/menu.md', 'App/App.csproj', 'eng/planning-inquiries/app-production/intake.mjs']) {
    assert.ok(manifest.sourceInputs.some(input => input.path === relative), relative);
  }
  const review = JSON.parse(fs.readFileSync(path.join(f.root, 'inputs/review.json'), 'utf8'));
  assert.equal(review.status, 'Pending');
  assert.equal(f.evaluate().approval, undefined);
});

test('nested input source changes stale only the linked evidence even outside code inventory', t => {
  const f = fixture(t); attachIntake(f); f.start(); f.finish();
  f.put('requirements/menu.md', '# changed requirement');
  const evidence = f.evaluate();
  assert.equal(evidence.result, 'Passed'); assert.equal(evidence.freshness, 'Stale');
  assert.equal(evidence.diagnostics[0].code, 'EvidenceIntakeInputsChanged');
});

test('review or top-level intake reference changes cannot retain evidence currentness', t => {
  const f = fixture(t); const { review } = attachIntake(f); f.start(); f.finish();
  review.status = 'Confirmed'; f.put('inputs/review.json', review);
  f.item.intakeReviewRef.sha256 = sha(JSON.stringify(review)); f.links();
  assert.equal(f.evaluate().freshness, 'Stale');
  assert.equal(f.evaluate().diagnostics[0].code, 'EvidenceBindingChanged');
});

test('missing or mismatched intake sources block evidence capture before execution', t => {
  const f = fixture(t); attachIntake(f);
  f.put('requirements/menu.md', '# changed requirement');
  assert.throws(f.start, /PlanningIntakeInputInvalid/);
  fs.unlinkSync(path.join(f.root, 'requirements/menu.md'));
  assert.throws(f.start, /PlanningIntakeInputInvalid/);
  assert.equal(fs.existsSync(path.join(f.root, f.options.startPath)), false);
});

test('opting a legacy work item into intake makes older unbound evidence stale', t => {
  const f = fixture(t); f.start(); f.finish(); attachIntake(f);
  assert.equal(f.evaluate().freshness, 'Stale');
  assert.equal(f.evaluate().diagnostics[0].code, 'EvidenceBindingChanged');
});

test('unlinked input documents do not invalidate unrelated selected work evidence', t => {
  const f = fixture(t); f.put('inputs/other-task.json', { status: 'Pending' }); f.start(); f.finish();
  f.put('inputs/other-task.json', { status: 'Confirmed' });
  assert.equal(f.evaluate().freshness, 'Current');
});
