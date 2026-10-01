import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { evaluateEvidence } from './evidence.mjs';
import { evaluateIntake } from './intake.mjs';

const INDEX = 'docs/AI/PLANNING.md';
const TREE = 'docs/AI/Planning';
const SOURCES = 'eng/planning-inquiries/sources.json';
const OUTPUTS = ['docs/AI/generated/planning-app-production.json', 'docs/AI/generated/planning-app-production.md', 'docs/AI/generated/planning-app-production.html'];
const TOOLS = ['catalog.mjs', 'evidence.mjs', 'intake.mjs', 'cli.mjs', 'viewer.html'].map(name => `eng/planning-inquiries/app-production/${name}`);
const ROLES = ['orderer', 'restaurant', 'driver', 'admin'];
const compare = (a, b) => a < b ? -1 : a > b ? 1 : 0;
const unique = values => [...new Set(values)].sort(compare);
const slash = value => value.replaceAll('\\', '/');
const digest = value => createHash('sha256').update(value).digest('hex').toUpperCase();
const plain = (value, limit = 600) => String(value ?? '').replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, '')
  .replace(/<!--[\s\S]*?-->/g, '').replace(/<[^>]*>/g, '').replace(/[\u0000-\u001f]/g, ' ')
  .replace(/[`*_]/g, '').trim().slice(0, limit);
const stable = value => JSON.stringify(value, (_, v) => v && !Array.isArray(v) && typeof v === 'object'
  ? Object.fromEntries(Object.keys(v).sort(compare).map(key => [key, v[key]])) : v);

/** Resolve only repository-local files, including existing ancestors of missing files. */
export function safePath(root, ref) {
  if (typeof ref !== 'string' || !ref || /[\u0000-\u001f]/.test(ref) || /^(?:[a-z][a-z\d+.-]*:|[/\\])/i.test(ref)) {
    throw Object.assign(new Error('Repository-relative path required.'), { code: 'UnsafePath' });
  }
  const base = fs.realpathSync(root);
  const absolute = path.resolve(base, slash(ref));
  const inside = candidate => candidate === base || (!path.relative(base, candidate).startsWith(`..${path.sep}`)
    && path.relative(base, candidate) !== '..' && !path.isAbsolute(path.relative(base, candidate)));
  if (!inside(absolute)) throw Object.assign(new Error('Path escapes repository.'), { code: 'UnsafePath' });
  let ancestor = absolute;
  while (!fs.existsSync(ancestor)) {
    // A dangling symlink must not be treated as an ordinary missing path.
    try { if (fs.lstatSync(ancestor).isSymbolicLink()) throw Object.assign(new Error('Dangling symbolic link.'), { code: 'UnsafePath' }); }
    catch (error) { if (error.code !== 'ENOENT' && error.code !== 'ENOTDIR') throw error; }
    const parent = path.dirname(ancestor);
    if (parent === ancestor) throw Object.assign(new Error('Invalid repository path.'), { code: 'UnsafePath' });
    ancestor = parent;
  }
  if (!inside(fs.realpathSync(ancestor))) throw Object.assign(new Error('Symbolic link escapes repository.'), { code: 'UnsafePath' });
  return absolute;
}

export function hashFile(root, ref) {
  const absolute = safePath(root, ref);
  return fs.existsSync(absolute) && fs.statSync(absolute).isFile() ? digest(fs.readFileSync(absolute)) : null;
}

function links(text) {
  const result = [];
  for (const match of text.matchAll(/\[([^\]\r\n]*)\]\((<[^>]+>|[^\s)]+)(?:\s+"[^"]*")?\)/g)) {
    let target = match[2].replace(/^<|>$/g, '');
    if (!target || target.startsWith('#') || /^(?:[a-z][a-z\d+.-]*:|\/\/)/i.test(target)) continue;
    try { target = decodeURIComponent(target); } catch { /* invalid escapes remain literal paths */ }
    result.push({ target, title: plain(match[1]) });
  }
  return result;
}

function localLink(from, target) {
  const [file, ...fragment] = slash(target).split('#');
  if (/^(?:[a-z][a-z\d+.-]*:|\/)/i.test(file)) return { path: file, anchor: fragment.join('#') || undefined };
  return { path: path.posix.normalize(path.posix.join(path.posix.dirname(from), file)), anchor: fragment.join('#') || undefined };
}

function kindOf(ref, body) {
  if (/(?:^|\/)(?:generated|artifacts)(?:\/|$)/i.test(ref)) return 'GeneratedReference';
  if (/\.result(?:\.[^/]*)?\.md$|(?:^|\/)[^/]*(?:audit|test-results?|validation-report)[^/]*\.md$/i.test(ref)) return 'ResultReference';
  if (/^(?:eng|scripts|tools)\//i.test(ref)) return 'ToolReference';
  if (/호환 (?:안내|기획 ID)|compatibility (?:pointer|reference)/i.test(body.slice(0, 1500))) return 'CompatibilityReference';
  return ref.startsWith(`${TREE}/`) ? (/\/PLAN-[A-Z0-9-]+\//.test(ref) || ownIds(body).length ? 'PlanDocument' : 'Reference') : 'LegacyReference';
}

function ownIds(body) {
  const first = body.split(/\r?\n/).slice(0, 35).join('\n');
  const explicit = [...first.matchAll(/(?:기획 ID|호환 기획 ID|Plan ID)\s*[:：]\s*`?(PLAN-[A-Z0-9-]+)/gi)].map(m => m[1]);
  const header = first.match(/^\[기획[^\r\n]*\b(PLAN-[A-Z0-9-]+)/m);
  return unique([...explicit, ...(header ? [header[1]] : [])]);
}

/** Build observations; declared plan status and legacy O/X are never execution approval. */
export function buildIndex(root, bindingsPath = 'eng/planning-inquiries/app-production/role-app-links.json', options = {}) {
  const diagnostics = [];
  const inputMap = new Map();
  const docs = new Map();
  const plans = new Map();
  const aliases = new Map();
  const registered = new Set();
  const documentRegistrations = new Map();
  const addDiagnostic = (code, ref, message, severity = 'Warning') => diagnostics.push({ code, severity, ref, message });
  const inspect = (ref, required = true) => {
    try {
      const absolute = safePath(root, ref);
      const normalized = slash(path.relative(fs.realpathSync(root), absolute));
      if (OUTPUTS.includes(normalized)) {
        addDiagnostic('SelfGeneratedInputExcluded', normalized, '이 도구의 생성물은 자기 자신의 검증 입력으로 사용할 수 없습니다.', 'Error');
        return null;
      }
      const sha256 = hashFile(root, normalized);
      inputMap.set(normalized, { path: normalized, sha256 });
      if (!sha256 && required) addDiagnostic('MissingReference', normalized, '명시적으로 연결한 파일을 찾지 못했습니다.');
      return { path: normalized, sha256, state: sha256 ? 'Present' : 'Missing' };
    } catch {
      addDiagnostic('UnsafePath', '[rejected path]', '저장소 경계를 벗어나거나 안전하게 해석할 수 없는 경로를 제외했습니다.', 'Error');
      return null;
    }
  };
  const read = ref => {
    const record = inspect(ref);
    return record?.sha256 ? { ...record, text: fs.readFileSync(safePath(root, record.path), 'utf8').replace(/^\uFEFF/, '') } : null;
  };
  const json = ref => {
    const record = read(ref);
    if (!record) return null;
    try { return JSON.parse(record.text); }
    catch { addDiagnostic('InvalidJson', record.path, 'JSON 형식을 읽을 수 없습니다.', 'Error'); return null; }
  };
  const resolveId = id => aliases.get(id) ?? id;
  const ensurePlan = (id, defaults = {}) => {
    id = resolveId(id);
    if (!plans.has(id)) plans.set(id, { id, title: defaults.title || id, category: defaults.category || '미분류',
      sourceRefs: [], canonicalRefs: [], declaredState: defaults.declaredState || 'Unknown', codePresence: 'Unknown',
      roles: [], workItemIds: [], compatibilityIds: [] });
    return plans.get(id);
  };
  const addPlanRef = (plan, file, role, canonical = false, anchor) => {
    const ref = { path: file.path, sha256: file.sha256, role, ...(anchor ? { anchor } : {}) };
    if (!plan.sourceRefs.some(x => x.path === ref.path && x.role === role && x.anchor === anchor)) plan.sourceRefs.push(ref);
    if (canonical && !plan.canonicalRefs.some(x => x.path === ref.path && x.anchor === anchor)) plan.canonicalRefs.push(ref);
  };
  const addDocument = (ref, linkedIds = [], role = 'PlanningDocument', canonical = false, anchor) => {
    // Our own generated outputs are references, never inputs to their own hash.
    if (OUTPUTS.includes(ref)) {
      if (!docs.has(ref)) docs.set(ref, { path: ref, sha256: null, title: path.posix.basename(ref), planIds: [], kind: 'GeneratedReference' });
      return docs.get(ref);
    }
    const data = read(ref);
    if (!data) {
      const record = inspect(ref, false);
      if (record) for (const id of linkedIds) addPlanRef(ensurePlan(id), record, role, canonical, anchor);
      return null;
    }
    let document = docs.get(data.path);
    if (!document) {
      const heading = data.text.match(/^#\s+(.+)$/m)?.[1];
      const directoryId = data.path.startsWith(`${TREE}/`) ? data.path.split('/').find(part => /^PLAN-[A-Z0-9-]+$/.test(part)) : null;
      const explicitIds = ownIds(data.text);
      const ownerIds = explicitIds.length ? explicitIds : directoryId ? [directoryId] : linkedIds;
      document = { path: data.path, sha256: data.sha256, title: plain(heading || path.posix.basename(data.path)),
        planIds: [], kind: kindOf(data.path, data.text) };
      docs.set(data.path, document);
      const semantic = ['PlanDocument', 'CompatibilityReference', 'LegacyReference'].includes(document.kind);
      if (semantic) {
        linkedIds = unique([...linkedIds, ...ownerIds]);
        document.planIds = unique(ownerIds.map(resolveId));
      } else {
        linkedIds = unique([...linkedIds, ...ownerIds.filter(id => plans.has(resolveId(id)))]);
        document.planIds = unique(ownerIds.filter(id => plans.has(resolveId(id))).map(resolveId));
      }
      // Only explicitly labelled canonical pointers may supplement the registry.
      for (const line of data.text.split(/\r?\n/).filter(line => /현행 정본|현재 정본|canonical\s*(?:document|ref)?\s*:/i.test(line))) {
        for (const link of links(line).slice(0, 1)) {
          const target = localLink(data.path, link.target);
          if (!/\.md$/i.test(target.path) || OUTPUTS.includes(target.path)) continue;
          const record = inspect(target.path);
          if (record) for (const id of linkedIds) addPlanRef(ensurePlan(id), record, 'ExplicitCanonicalReference', true, target.anchor);
        }
      }
    }
    // Referencing another plan's document does not transfer that document's ownership.
    for (const id of linkedIds) {
      const plan = ensurePlan(id, { title: document.title, category: data.path.startsWith(`${TREE}/`) ? data.path.split('/')[3] : '기존 기획' });
      addPlanRef(plan, data, role === 'PlanningDocument' ? document.kind : role, canonical, anchor);
      if (plan.declaredState === 'Unknown') {
        const state = data.text.match(/^-\s*상태\s*[:：]\s*(.+)$/m)?.[1];
        if (state) plan.declaredState = plain(state, 4000);
      }
    }
    return document;
  };

  const index = read(INDEX);
  const rows = [];
  let section = '목차 등록';
  for (const line of index?.text.split(/\r?\n/) ?? []) {
    if (/^##\s/.test(line)) section = plain(line.replace(/^##\s+/, ''));
    if (!/^\s*\|/.test(line)) continue;
    const cells = line.trim().split(/(?<!\\)\|/).slice(1, -1);
    const id = cells[0]?.match(/`(PLAN-[A-Z0-9-]+)`/)?.[1];
    if (!id) continue;
    const compatibility = [...cells[0].matchAll(/<!--\s*compatibility-id\s*:\s*(PLAN-[A-Z0-9-]+)\s*-->/g)].map(m => m[1]);
    rows.push({ id, compatibility, links: links(cells[1] ?? ''), state: plain(cells[2], 4000), category: section });
    for (const oldId of compatibility) {
      if (aliases.has(oldId) && aliases.get(oldId) !== id) addDiagnostic('ConflictingCompatibilityId', INDEX, `${oldId}의 등록 대상이 둘 이상입니다.`, 'Error');
      else aliases.set(oldId, id);
    }
  }
  for (const row of rows) {
    const id = resolveId(row.id);
    const targets = row.links.map(link => localLink(INDEX, link.target));
    if (registered.has(id)) addDiagnostic('DuplicateRegisteredId', INDEX, `${id}가 목차에 중복 등록되어 있습니다.`, 'Error');
    registered.add(id);
    const plan = ensurePlan(id, { title: row.links[0]?.title, category: row.category, declaredState: row.state });
    plan.compatibilityIds = unique([...plan.compatibilityIds, ...row.compatibility]);
    if (!targets.length) addDiagnostic('MissingRegisteredDocument', INDEX, `${id}의 로컬 원문 링크가 없습니다.`, 'Error');
    for (const [position, target] of targets.entries()) {
      const other = position === 0 ? documentRegistrations.get(target.path) : null;
      if (other && other !== id) addDiagnostic('ConflictingRegisteredDocument', target.path, `${other}와 ${id}가 같은 원문에 등록되어 있습니다.`, 'Error');
      else if (position === 0) documentRegistrations.set(target.path, id);
      addDocument(target.path, [id], 'RegisteredReference', true, target.anchor);
    }
  }
  // Include every local Markdown link in the inventory, even when it is only a reference.
  for (const link of links(index?.text ?? '')) {
    const target = localLink(INDEX, link.target);
    if (/\.md$/i.test(target.path)) addDocument(target.path, [], 'IndexReference', false, target.anchor);
  }
  const visitedDirectories = new Set();
  const walk = ref => {
    let absolute;
    try { absolute = safePath(root, ref); } catch { inspect(ref); return; }
    if (!fs.existsSync(absolute)) return;
    const real = fs.realpathSync(absolute);
    if (visitedDirectories.has(real)) return;
    visitedDirectories.add(real);
    for (const entry of fs.readdirSync(absolute, { withFileTypes: true }).sort((a, b) => compare(a.name, b.name))) {
      const child = `${ref}/${entry.name}`;
      if (entry.isDirectory()) walk(child);
      else if (entry.isSymbolicLink()) {
        try {
          const target = safePath(root, child);
          if (fs.existsSync(target) && fs.statSync(target).isDirectory()) walk(child);
          else if (/\.md$/i.test(child)) addDocument(child);
        } catch { inspect(child); }
      } else if (/\.md$/i.test(child)) addDocument(child);
    }
  };
  walk(TREE);
  const sourceConfig = json(SOURCES);
  const sourcePaths = new Set();
  const gather = value => {
    if (Array.isArray(value)) value.forEach(gather);
    else if (value && typeof value === 'object') for (const [key, child] of Object.entries(value)) {
      if (typeof child === 'string' && /(?:path|ref)$/i.test(key) && /\.md(?:#.*)?$/i.test(child)) sourcePaths.add(child);
      else gather(child);
    }
  };
  gather(sourceConfig);
  for (const ref of [...sourcePaths].sort(compare)) {
    const [file, anchor] = ref.split('#');
    addDocument(file, [], 'LegacySourceReference', false, anchor);
  }

  const bindings = json(bindingsPath);
  if (!bindings) addDiagnostic('BindingsUnavailable', bindingsPath, '필수 업무 관계 자료를 읽을 수 없습니다.', 'Error');
  if (bindings && bindings.schemaVersion !== 'planning-app-production-links.v1') addDiagnostic('UnsupportedBindingsSchema', bindingsPath, '지원하지 않는 관계 자료 형식입니다.', 'Error');
  const rawItems = bindings?.schemaVersion === 'planning-app-production-links.v1' && Array.isArray(bindings.items) ? bindings.items : [];
  const reference = raw => {
    const value = typeof raw === 'string' ? { path: raw } : raw;
    if (!value || typeof value.path !== 'string') { addDiagnostic('InvalidReference', bindingsPath, '경로가 없는 파일 참조를 제외했습니다.', 'Error'); return null; }
    const actual = inspect(value.path);
    if (!actual) return null;
    if (actual.state === 'Missing') addDiagnostic('BindingReferenceMissing', actual.path, '업무 관계 자료에 명시한 필수 참조 파일이 없습니다.', 'Error');
    if (value.sha256 && actual.sha256 && String(value.sha256).toUpperCase() !== actual.sha256) addDiagnostic('ReferenceHashMismatch', actual.path, '관계 자료의 고정 hash와 현재 파일 hash가 다릅니다.', 'Error');
    return { ...actual, ...(value.sha256 ? { expectedSha256: String(value.sha256).toUpperCase() } : {}) };
  };
  const references = raw => (Array.isArray(raw) ? raw : []).map(reference).filter(Boolean).sort((a, b) => compare(a.path, b.path));
  const presence = refs => !refs.length ? 'Unknown' : refs.some(ref => ref.state === 'Present') ? 'Present' : 'Missing';
  const workItems = [];
  const ids = new Set();
  const evidenceByItem = new Map();
  for (const evidencePath of options.evidencePaths ?? []) {
    const value = typeof evidencePath === 'string' ? { path: evidencePath } : evidencePath;
    if (!value?.path) continue;
    const manifest = json(value.path);
    const itemId = value.workItemId ?? manifest?.workItemId;
    if (!itemId) { addDiagnostic('EvidenceWorkItemMissing', value.path, '실행 증거에 연결 업무 ID가 없습니다.', 'Error'); continue; }
    if (!evidenceByItem.has(itemId)) evidenceByItem.set(itemId, []);
    evidenceByItem.get(itemId).push({ path: value.path, kind: value.kind ?? manifest?.kind, format: 'EvidenceManifest' });
  }
  for (const raw of rawItems) {
    if (!raw || typeof raw.id !== 'string' || !/^[A-Za-z0-9][A-Za-z0-9._:-]*$/.test(raw.id)) { addDiagnostic('InvalidWorkItemId', bindingsPath, '업무 ID가 없거나 올바르지 않습니다.', 'Error'); continue; }
    if (ids.has(raw.id)) { addDiagnostic('DuplicateWorkItemId', bindingsPath, `${raw.id} 업무가 중복 등록되어 있습니다.`, 'Error'); continue; }
    ids.add(raw.id);
    const item = { id: raw.id, title: plain(raw.title || raw.id), planRefs: [], roles: [], codeRefs: references(raw.codeRefs), testRefs: references(raw.testRefs),
      roleLinks: [], workOrderRefs: references(raw.workOrderRefs), resultRefs: references(raw.resultRefs), evidence: [], remaining: [], codePresence: 'Unknown', testCodePresence: 'Unknown' };
    item.roles = ROLES.filter(role => raw.roles?.includes(role));
    if ((raw.roles ?? []).some(role => !ROLES.includes(role))) addDiagnostic('UnknownRole', bindingsPath, `${raw.id}에 지원하지 않는 역할이 있습니다.`, 'Error');
    item.remaining = (Array.isArray(raw.remaining) ? raw.remaining : []).map(value => plain(value)).filter(Boolean);
    const { inputs: intakeInputs = [], diagnostics: intakeDiagnostics = [], ...intake } = evaluateIntake(root, raw);
    item.intake = { ...intake, sourceRefs: intakeInputs.filter(input => ['appProfileRef', 'intakeRef', 'intakeReviewRef'].some(key => raw[key]?.path === input.path)) };
    for (const diagnostic of intakeDiagnostics) diagnostics.push(diagnostic);
    for (const input of intakeInputs) if (input.path && input.path !== '[rejected path]') inspect(input.path, false);
    for (const planRef of raw.planRefs ?? []) {
      const value = typeof planRef === 'string' ? { path: planRef } : planRef;
      const ref = reference(value);
      if (!ref) continue;
      item.planRefs.push({ ...ref, revision: plain(value.revision || 'Unspecified') });
      const doc = addDocument(ref.path, [], 'BoundWorkItemPlan');
      if (!doc?.planIds.length) addDiagnostic('UnresolvedPlanOwnership', ref.path, `${raw.id}의 기획 원문에서 소유 PLAN ID를 판정하지 못했습니다.`);
      for (const id of doc?.planIds ?? []) {
        const plan = ensurePlan(id);
        plan.workItemIds.push(item.id);
        plan.roles.push(...item.roles);
      }
    }
    for (const link of raw.roleLinks ?? []) {
      if (!link || !ROLES.includes(link.role)) { addDiagnostic('UnknownRole', bindingsPath, `${raw.id} 화면 참조의 역할이 올바르지 않습니다.`, 'Error'); continue; }
      item.roleLinks.push({ role: link.role, codeRefs: references(link.codeRefs ?? (link.path ? [link] : [])), note: plain(link.note || link.label) });
    }
    // A test or document in codeRefs does not establish product code presence.
    const productRefs = item.codeRefs.filter(ref => {
      const valid = /\.(?:cs|razor|xaml|js|jsx|ts|tsx|vue|svelte|cpp|c|h|py|swift|kt)$/i.test(ref.path)
        && !/(?:^|\/)(?:docs|eng|scripts|tests?|[^/]*\.Tests?|generated|artifacts)(?:\/|$)|(?:\.test|\.spec|Tests)\.[^/]+$/i.test(ref.path);
      if (!valid) addDiagnostic('NonProductCodeReference', ref.path, '빌드 설정·문서·도구·시험 참조는 제품 구현 소스의 존재 판정에서 제외했습니다.');
      return valid;
    });
    item.codePresence = productRefs.length ? presence(productRefs) : 'Unknown';
    const actualTestRefs = item.testRefs.filter(ref => /\.(?:cs|js|mjs|cjs|ts|tsx|py|swift|kt|ps1)$/i.test(ref.path));
    if (actualTestRefs.length !== item.testRefs.length) addDiagnostic('NonTestCodeReference', bindingsPath, `${raw.id} 시험 참조에 소스 파일이 아닌 항목이 있습니다.`);
    item.testCodePresence = presence(actualTestRefs);
    for (const orderRef of item.workOrderRefs) {
      if (!orderRef.sha256) continue;
      const order = json(orderRef.path);
      if (!order?.designDocumentRef || !order.designDocumentSha256) { addDiagnostic('WorkOrderBaselineMissing', orderRef.path, '작업지시서의 기획 원문 또는 고정 hash가 없습니다.'); continue; }
      const target = /^(?:docs|eng)\//.test(order.designDocumentRef) ? { path: order.designDocumentRef } : localLink(orderRef.path, order.designDocumentRef);
      const actual = inspect(target.path);
      if (actual?.sha256 && actual.sha256 !== String(order.designDocumentSha256).toUpperCase()) addDiagnostic('WorkOrderPlanHashMismatch', orderRef.path, '작업지시서가 고정한 기획 hash와 원문이 다릅니다.', 'Error');
      if (actual && !item.planRefs.some(ref => ref.path === actual.path)) addDiagnostic('WorkOrderPlanNotBound', orderRef.path, '작업지시서 기획이 해당 업무의 기획 참조에 연결되어 있지 않습니다.');
    }
    const evidenceRefs = [...(Array.isArray(raw.evidenceRefs) ? raw.evidenceRefs : []), ...(evidenceByItem.get(item.id) ?? [])];
    for (const evidenceRef of evidenceRefs) {
      if (!inspect(evidenceRef?.path, false)) continue;
      const result = evaluateEvidence(root, evidenceRef, { ...raw, bindingPath: bindingsPath, currentObservation: item,
        toolPaths: ['eng/planning-inquiries/app-production/catalog.mjs', 'eng/planning-inquiries/app-production/evidence.mjs', 'eng/planning-inquiries/app-production/cli.mjs'] });
      const { diagnostics: evidenceDiagnostics = [], inputs = [], ...evidence } = result;
      for (const diagnostic of evidenceDiagnostics) diagnostics.push({ code: diagnostic.code, severity: String(diagnostic.severity || 'Warning').replace(/^./, s => s.toUpperCase()),
        ref: diagnostic.ref ?? diagnostic.path ?? '[evidence]', message: diagnostic.message });
      for (const input of inputs) if (input.path && input.path !== '[rejected path]') {
        const verified = inspect(input.path, false);
        if (verified) inputMap.set(verified.path, { path: verified.path, sha256: verified.sha256 });
      }
      item.evidence.push(evidence);
    }
    item.planRefs.sort((a, b) => compare(a.path, b.path));
    item.roleLinks.sort((a, b) => compare(a.role, b.role));
    item.evidence.sort((a, b) => compare(`${a.kind}:${a.path}`, `${b.kind}:${b.path}`));
    workItems.push(item);
  }
  for (const id of evidenceByItem.keys()) if (!ids.has(id)) addDiagnostic('UnknownEvidenceWorkItem', '[evidence]', `${plain(id)}에 해당하는 업무가 없습니다.`, 'Error');
  for (const toolRef of TOOLS) inspect(toolRef, false);
  for (const plan of plans.values()) {
    plan.roles = ROLES.filter(role => plan.roles.includes(role));
    plan.workItemIds = unique(plan.workItemIds);
    plan.sourceRefs.sort((a, b) => compare(`${a.path}:${a.role}:${a.anchor ?? ''}`, `${b.path}:${b.role}:${b.anchor ?? ''}`));
    plan.canonicalRefs.sort((a, b) => compare(`${a.path}:${a.anchor ?? ''}`, `${b.path}:${b.anchor ?? ''}`));
    const itemPresence = workItems.filter(item => plan.workItemIds.includes(item.id)).map(item => item.codePresence);
    plan.codePresence = itemPresence.includes('Present') ? 'Present' : 'Unknown';
  }
  const documents = [...docs.values()].sort((a, b) => compare(a.path, b.path));
  const result = { schemaVersion: 'planning-app-production-index.v1', scope: {
    registeredIndex: INDEX, recursiveRoot: TREE, legacySourceRegistry: SOURCES, bindingsPath,
    registeredPlanCount: registered.size, discoveredPlanCount: plans.size, documentCount: documents.length,
    semanticDocumentCount: documents.filter(doc => doc.kind === 'PlanDocument' || doc.kind === 'LegacyReference' && doc.planIds.length).length,
    referenceDocumentCount: documents.filter(doc => !(doc.kind === 'PlanDocument' || doc.kind === 'LegacyReference' && doc.planIds.length)).length,
    workItemCount: workItems.length, boundPlanCount: [...plans.values()].filter(plan => plan.workItemIds.length).length,
    roles: ROLES, codeSurvey: 'ExplicitWorkItemReferencesOnly', legacyCodeSurvey: 'HistoricalInformationalOnly',
    excludedGeneratedInputPaths: OUTPUTS,
    canonicalPolicy: 'ExplicitRegisteredAndCanonicalReferencesOnly', approvalPolicy: 'SourceTextIsNotApproval',
    limitation: '전체 기획 목록과 명시적 업무 참조를 조회합니다. 미연결 기획의 코드 부재·구현 충족·실행 성공을 판정하지 않습니다.'
  }, plans: [...plans.values()].sort((a, b) => compare(a.id, b.id)), documents,
    workItems: workItems.sort((a, b) => compare(a.id, b.id)),
    diagnostics: [...new Map(diagnostics.map(value => [stable(value), value])).values()].sort((a, b) => compare(stable(a), stable(b))),
    inputs: [...inputMap.values()].sort((a, b) => compare(a.path, b.path)) };
  return { ...result, fingerprint: digest(stable(result)) };
}
