// 기획 정본을 수정하지 않는 목록·상태판·인계 사본 도구. 외부 패키지/통신 불필요.
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import { fileURLToPath } from 'node:url';
import { buildIndex } from './catalog.mjs';

const directory = path.dirname(fileURLToPath(import.meta.url));
export const defaultRoot = path.resolve(directory, '../../..');
export const defaultOutput = 'docs/AI/generated/planning-app-production';

export function inside(root, relative) {
  if (typeof relative !== 'string' || !relative || path.isAbsolute(relative) || /[\\:]|[\x00-\x1f]/.test(relative) || relative.split('/').includes('..')) throw new Error('UnsafePath');
  const base = fs.realpathSync(root), target = path.resolve(base, relative);
  if (!target.startsWith(base + path.sep)) throw new Error('OutsideRepository');
  let ancestor = target;
  while (!fs.existsSync(ancestor)) {
    // existsSync follows links; a dangling link must not be treated as a new directory.
    let entry;
    try { entry = fs.lstatSync(ancestor); } catch (error) { if (error.code !== 'ENOENT') throw error; }
    if (entry?.isSymbolicLink()) throw new Error('DanglingSymlink');
    ancestor = path.dirname(ancestor);
  }
  const resolved = fs.realpathSync(ancestor);
  if (resolved !== base && !resolved.startsWith(base + path.sep)) throw new Error('SymlinkOutsideRepository');
  return target;
}

function md(value) { return String(value ?? '').replace(/[\r\n|]/g, ' ').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
function refPath(value) { return typeof value === 'string' ? value : value?.path; }
function markdownLink(ref) { return `[${md(ref)}](../../../${ref.split('/').map(encodeURIComponent).join('/')})`; }
export function intakeLines(intake) {
  if (!intake || intake.state === 'NotReviewed') return ['- 제작 입력: NotReviewed (미검토). 코드 부재나 개발 승인을 뜻하지 않습니다.'];
  const changed = intake.state === 'Changed' || intake.inputState === 'Changed';
  const lines = [`- 제작 입력: ${md(intake.state)} / 입력 준비: ${md(intake.inputState)} / 사람 검토: ${md(intake.reviewState)}`,
    `- 대상 앱: ${(intake.targetAppIds || []).map(md).join(', ')} / 영향 역할: ${(intake.impactedRoles || []).map(md).join(', ')}`,
    `- 실행 승인: ${md(intake.approval?.state)} / 환경 준비: ${md(intake.environment?.state)} (입력 확인과 별도)`];
  if (changed) lines.push('- 입력 기준 변경 감지: 아래 내용은 입력 당시 기록이며, 현재 코드의 미해결 판정이 아닙니다. 현재 입력과 후속 결과를 대조해야 합니다.');
  for (const ref of intake.sourceRefs || []) lines.push(`- 입력 참조: ${md(ref.path)} · ${md(ref.state)} · SHA256 ${md(ref.sha256)}`);
  for (const [name, fact] of Object.entries(intake.profile?.fields || {})) lines.push(`- 앱 공통 ${md(name)}: ${md(fact.status)} · ${md(fact.value)}`);
  for (const [name, fact] of Object.entries(intake.sections || {})) lines.push(`- 입력 ${md(name)}: ${md(fact.status)} · ${md(fact.value)}`);
  for (const question of intake.questions || []) lines.push(`- 검토 질문 ${md(question.id)}${question.critical ? ' [필수]' : ''}: ${md(question.text)}`);
  for (const conflict of intake.conflicts || []) lines.push(`- ${changed ? '이전 입력에 기록된 충돌' : '충돌'} ${md(conflict.id)}: ${md(conflict.description)}`);
  for (const blocker of intake.blockers || []) lines.push(`- 인계 차단: ${md(typeof blocker === 'string' ? blocker : blocker.message || blocker.code)}`);
  for (const delivery of intake.deliverables || []) lines.push(`- 요구 결과물: ${md(delivery.id)} · ${md(delivery.kind)} · ${delivery.required ? '필수' : '선택'} (실행 결과 아님)`);
  for (const check of intake.environment?.checks || []) lines.push(`- 환경 ${md(check.id)}: ${md(check.state)} · ${md(check.reason)} · 대상 ${(check.deliverableIds || []).map(md).join(', ')}`);
  if (intake.environment?.blockedDeliverableIds?.length) lines.push(`- 환경 차단 결과물: ${intake.environment.blockedDeliverableIds.map(md).join(', ')}`);
  return lines;
}
export function renderMarkdown(index) {
  const lines = ['# 기획에서 역할 앱까지 — 생성 현황표', '', '> 생성물입니다. 원문·관계 자료를 수정한 뒤 다시 생성하세요. 코드 존재·시험·화면·APK·설치는 각각 별도 증거입니다.', '',
    `입력 지문: \`${index.fingerprint}\``, '', `기획 ${index.plans.length}개 / 관련 문서 ${index.documents.length}개 / 상세 업무 ${index.workItems.length}개. 전체 의미 충족이나 현재 시험 통과 수가 아닙니다.`, '',
    '## 기획 목록', '', '| 기획 | 분류 | 연결 업무 | 원문 |', '| --- | --- | --- | --- |'];
  for (const p of index.plans) {
    const refs = (p.canonicalRefs?.length ? p.canonicalRefs : p.sourceRefs).slice(0, 3).map(refPath).filter(Boolean);
    lines.push(`| ${md(p.id)} — ${md(p.title)} | ${md(p.category)} | ${(p.workItemIds || []).length} | ${refs.map(markdownLink).join('<br>')} |`);
  }
  lines.push('', '## 역할 앱 상세', '');
  for (const w of index.workItems) {
    lines.push(`### ${md(w.title)} (${md(w.id)})`, '', `역할: ${w.roles.join(', ')} / 코드: ${md(w.codePresence)} / 시험 소스: ${md(w.testCodePresence)}`, '');
    lines.push(...intakeLines(w.intake), '');
    for (const p of w.planRefs) lines.push(`- 기획: ${markdownLink(p.path)} · ${md(p.revision)} · ${md(p.state)}`);
    for (const e of w.evidence) lines.push(`- ${md(e.kind)}: 결과 ${md(typeof e.result === 'object' ? e.result.status : e.result)} / 현재성 ${md(e.freshness)}. ${md(e.summary)}${e.path ? ' ' + markdownLink(e.path) : ''}`);
    if (!w.evidence.length) lines.push('- 실행 증거: 미연결. 시험 코드 존재만으로 실행을 판정하지 않습니다.');
    for (const note of w.remaining || []) lines.push(`- 남은 확인: ${md(note)}`);
    lines.push('');
  }
  lines.push('## 참조 점검', '', ...index.diagnostics.map(d => `- ${md(d.severity)} · ${md(d.code)} · ${md(d.ref)} · ${md(d.message)}`), '', '## 조사 범위', '', '```json', JSON.stringify(index.scope, null, 2), '```', '');
  return lines.join('\n');
}

export function renderHtml(index, template = fs.readFileSync(path.join(directory, 'viewer.html'), 'utf8')) {
  // inert JSON에도 </script>를 넣을 수 없도록 escape. 화면 값은 textContent로만 출력한다.
  const view = { ...index, viewerFileRefs: [...viewerFileRefs(index)].sort() };
  return template.replace('__INDEX_JSON__', JSON.stringify(view).replace(/</g, '\\u003c').replace(/>/g, '\\u003e').replace(/&/g, '\\u0026').replace(/\u2028/g, '\\u2028').replace(/\u2029/g, '\\u2029'));
}

// The viewer and HTTP server share one allowlist. Raw artifacts/config JSON are never exposed.
export function viewerFileRefs(index) {
  const allow = new Set();
  const visit = v => {
    if (v && typeof v === 'object') {
      if (typeof v.path === 'string' && /\.(md|cs|razor|csproj|png|mjs|ps1)$/i.test(v.path) && !v.path.startsWith('artifacts/')) allow.add(v.path);
      for (const child of Object.values(v)) visit(child);
    }
  };
  visit(index);
  for (const w of index.workItems || []) for (const ref of w.workOrderRefs || []) {
    const p = refPath(ref);
    if (p?.startsWith('docs/') && p.endsWith('.work-order.json')) allow.add(p);
  }
  return allow;
}

export function outputContents(index) {
  return { '.json': JSON.stringify(index, null, 2) + '\n', '.md': renderMarkdown(index), '.html': renderHtml(index) };
}

export function writeOutputs(root, index, output = defaultOutput) {
  for (const [extension, text] of Object.entries(outputContents(index))) {
    const target = inside(root, output + extension);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, text, 'utf8');
  }
}

export function checkOutputs(root, index, output = defaultOutput) {
  const problems = [];
  for (const [extension, text] of Object.entries(outputContents(index))) {
    const target = inside(root, output + extension);
    if (!fs.existsSync(target)) problems.push('MissingOutput:' + extension);
    else if (fs.readFileSync(target, 'utf8') !== text) problems.push('StaleOrModifiedOutput:' + extension);
  }
  return problems;
}

export function handoff(index, planId) {
  const plan = index.plans.find(p => p.id === planId || (p.compatibilityIds || []).includes(planId));
  if (!plan) throw new Error('PlanNotFound');
  const workItems = index.workItems.filter(w => (plan.workItemIds || []).includes(w.id));
  // 사본에는 상태판용 참조·요약만 포함한다. 원문/소스/로그/첨부/비밀값을 자동 복사하지 않는다.
  const packet = { schemaVersion: 'planning-app-handoff.v1', sourceFingerprint: index.fingerprint,
    authority: 'ReferenceOnly_NotDevelopmentApproval', privacy: 'ReferencesAndCuratedSummariesOnly_NoRawDocumentsOrLogs',
    plan, workItems, diagnostics: index.diagnostics.filter(d => d.ref && JSON.stringify({ plan, workItems }).includes(d.ref)) };
  const lines = [`# ${md(plan.title)} — 기획·개발 검토용 인계`, '', '> 원문을 자동 첨부하지 않은 참조·요약 사본입니다. 기획 승인이나 개발 실행 허가가 아닙니다. 저장소 접근이 없는 GPT는 미첨부 원문을 읽었다고 가정하면 안 됩니다.', '',
    `기획: ${plan.id}`, `입력 지문: ${index.fingerprint}`, '', '## 읽을 원문', ...plan.sourceRefs.map(r => `- ${r.path} · SHA256 ${r.sha256}`), '', '## 현재 연결과 남은 확인'];
  for (const w of workItems) { lines.push('', `### ${md(w.title)}`, `- 역할: ${w.roles.join(', ')}`, `- 코드: ${w.codePresence}; 시험 소스: ${w.testCodePresence}`, ...intakeLines(w.intake)); for (const e of w.evidence) lines.push(`- ${e.kind}: ${md(typeof e.result === 'object' ? e.result.status : e.result)} / ${e.freshness} · ${md(e.summary)}`); for (const gap of w.remaining || []) lines.push(`- 남은 확인: ${md(gap)}`); }
  lines.push('', '## 다음 제작 입력', '', '대상 업무 / 관련 역할 / 시작·끝 / 입력·행동·결과 / 정상·실패·회복 / 허용 범위 / 필요한 결정 / 검증·화면·테스트 APK 조건을 정리하고 기존 코드와 연결하세요. 금전·권한·개인정보 미정 사항은 임의 확정하지 마세요.', '');
  return { packet, markdown: lines.join('\n') };
}

export function exportHandoff(root, index, planId, destination) {
  if (!destination.startsWith('artifacts/local/')) throw new Error('ExportMustBeLocalArtifact');
  const target = inside(root, destination);
  if (fs.existsSync(target)) throw new Error('ExportDestinationAlreadyExists');
  const result = handoff(index, planId);
  fs.mkdirSync(target, { recursive: true });
  fs.writeFileSync(path.join(target, 'handoff.json'), JSON.stringify(result.packet, null, 2) + '\n');
  fs.writeFileSync(path.join(target, 'handoff.md'), result.markdown);
  return target;
}

export function createReadOnlyServer(root, index, output = defaultOutput) {
  const allow = new Set([output + '.html', output + '.json', output + '.md', ...viewerFileRefs(index)]);
  return http.createServer((req, res) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Cache-Control', 'no-store');
    if (!/^(127\.0\.0\.1|localhost)(:\d+)?$/.test(req.headers.host || '')) { res.writeHead(403).end('LoopbackHostRequired'); return; }
    if (!['GET', 'HEAD'].includes(req.method)) { res.writeHead(405).end('ReadOnly'); return; }
    let ref;
    try { ref = decodeURIComponent(req.url.split('?')[0]).replace(/^\//, ''); } catch { res.writeHead(400).end('BadPath'); return; }
    if (!ref) ref = output + '.html';
    if (!allow.has(ref)) { res.writeHead(404).end('NotInReferenceAllowlist'); return; }
    try {
      const target = inside(root, ref), bytes = fs.readFileSync(target);
      res.setHeader('Content-Type', ref === output + '.html' ? 'text/html; charset=utf-8' : ref.endsWith('.png') ? 'image/png' : 'text/plain; charset=utf-8');
      res.writeHead(200).end(req.method === 'HEAD' ? undefined : bytes);
    } catch { res.writeHead(404).end('ReferenceUnavailable'); }
  });
}

export function parseArgs(args) {
  const options = { mode: args[0] || 'check', root: defaultRoot, output: defaultOutput, bindings: 'eng/planning-inquiries/app-production/role-app-links.json', evidence: [] };
  for (let i = 1; i < args.length; i += 2) {
    const key = args[i]?.replace(/^--/, ''), value = args[i + 1];
    if (!['root', 'output', 'bindings', 'evidence', 'plan', 'destination', 'port', 'work-item'].includes(key) || !value) throw new Error('InvalidArguments');
    if (key === 'evidence') options.evidence.push(value); else options[key] = value;
  }
  if (!['write', 'check', 'export', 'serve', 'intake-check'].includes(options.mode)) throw new Error('UnknownMode');
  return options;
}

export function main(args) {
  const opts = parseArgs(args), index = buildIndex(opts.root, opts.bindings, { evidencePaths: opts.evidence });
  if (opts.mode === 'intake-check') {
    if (!opts['work-item']) throw new Error('WorkItemRequired');
    const work = index.workItems.find(w => w.id === opts['work-item']);
    if (!work) throw new Error('WorkItemNotFound');
    console.log(JSON.stringify({ workItemId: work.id, authority: 'ReadOnly_NotDevelopmentApproval', intake: work.intake }, null, 2));
    if (work.intake.state !== 'Confirmed') process.exitCode = 3;
    else if (index.diagnostics.some(d => d.severity?.toLowerCase() === 'error')) process.exitCode = 2;
    return;
  }
  if (opts.mode === 'write') writeOutputs(opts.root, index, opts.output);
  if (opts.mode === 'check') { const problems = checkOutputs(opts.root, index, opts.output); if (problems.length) throw new Error(problems.join(',')); }
  if (opts.mode === 'export') { if (!opts.plan || !opts.destination) throw new Error('PlanAndDestinationRequired'); exportHandoff(opts.root, index, opts.plan, opts.destination); }
  if (opts.mode === 'serve') {
    const problems = checkOutputs(opts.root, index, opts.output); if (problems.length) throw new Error(problems.join(','));
    if (index.diagnostics.some(d => d.severity?.toLowerCase() === 'error')) throw new Error('InvalidPlanningInputs');
    const port = Number(opts.port || 5388); if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('InvalidPort');
    const server = createReadOnlyServer(opts.root, index, opts.output);
    server.on('error', e => { console.error(e.code || 'ServeFailed'); process.exitCode = 1; });
    server.listen(port, '127.0.0.1', () => console.log(`PlanningViewer:http://127.0.0.1:${port}/`)); return;
  }
  console.log(JSON.stringify({ mode: opts.mode, plans: index.plans.length, documents: index.documents.length, workItems: index.workItems.length, diagnostics: index.diagnostics.length, fingerprint: index.fingerprint }));
  if (index.diagnostics.some(d => d.severity?.toLowerCase() === 'error')) process.exitCode = 2;
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { main(process.argv.slice(2)); } catch (e) { console.error(e.message); process.exitCode = 1; }
}
