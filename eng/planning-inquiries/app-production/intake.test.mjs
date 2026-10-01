import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { collectIntakeInputs, evaluateIntake } from './intake.mjs';

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'app-intake-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const write = (name, content) => {
    const target = path.join(root, name);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, typeof content === 'string' ? content : JSON.stringify(content));
  };
  const ref = name => ({ path: name, sha256: createHash('sha256').update(fs.readFileSync(path.join(root, name))).digest('hex') });
  write('docs/plan.md', '# 계획\n\n## 기준\n입력 근거\n');
  write('App/App.csproj', '<Project />');
  const fact = () => ({ status: 'ExistingConfirmed', value: '기존 근거 기반 입력', critical: true, sources: [{ ...ref('docs/plan.md'), revision: 'r1', anchor: '기준' }] });
  const profile = { schemaVersion: 'planning-app-profile.v1', appId: 'restaurant', projectPath: 'App/App.csproj', platforms: ['Android'], fields: Object.fromEntries(['navigation', 'presentation', 'authentication', 'server', 'testEnvironment'].map(key => [key, fact()])) };
  const intake = { schemaVersion: 'planning-app-intake.v1', workItemId: 'APP-MENU', targetAppIds: ['restaurant'], impactedRoles: ['restaurant', 'orderer'], sections: Object.fromEntries(['purpose', 'information', 'workflow', 'uiStates', 'recovery', 'deliverables'].map(key => [key, fact()])), questions: [], conflicts: [], deliverables: [{ id: 'test', kind: 'AutomatedTest', required: true }, { id: 'apk', kind: 'AndroidPackage', required: true }] };
  const review = { schemaVersion: 'planning-app-intake-review.v1', workItemId: 'APP-MENU', reviewedInputs: [], confirmation: { state: 'Pending' }, executionApproval: { state: 'NotGranted', workOrderRefs: [] }, environment: [{ id: 'test-env', state: 'Ready', deliverableIds: ['test'], reason: '격리 시험 준비' }, { id: 'android-env', state: 'Unknown', deliverableIds: ['apk'], reason: '서명 환경 미검토' }] };
  const item = { id: 'APP-MENU', planRefs: [ref('docs/plan.md')] };
  const save = () => {
    write('inputs/app.profile.json', profile);
    write('inputs/task.intake.json', intake);
    write('inputs/task.review.json', review);
    Object.assign(item, { appProfileRef: ref('inputs/app.profile.json'), intakeRef: ref('inputs/task.intake.json'), intakeReviewRef: ref('inputs/task.review.json') });
  };
  const confirm = () => {
    save();
    review.reviewedInputs = [item.appProfileRef, item.intakeRef];
    review.confirmation = { state: 'Confirmed', confirmedBy: 'Human', confirmedAt: '2026-09-27T04:00:00Z' };
    save();
  };
  save();
  return { root, write, ref, fact, profile, intake, review, item, save, confirm, evaluate: () => evaluateIntake(root, item) };
}

test('legacy work items without intake remain NotReviewed, not missing implementation', () => {
  assert.equal(evaluateIntake('.', { id: 'legacy' }).state, 'NotReviewed');
  assert.deepEqual(collectIntakeInputs('.', { id: 'legacy' }), []);
});

test('explicit null or empty refs opt into validation instead of legacy absence', () => {
  for (const value of [null, '', false]) {
    const item = { id: 'explicit-invalid', intakeRef: value };
    assert.equal(evaluateIntake('.', item).state, 'NeedsInformation');
    assert.ok(collectIntakeInputs('.', item).some(ref => ref.state === 'Invalid'));
  }
});

test('AI complete draft does not imply human confirmation or execution approval', t => {
  const f = fixture(t);
  const result = f.evaluate();
  assert.equal(result.inputState, 'Ready');
  assert.equal(result.state, 'NeedsInformation');
  assert.equal(result.reviewState, 'Pending');
  assert.equal(result.approval.state, 'NotGranted');
  assert.equal(result.approval.grantsExecution, false);
  assert.deepEqual(result.targetAppIds, ['restaurant']);
  assert.deepEqual(result.impactedRoles, ['restaurant', 'orderer']);
});

test('human-confirmed input remains independently blocked for execution and APK environment', t => {
  const f = fixture(t);
  f.confirm();
  const result = f.evaluate();
  assert.equal(result.state, 'Confirmed');
  assert.equal(result.approval.state, 'NotGranted');
  assert.ok(result.blockers.some(value => value.code === 'IntakeExecutionNotApproved'));
  assert.deepEqual(result.environment.blockedDeliverableIds, ['apk']);
  assert.equal(result.environment.checks.find(check => check.id === 'test-env').state, 'Ready');
});

test('missing facts, sources, required lists and unjustified exclusions block readiness', t => {
  const f = fixture(t);
  delete f.intake.sections.workflow;
  f.intake.sections.purpose.sources = [];
  f.intake.sections.recovery = { status: 'NotApplicable', critical: true, value: '', sources: [] };
  delete f.intake.questions;
  f.save();
  const result = f.evaluate();
  assert.equal(result.inputState, 'Incomplete');
  for (const code of ['IntakeFactMissing', 'IntakeSourceMissing', 'IntakeExclusionReasonMissing', 'IntakeListMissing']) assert.ok(result.diagnostics.some(value => value.code === code), code);
});

test('critical unresolved input, questions and conflicts block; explicit exclusion does not', t => {
  const f = fixture(t);
  f.intake.sections.recovery = { status: 'Unresolved', critical: true, value: '', sources: [] };
  f.intake.questions = [{ id: 'scope', text: '범위 확인', critical: true }];
  f.intake.conflicts = [{ id: 'auth', description: '권한 정책 상충', blocking: true }];
  f.save();
  let result = f.evaluate();
  for (const code of ['IntakeCriticalUnresolved', 'IntakeCriticalQuestion', 'IntakeBlockingConflict']) assert.ok(result.diagnostics.some(value => value.code === code), code);
  f.intake.sections.recovery = { status: 'NotApplicable', critical: true, value: '', reason: '이 작업은 읽기 전용 목록 도구로 상태 변경을 하지 않음', sources: [] };
  f.intake.questions = [];
  f.intake.conflicts = [];
  f.save();
  assert.equal(f.evaluate().inputState, 'Ready');
});

test('source changes yield Changed without deleting historical human review', t => {
  const f = fixture(t);
  f.confirm();
  f.write('docs/plan.md', '# 계획 변경');
  const result = f.evaluate();
  assert.equal(result.state, 'Changed');
  assert.equal(result.reviewState, 'Confirmed');
  assert.ok(result.inputs.some(input => input.path === 'docs/plan.md' && input.state === 'HashMismatch'));
});

test('updated task ref cannot silently reuse an older confirmed snapshot', t => {
  const f = fixture(t);
  f.confirm();
  f.intake.sections.workflow.value = '새 작업 흐름';
  f.save();
  assert.equal(f.evaluate().state, 'Changed');
  assert.notEqual(f.evaluate().reviewState, 'Confirmed');
});

test('missing references and unknown schemas are reported safely', t => {
  const f = fixture(t);
  f.profile.schemaVersion = 'unrecognized';
  f.intake.sections.purpose.sources = [{ path: 'missing.md', sha256: '0'.repeat(64) }];
  f.save();
  delete f.item.intakeReviewRef;
  const result = f.evaluate();
  for (const code of ['IntakeSchemaInvalid', 'IntakeMissing', 'IntakeReferenceMissing']) assert.ok(result.diagnostics.some(value => value.code === code), code);
  assert.equal(result.profile, null);
});

test('path traversal, URI and # path fragments are rejected; anchor is separate', t => {
  const f = fixture(t);
  for (const bad of ['../private.json', 'file:///private.json', 'docs/plan.md#기준']) {
    f.item.appProfileRef = { path: bad, sha256: 'a'.repeat(64) };
    const result = f.evaluate();
    assert.ok(result.inputs.some(input => input.state === 'Unsafe'));
    assert.ok(!JSON.stringify(result).includes(bad));
  }
  f.save();
  assert.ok(f.evaluate().sections.purpose.sources.some(source => source.anchor === '기준' && source.state === 'Present'));
});

test('external and dangling symlink ancestors are rejected', t => {
  const f = fixture(t);
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'app-intake-outside-'));
  t.after(() => fs.rmSync(outside, { recursive: true, force: true }));
  try { fs.symlinkSync(outside, path.join(f.root, 'linked'), process.platform === 'win32' ? 'junction' : 'dir'); }
  catch (error) { if (['EPERM', 'EACCES'].includes(error.code)) { t.skip('OS disallows test symlinks'); return; } throw error; }
  f.item.appProfileRef = { path: 'linked/missing.json', sha256: 'a'.repeat(64) };
  assert.ok(f.evaluate().inputs.some(input => input.state === 'Unsafe'));
  fs.rmSync(outside, { recursive: true, force: true });
  assert.ok(f.evaluate().inputs.some(input => input.state === 'Unsafe'));
});

test('unknown properties never leak and obvious credentials block a whole document', t => {
  const f = fixture(t);
  f.intake.rawInternalNotes = 'INTERNAL_PAYLOAD_SHOULD_NOT_BE_EXPORTED';
  f.save();
  assert.ok(!JSON.stringify(f.evaluate()).includes(f.intake.rawInternalNotes));
  f.intake.password = 'FAKE_TEST_SECRET_NOT_REAL';
  f.save();
  let result = f.evaluate();
  assert.ok(result.diagnostics.some(value => value.code === 'IntakeSensitiveData'));
  assert.ok(!JSON.stringify(result).includes(f.intake.password));
  delete f.intake.password;
  f.intake.sections.purpose.value = 'password=ANOTHER_FAKE_TEST_VALUE';
  f.save();
  result = f.evaluate();
  assert.ok(!JSON.stringify(result).includes('ANOTHER_FAKE_TEST_VALUE'));
});

test('app/profile mismatch and absent project are not considered ready', t => {
  const f = fixture(t);
  f.intake.targetAppIds = ['driver'];
  f.profile.projectPath = 'missing.csproj';
  f.save();
  const result = f.evaluate();
  assert.ok(result.diagnostics.some(value => value.code === 'IntakeTargetProfileMismatch'));
  assert.ok(result.inputs.some(input => input.path === 'missing.csproj' && input.state === 'Missing'));
  assert.equal(result.inputState, 'Incomplete');
});

test('missing environment blocks only its deliverable and does not revoke confirmed input', t => {
  const f = fixture(t);
  f.review.environment = [{ id: 'test-env', state: 'Ready', deliverableIds: ['test'], reason: '격리 시험 준비' }];
  f.confirm();
  const result = f.evaluate();
  assert.equal(result.state, 'Confirmed');
  assert.deepEqual(result.environment.blockedDeliverableIds, ['apk']);
  assert.ok(result.environment.checks.some(check => check.id === 'missing:apk' && check.state === 'Unknown'));
});

test('malformed environment dependencies cannot disappear behind another Ready dependency', t => {
  const f = fixture(t);
  const cases = [
    { name: 'missing reason', check: { id: 'signing', state: 'Missing', deliverableIds: ['apk'], reason: '' }, blocked: ['apk'] },
    { name: 'duplicate identity', check: { id: 'sdk', state: 'Missing', deliverableIds: ['apk'], reason: '서명 준비 안 됨' }, blocked: ['apk'] },
    { name: 'malformed entry', check: null, blocked: ['apk', 'test'] },
    { name: 'unknown target', check: { id: 'signing', state: 'Missing', deliverableIds: ['unknown'], reason: '대상 잘못 지정' }, blocked: ['apk', 'test'] },
    { name: 'partially unknown targets', check: { id: 'signing', state: 'Ready', deliverableIds: ['apk', 'unknown'], reason: '대상 잘못 지정' }, blocked: ['apk', 'test'] },
    { name: 'invalid state', check: { id: 'signing', state: 'MaybeReady', deliverableIds: ['apk'], reason: '준비 상태 불명' }, blocked: ['apk'] }
  ];
  for (const entry of cases) {
    f.review.environment = [
      { id: 'test-runtime', state: 'Ready', deliverableIds: ['test'], reason: '시험 환경 준비' },
      { id: 'sdk', state: 'Ready', deliverableIds: ['apk'], reason: 'SDK 준비' },
      entry.check
    ];
    f.confirm();
    const result = f.evaluate();
    assert.equal(result.inputState, 'Ready', entry.name);
    assert.equal(result.state, 'Confirmed', entry.name);
    assert.equal(result.environment.state, 'Blocked', entry.name);
    assert.deepEqual(result.environment.blockedDeliverableIds, entry.blocked, entry.name);
    assert.ok(result.diagnostics.some(value => value.code === 'IntakeEnvironmentInvalid'), entry.name);
    if (entry.check?.state === 'Missing') assert.ok(result.environment.checks.some(value => value.state === 'Missing'), entry.name);
  }
});

test('duplicate environment identity blocks both formerly ready and newly implicated deliverables', t => {
  const f = fixture(t);
  f.review.environment = [
    { id: 'runtime', state: 'Ready', deliverableIds: ['test'], reason: '시험 준비' },
    { id: 'sdk', state: 'Ready', deliverableIds: ['apk'], reason: 'SDK 준비' },
    { id: 'runtime', state: 'Missing', deliverableIds: ['apk'], reason: '서명 준비 안 됨' }
  ];
  f.confirm();
  assert.deepEqual(f.evaluate().environment.blockedDeliverableIds, ['apk', 'test']);
});

test('approval declarations must bind this work item and current plan/work-order hashes', t => {
  const f = fixture(t);
  const order = { workItemId: 'OTHER', planningStatus: 'Approved', handoffStatus: 'ReadyToDispatch', localAcceptance: 'AcceptedInCurrentThread', designDocumentRef: 'plan.md', designDocumentSha256: f.ref('docs/plan.md').sha256 };
  f.write('docs/order.work-order.json', order);
  f.review.executionApproval = { state: 'Approved', workOrderRefs: [f.ref('docs/order.work-order.json')] };
  f.confirm();
  assert.equal(f.evaluate().approval.state, 'Invalid');
  order.workItemId = f.item.id;
  f.write('docs/order.work-order.json', order);
  f.review.executionApproval.workOrderRefs = [f.ref('docs/order.work-order.json')];
  f.confirm();
  let result = f.evaluate();
  assert.equal(result.approval.state, 'Declared');
  assert.equal(result.approval.grantsExecution, false);
  f.write('docs/plan.md', 'changed');
  result = f.evaluate();
  assert.equal(result.approval.state, 'Invalid');
  assert.equal(result.state, 'Changed');
});

test('collector includes profile, task, review, project and nested sources once in stable order', t => {
  const f = fixture(t);
  f.confirm();
  const inputs = collectIntakeInputs(f.root, f.item);
  assert.deepEqual(inputs.map(input => input.path), ['App/App.csproj', 'docs/plan.md', 'inputs/app.profile.json', 'inputs/task.intake.json', 'inputs/task.review.json']);
  assert.ok(inputs.every(input => input.state === 'Present'));
  assert.deepEqual(collectIntakeInputs(f.root, f.item), inputs);
});

test('human confirmation cannot be manufactured by AI or bind its own review file', t => {
  const f = fixture(t);
  f.confirm();
  f.review.confirmation.confirmedBy = 'AI';
  f.review.reviewedInputs.push(f.item.intakeReviewRef);
  f.save();
  const result = f.evaluate();
  assert.notEqual(result.reviewState, 'Confirmed');
  for (const code of ['IntakeHumanReviewRequired', 'IntakeReviewSelfReference']) assert.ok(result.diagnostics.some(value => value.code === code), code);
});

test('malformed review refs and timestamp are diagnostics rather than thrown errors', t => {
  const f = fixture(t);
  f.confirm();
  f.review.reviewedInputs = [null, false, 'invalid'];
  f.review.confirmation.confirmedAt = '1';
  f.save();
  const result = f.evaluate();
  assert.equal(result.state, 'NeedsInformation');
  assert.ok(result.diagnostics.some(value => value.code === 'IntakeHumanReviewRequired'));
  assert.ok(result.inputs.some(value => value.state === 'Invalid'));
});
