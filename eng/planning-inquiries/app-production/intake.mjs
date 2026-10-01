import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const PROFILE_FIELDS = ['navigation', 'presentation', 'authentication', 'server', 'testEnvironment'];
const SECTIONS = ['purpose', 'information', 'workflow', 'uiStates', 'recovery', 'deliverables'];
const FACT_STATES = ['ExistingConfirmed', 'Proposed', 'Unresolved', 'NotApplicable'];
const KINDS = ['AutomatedTest', 'StaticMockup', 'SharedUiPreview', 'ApiConnectedUi', 'AndroidUi', 'AndroidPackage', 'DeviceInstall'];
const REF_KEYS = ['appProfileRef', 'intakeRef', 'intakeReviewRef'];
const owns = (value, key) => Object.prototype.hasOwnProperty.call(value, key);
const hash = value => createHash('sha256').update(value).digest('hex').toUpperCase();
const object = value => value && typeof value === 'object' && !Array.isArray(value);
const list = value => Array.isArray(value) ? value : [];
const plain = (value, limit = 1600) => typeof value === 'string' ? (secretText.test(value) ? '[redacted]' : value.replace(/<[^>]*>/g, '').replace(/[\u0000-\u001f]/g, ' ').trim().slice(0, limit)) : '';
const text = value => typeof value === 'string' && value.trim().length > 0;
const identity = value => typeof value === 'string' && /^[A-Za-z0-9][A-Za-z0-9_.:-]{0,119}$/.test(value);
const sha = value => typeof value === 'string' && /^[a-f\d]{64}$/i.test(value);
const compare = (a, b) => a < b ? -1 : a > b ? 1 : 0;
const secretKey = /^(?:password|passwd|secret|apiKey|accessToken|refreshToken|authorization|connectionString|privateKey|keystorePassword)$/i;
const secretText = /(?:\b(?:password|passwd|api[_-]?key|access[_-]?token|refresh[_-]?token|authorization|connectionstring|client[_-]?secret)\s*[:=]\s*\S+|\bBearer\s+\S+|https?:\/\/[^\s/@]+:[^\s/@]+@|-----BEGIN [A-Z ]*PRIVATE KEY-----)/i;

function sensitive(value) {
  if (typeof value === 'string') return secretText.test(value);
  if (Array.isArray(value)) return value.some(sensitive);
  return object(value) && Object.entries(value).some(([key, item]) => secretKey.test(key) || sensitive(item));
}

/** Inspect only local, regular files; missing/dangling paths cannot escape via an ancestor link. */
function localPath(root, ref) {
  if (!text(ref) || secretText.test(ref) || /[\u0000-\u001f#]/.test(ref) || /^(?:[a-z][a-z\d+.-]*:|[/\\])/i.test(ref)) throw new Error('Unsafe');
  const base = fs.realpathSync(root);
  const file = path.resolve(base, ref.replaceAll('\\', '/'));
  const inside = candidate => {
    const relative = path.relative(base, candidate);
    return relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative);
  };
  if (!inside(file)) throw new Error('Unsafe');
  let ancestor = file;
  while (!fs.existsSync(ancestor)) {
    try { if (fs.lstatSync(ancestor).isSymbolicLink()) throw new Error('Unsafe'); }
    catch (error) { if (!['ENOENT', 'ENOTDIR'].includes(error.code)) throw error; }
    const parent = path.dirname(ancestor);
    if (parent === ancestor) throw new Error('Unsafe');
    ancestor = parent;
  }
  if (!inside(fs.realpathSync(ancestor))) throw new Error('Unsafe');
  return file;
}

function inspect(root, reference, implicitHash = false) {
  try {
    if (!object(reference)) return { path: '[invalid reference]', sha256: null, state: 'Invalid' };
    const absolute = localPath(root, reference.path);
    const normalized = path.relative(fs.realpathSync(root), absolute).replaceAll('\\', '/');
    const current = fs.existsSync(absolute) && fs.statSync(absolute).isFile() ? hash(fs.readFileSync(absolute)) : null;
    let state = current ? 'Present' : 'Missing';
    if (!implicitHash && !sha(reference.sha256)) state = 'Invalid';
    else if (current && reference.sha256 && reference.sha256.toUpperCase() !== current) state = 'HashMismatch';
    if (reference.anchor !== undefined && (!text(reference.anchor) || /[\u0000-\u001f#]/.test(reference.anchor))) state = 'Invalid';
    return { path: normalized, sha256: current, state,
      ...(sha(reference.sha256) ? { expectedSha256: reference.sha256.toUpperCase() } : {}),
      ...(text(reference.revision) ? { revision: plain(reference.revision, 160) } : {}),
      ...(text(reference.anchor) && !/[\u0000-\u001f#]/.test(reference.anchor) ? { anchor: plain(reference.anchor, 200) } : {}) };
  } catch { return { path: '[rejected path]', sha256: null, state: 'Unsafe' }; }
}

function read(root, reference) {
  const ref = inspect(root, reference);
  if (!ref.sha256 || ['Unsafe', 'Invalid'].includes(ref.state)) return { ref, value: null };
  try {
    const body = fs.readFileSync(localPath(root, ref.path), 'utf8').replace(/^\uFEFF/, '');
    const value = JSON.parse(body);
    return { ref, value: object(value) ? value : null };
  } catch { return { ref: { ...ref, state: 'Invalid' }, value: null }; }
}

function scan(root, item) {
  const documents = Object.fromEntries(REF_KEYS.filter(key => owns(item, key)).map(key => [key, read(root, item[key])]));
  const refs = Object.values(documents).map(doc => doc.ref);
  const add = reference => refs.push(inspect(root, reference));
  const profile = documents.appProfileRef?.value;
  const intake = documents.intakeRef?.value;
  const review = documents.intakeReviewRef?.value;
  if (profile) {
    refs.push(inspect(root, { path: profile.projectPath }, true));
    for (const key of PROFILE_FIELDS) for (const source of list(profile.fields?.[key]?.sources)) add(source);
  }
  if (intake) for (const key of SECTIONS) for (const source of list(intake.sections?.[key]?.sources)) add(source);
  if (review) {
    for (const source of list(review.reviewedInputs)) add(source);
    for (const source of list(review.executionApproval?.workOrderRefs)) {
      add(source);
      const workOrder = read(root, source);
      if (workOrder.value?.designDocumentRef) {
        const designRef = String(workOrder.value.designDocumentRef);
        const relative = designRef.startsWith('docs/') ? designRef : path.posix.join(path.posix.dirname(source.path?.replaceAll('\\', '/') || ''), designRef);
        add({ path: relative, sha256: workOrder.value.designDocumentSha256 });
      }
    }
  }
  const priority = { Present: 0, HashMismatch: 1, Missing: 2, Invalid: 3, Unsafe: 4 };
  const merged = new Map();
  for (const ref of refs) {
    const existing = merged.get(ref.path);
    if (!existing || priority[ref.state] > priority[existing.state]) merged.set(ref.path, ref);
  }
  return { documents, inputs: [...merged.values()].sort((a, b) => compare(a.path, b.path)) };
}

/** Exact reviewed inputs and their nested source files participate in evidence freshness. */
export function collectIntakeInputs(root, item) {
  if (!REF_KEYS.some(key => owns(item, key))) return [];
  return scan(root, item).inputs;
}

/** Read-only readiness projection. It never grants execution, publishes data, or promotes evidence. */
export function evaluateIntake(root, item) {
  const summary = { state: 'NotReviewed', inputState: 'NotReviewed', reviewState: 'Pending',
    approval: { state: 'NotGranted', grantsExecution: false, workOrderRefs: [] },
    environment: { state: 'Unknown', checks: [], blockedDeliverableIds: [] },
    profile: null, targetAppIds: [], impactedRoles: [], sections: {}, questions: [], conflicts: [], deliverables: [], blockers: [], inputs: [], diagnostics: [] };
  if (!REF_KEYS.some(key => owns(item, key))) return summary;
  const { documents, inputs } = scan(root, item);
  summary.inputs = inputs;
  const issue = (code, ref, message, blocking = true) => {
    summary.diagnostics.push({ code, severity: blocking ? 'Warning' : 'Information', ref, message });
    if (blocking) summary.blockers.push({ code, ref, message });
  };
  for (const key of REF_KEYS) if (!documents[key]) issue('IntakeReferenceMissing', key, '제작 입력의 세 참조를 모두 연결해야 합니다.');
  for (const input of inputs) if (input.state !== 'Present') issue(`Intake${input.state}`, input.path, '입력 또는 연결한 출처의 경로·파일·hash를 다시 확인해야 합니다.');
  const load = (key, schema) => {
    const value = documents[key]?.value;
    if (!value) return null;
    if (sensitive(value)) { issue('IntakeSensitiveData', documents[key].ref.path, '비밀값으로 의심되는 필드나 문자열이 있어 해당 입력 내용은 공개하지 않습니다.'); return null; }
    if (value.schemaVersion !== schema) { issue('IntakeSchemaInvalid', documents[key].ref.path, '지원하는 입력 판본이 아닙니다.'); return null; }
    return value;
  };
  const profile = load('appProfileRef', 'planning-app-profile.v1');
  const intake = load('intakeRef', 'planning-app-intake.v1');
  const review = load('intakeReviewRef', 'planning-app-intake-review.v1');
  const fact = (value, location) => {
    if (!object(value) || !FACT_STATES.includes(value.status) || typeof value.critical !== 'boolean' || !Array.isArray(value.sources)) {
      issue('IntakeFactMissing', location, '입력의 상태·중요 여부·출처 목록을 채워야 합니다.');
      return { status: 'Unresolved', value: '', critical: true, reason: '', sources: [] };
    }
    const result = { status: value.status, value: plain(value.value), critical: value.critical, reason: plain(value.reason), sources: value.sources.map(ref => inspect(root, ref)) };
    if (value.status === 'NotApplicable' && !text(value.reason)) issue('IntakeExclusionReasonMissing', location, '해당 없음에는 제외 사유가 필요합니다.');
    if (value.status !== 'NotApplicable' && value.status !== 'Unresolved' && !text(value.value)) issue('IntakeFactValueMissing', location, '입력값 또는 요약이 필요합니다.');
    if (value.status === 'ExistingConfirmed' && !value.sources.length) issue('IntakeSourceMissing', location, '기존 확인 항목에는 근거 파일과 hash가 필요합니다.');
    if (value.critical && ['Proposed', 'Unresolved'].includes(value.status)) issue('IntakeCriticalUnresolved', location, '중요 제안·미정 입력은 확정 또는 사유 있는 제외가 필요합니다.');
    return result;
  };
  if (profile) {
    if (!identity(profile.appId) || !text(profile.projectPath) || !Array.isArray(profile.platforms) || !profile.platforms.length || profile.platforms.some(value => !identity(value))) {
      issue('IntakeProfileInvalid', 'profile', '대상 앱·프로젝트·플랫폼을 확인해야 합니다.');
    }
    const projectRef = inspect(root, { path: profile.projectPath }, true);
    summary.profile = { appId: identity(profile.appId) ? profile.appId : '', projectPath: projectRef.path,
      platforms: list(profile.platforms).filter(identity), fields: Object.fromEntries(PROFILE_FIELDS.map(key => [key, fact(profile.fields?.[key], `profile.${key}`)])) };
  }
  if (intake) {
    if (intake.workItemId !== item.id) issue('IntakeWorkItemMismatch', 'intake', '작업 입력의 고유 식별자가 대장과 다릅니다.');
    summary.targetAppIds = list(intake.targetAppIds).filter(identity);
    summary.impactedRoles = list(intake.impactedRoles).filter(role => ['restaurant', 'orderer', 'driver', 'admin'].includes(role));
    if (!Array.isArray(intake.targetAppIds) || !summary.targetAppIds.length || summary.targetAppIds.length !== intake.targetAppIds.length || new Set(summary.targetAppIds).size !== summary.targetAppIds.length) issue('IntakeTargetMissing', 'intake.targetAppIds', '실제 제작 대상 앱을 중복 없이 명시해야 합니다.');
    if (!Array.isArray(intake.impactedRoles) || summary.impactedRoles.length !== intake.impactedRoles.length) issue('IntakeRolesInvalid', 'intake.impactedRoles', '영향받는 역할을 제작 대상 앱과 구별해 기록해야 합니다.');
    if (profile && (summary.targetAppIds.length !== 1 || summary.targetAppIds[0] !== profile.appId)) issue('IntakeTargetProfileMismatch', 'intake.targetAppIds', '이 입력표의 대상 앱은 연결한 앱 공통 프로필 하나와 일치해야 합니다.');
    summary.sections = Object.fromEntries(SECTIONS.map(key => [key, fact(intake.sections?.[key], `intake.${key}`)]));
    for (const key of ['questions', 'conflicts', 'deliverables']) if (!Array.isArray(intake[key])) issue('IntakeListMissing', `intake.${key}`, '미정·충돌·결과물 목록은 비어 있더라도 명시해야 합니다.');
    for (const question of list(intake.questions)) {
      if (!identity(question?.id) || !text(question?.text) || typeof question?.critical !== 'boolean') { issue('IntakeQuestionInvalid', 'intake.questions', '질문의 고유 식별자·내용·중요 여부가 필요합니다.'); continue; }
      summary.questions.push({ id: question.id, text: plain(question.text), critical: question.critical });
      if (question.critical) issue('IntakeCriticalQuestion', question.id, '중요 질문에 대한 사람의 결정이 남아 있습니다.');
    }
    for (const conflict of list(intake.conflicts)) {
      if (!identity(conflict?.id) || !text(conflict?.description) || typeof conflict?.blocking !== 'boolean') { issue('IntakeConflictInvalid', 'intake.conflicts', '충돌의 고유 식별자·내용·차단 여부가 필요합니다.'); continue; }
      summary.conflicts.push({ id: conflict.id, description: plain(conflict.description), blocking: conflict.blocking });
      if (conflict.blocking) issue('IntakeBlockingConflict', conflict.id, '입력 사이의 충돌이 아직 해결되지 않았습니다.');
    }
    for (const deliverable of list(intake.deliverables)) {
      if (!identity(deliverable?.id) || !KINDS.includes(deliverable?.kind) || typeof deliverable?.required !== 'boolean' || summary.deliverables.some(entry => entry.id === deliverable.id)) { issue('IntakeDeliverableInvalid', 'intake.deliverables', '결과물의 고유 식별자·증거 종류·필수 여부를 확인해야 합니다.'); continue; }
      summary.deliverables.push({ id: deliverable.id, kind: deliverable.kind, required: deliverable.required });
    }
    if (!summary.deliverables.length) issue('IntakeDeliverableMissing', 'intake.deliverables', '하나 이상의 결과물을 명시해야 합니다.');
  }
  const inputBlockers = summary.blockers.length;
  let changed = inputs.some(input => input.state === 'HashMismatch');
  if (review) {
    if (review.workItemId !== item.id) issue('IntakeReviewWorkItemMismatch', 'review', '검토 기록의 작업 식별자가 다릅니다.');
    const confirmation = review.confirmation;
    if (!object(confirmation) || !['Pending', 'Confirmed'].includes(confirmation.state)) issue('IntakeConfirmationInvalid', 'review.confirmation', '사람 검토 여부를 명시해야 합니다.');
    else if (confirmation.state === 'Confirmed') {
      const snapshots = list(review.reviewedInputs);
      const exactInputs = ['appProfileRef', 'intakeRef'].every(key => documents[key]?.ref.state === 'Present' && snapshots.some(ref => {
        const inspected = inspect(root, ref);
        return inspected.state === 'Present' && inspected.path === documents[key].ref.path && inspected.expectedSha256 === documents[key].ref.sha256;
      }));
      if (confirmation.confirmedBy !== 'Human' || !text(confirmation.confirmedAt) || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$/.test(confirmation.confirmedAt) || !Number.isFinite(Date.parse(confirmation.confirmedAt))) issue('IntakeHumanReviewRequired', 'review.confirmation', '확인 주체와 확인 시각이 있는 사람 검토 기록이 필요합니다.');
      else if (!exactInputs) { changed = true; issue('IntakeReviewedInputsChanged', 'review.reviewedInputs', '사람이 확인한 프로필·작업 입력 hash와 현재 입력이 일치하지 않습니다.'); }
      else summary.reviewState = 'Confirmed';
    }
    if (!Array.isArray(review.reviewedInputs)) issue('IntakeReviewedInputsMissing', 'review.reviewedInputs', '검토 입력 목록을 명시해야 합니다.');
    if (list(review.reviewedInputs).some(ref => inspect(root, ref).path === documents.intakeReviewRef?.ref.path)) issue('IntakeReviewSelfReference', 'review.reviewedInputs', '검토 기록은 자신의 hash를 검토 입력으로 삼을 수 없습니다.');
    const approval = review.executionApproval;
    if (!object(approval) || !['NotGranted', 'Approved'].includes(approval.state) || !Array.isArray(approval.workOrderRefs)) issue('IntakeApprovalInvalid', 'review.executionApproval', '실행 승인은 입력 확인과 별도로 명시해야 합니다.');
    else {
      summary.approval.workOrderRefs = approval.workOrderRefs.map(ref => inspect(root, ref));
      if (approval.state === 'Approved') {
        const valid = approval.workOrderRefs.length > 0 && approval.workOrderRefs.every(ref => {
          const inspected = read(root, ref);
          const order = inspected.value;
          if (inspected.ref.state !== 'Present' || !order || sensitive(order)) return false;
          const bound = order.workItemId === item.id || list(order.allowedWorkItemIds).includes(item.id);
          const design = text(order.designDocumentRef) ? (order.designDocumentRef.startsWith('docs/') ? order.designDocumentRef : path.posix.join(path.posix.dirname(ref.path), order.designDocumentRef)) : '';
          return bound && order.planningStatus === 'Approved' && order.handoffStatus === 'ReadyToDispatch' && order.localAcceptance === 'AcceptedInCurrentThread'
            && inspect(root, { path: design, sha256: order.designDocumentSha256 }).state === 'Present'
            && list(item.planRefs).some(plan => plan.path === design && sha(plan.sha256) && plan.sha256.toUpperCase() === order.designDocumentSha256?.toUpperCase());
        });
        summary.approval.state = valid ? 'Declared' : 'Invalid';
        if (!valid) issue('IntakeApprovalReferenceInvalid', 'review.executionApproval', '승인 선언과 현재 작업·기획 hash·작업지시서 결속을 확인할 수 없습니다.');
      }
    }
    if (!Array.isArray(review.environment)) issue('IntakeEnvironmentMissing', 'review.environment', '결과물별 환경 준비 목록이 없습니다.', false);
    for (const [checkIndex, check] of list(review.environment).entries()) {
      if (!identity(check?.id) || !['Unknown', 'Missing', 'Ready', 'NotApplicable'].includes(check?.state) || !Array.isArray(check?.deliverableIds) || !check.deliverableIds.length || check.deliverableIds.some(id => !summary.deliverables.some(d => d.id === id)) || !text(check?.reason) || summary.environment.checks.some(entry => entry.id === check.id)) {
        issue('IntakeEnvironmentInvalid', 'review.environment', '환경 점검은 결과물과 준비 여부·사유를 명시해야 합니다.', false);
        // Bad formatting must not erase a missing dependency behind another Ready check.
        // Unknown target references conservatively affect every required deliverable.
        const namedTargets = list(check?.deliverableIds).filter(id => summary.deliverables.some(d => d.id === id));
        const unknownTargets = !Array.isArray(check?.deliverableIds) || !check.deliverableIds.length || namedTargets.length !== check.deliverableIds.length;
        const duplicateTargets = summary.environment.checks.filter(entry => entry.id === check?.id).flatMap(entry => entry.deliverableIds);
        const affected = [...new Set([...namedTargets, ...duplicateTargets,
          ...(unknownTargets ? summary.deliverables.filter(d => d.required).map(d => d.id) : [])])].sort(compare);
        summary.environment.checks.push({ id: `invalid:${checkIndex}`, state: check?.state === 'Missing' ? 'Missing' : 'Unknown',
          deliverableIds: affected, reason: '환경 점검 형식이 잘못되어 준비 완료로 판정할 수 없습니다. 연결된 결과물의 준비 여부를 다시 확인해야 합니다.' });
        continue;
      }
      summary.environment.checks.push({ id: check.id, state: check.state, deliverableIds: [...check.deliverableIds], reason: plain(check.reason) });
    }
  }
  for (const deliverable of summary.deliverables.filter(entry => entry.required)) {
    const checks = summary.environment.checks.filter(check => check.deliverableIds.includes(deliverable.id));
    if (!checks.length) summary.environment.checks.push({ id: `missing:${deliverable.id}`, state: 'Unknown', deliverableIds: [deliverable.id], reason: '이 결과물의 환경 준비가 아직 확인되지 않았습니다.' });
  }
  summary.environment.blockedDeliverableIds = [...new Set(summary.environment.checks.filter(check => ['Missing', 'Unknown'].includes(check.state)).flatMap(check => check.deliverableIds))].sort(compare);
  summary.environment.state = summary.environment.blockedDeliverableIds.length ? 'Blocked' : summary.deliverables.length ? 'Ready' : 'Unknown';
  summary.inputState = changed ? 'Changed' : inputBlockers ? 'Incomplete' : 'Ready';
  if (summary.reviewState !== 'Confirmed') issue('IntakeReviewPending', 'review.confirmation', 'AI 입력 초안에 대한 사람 검토가 필요합니다.');
  if (summary.approval.state !== 'Declared') summary.blockers.push({ code: 'IntakeExecutionNotApproved', ref: 'review.executionApproval', message: '업무 개발 승인은 별도입니다. 이 도구는 실행 권한을 부여하지 않습니다.' });
  summary.state = changed ? 'Changed' : summary.inputState === 'Ready' && summary.reviewState === 'Confirmed' && !summary.diagnostics.some(d => d.severity === 'Warning') ? 'Confirmed' : 'NeedsInformation';
  return summary;
}
