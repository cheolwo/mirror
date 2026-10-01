import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { collectIntakeInputs } from './intake.mjs';

const SCHEMA = 'planning-app-evidence.v1';
const LINKS = 'eng/planning-inquiries/app-production/role-app-links.json';
const TOOLS = ['eng/planning-inquiries/app-production/evidence.mjs', 'eng/common/planning-evidence.ps1'];
const INTAKE_TOOL = 'eng/planning-inquiries/app-production/intake.mjs';
const INTAKE_REFS = ['appProfileRef', 'intakeRef', 'intakeReviewRef'];
const RESULTS = new Set(['Passed', 'Failed', 'NotRun', 'NotApplicable', 'Unknown']);
const EXCLUDED = new Set(['.git', '.vs', 'bin', 'obj', 'artifacts', 'node_modules', 'vendor']);
const SOURCE = /\.(cs|razor|csproj|fsproj|sln|slnx|props|targets|json|ps1|mjs|js|css|html|md|xml|config|xaml)$/i;
const digest = value => crypto.createHash('sha256').update(value).digest('hex');
const plain = value => typeof value === 'string' ? value.replace(/[\u0000-\u001f\u007f]/g, ' ').slice(0, 600) : '';
const stable = value => JSON.stringify(value, (_key, item) => item && typeof item === 'object' && !Array.isArray(item)
  ? Object.fromEntries(Object.entries(item).sort(([a], [b]) => a.localeCompare(b, 'en'))) : item);
const pathOf = ref => typeof ref === 'string' ? ref : ref?.path;

// Lexical and physical containment are both required, including junction parents of a missing file.
export function evidencePath(root, relative) {
  if (typeof relative !== 'string' || !relative || path.isAbsolute(relative) || /^[a-z]:/i.test(relative)
      || /[\u0000-\u001f<>:"|?*#]/.test(relative) || relative.startsWith('\\') || relative.startsWith('/')) {
    throw new Error('EvidencePathInvalid');
  }
  const normalized = relative.replaceAll('\\', '/');
  if (normalized.split('/').some(part => !part || part === '.' || part === '..' || /[ .]$/.test(part))) {
    throw new Error('EvidencePathInvalid');
  }
  const resolvedRoot = fs.realpathSync(root);
  const target = path.resolve(resolvedRoot, normalized);
  let ancestor = target;
  while (!fs.existsSync(ancestor)) {
    try {
      if (fs.lstatSync(ancestor).isSymbolicLink()) throw new Error('EvidencePathEscape');
    } catch (error) {
      if (!['ENOENT', 'ENOTDIR'].includes(error.code)) throw error;
    }
    ancestor = path.dirname(ancestor);
  }
  const physical = fs.realpathSync(ancestor);
  const relation = path.relative(resolvedRoot, physical);
  if (relation === '..' || relation.startsWith(`..${path.sep}`) || path.isAbsolute(relation)) throw new Error('EvidencePathEscape');
  return { relative: normalized, absolute: target };
}

function fileInput(root, relative) {
  const target = evidencePath(root, relative);
  if (!fs.existsSync(target.absolute)) return { path: target.relative, state: 'Missing', sha256: null, bytes: null };
  if (!fs.statSync(target.absolute).isFile()) throw new Error('EvidenceInputNotFile');
  const data = fs.readFileSync(target.absolute);
  return { path: target.relative, state: 'Present', sha256: digest(data), bytes: data.length };
}

function readJson(root, relative) {
  const target = evidencePath(root, relative);
  if (fs.statSync(target.absolute).size > 16 * 1024 * 1024) throw new Error('EvidenceTooLarge');
  return JSON.parse(fs.readFileSync(target.absolute, 'utf8').replace(/^\uFEFF/, ''));
}

function binding(item) {
  return {
    id: item.id, planRefs: item.planRefs ?? [], roles: item.roles ?? [],
    codeRefs: (item.codeRefs ?? []).map(pathOf), testRefs: (item.testRefs ?? []).map(pathOf),
    roleLinks: (item.roleLinks ?? []).map(link => ({ role: link.role, codeRefs: (link.codeRefs ?? []).map(pathOf), note: link.note ?? '' })),
    workOrderRefs: (item.workOrderRefs ?? []).map(pathOf),
    // Missing optional r9 fields must not rewrite historical r8 binding hashes.
    ...Object.fromEntries(INTAKE_REFS.filter(key => Object.hasOwn(item, key)).map(key => [key, item[key]]))
  };
}

function registry(root, linksPath, id) {
  const data = readJson(root, linksPath);
  if (data.schemaVersion !== 'planning-app-production-links.v1' || !Array.isArray(data.items)) throw new Error('PlanningLinksSchemaInvalid');
  const matches = data.items.filter(item => item.id === id);
  if (matches.length !== 1) throw new Error('PlanningWorkItemMissingOrDuplicate');
  return { item: matches[0], bindingSha256: digest(stable(binding(matches[0]))) };
}

function itemInputs(item) {
  return [...(item.planRefs ?? []).map(pathOf), ...(item.codeRefs ?? []).map(pathOf), ...(item.testRefs ?? []).map(pathOf),
    ...(item.roleLinks ?? []).flatMap(link => (link.codeRefs ?? []).map(pathOf)), ...(item.workOrderRefs ?? []).map(pathOf)];
}

function inventory(root, relative, excludedPaths = []) {
  const target = evidencePath(root, relative);
  if (!fs.existsSync(target.absolute)) return { path: target.relative, state: 'Missing', files: [] };
  if (!fs.statSync(target.absolute).isDirectory()) throw new Error('EvidenceRootNotDirectory');
  const files = [];
  function visit(directory, prefix) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name, 'en'))) {
      if (EXCLUDED.has(entry.name) || entry.name.endsWith('.worktrees')) continue;
      const relativePath = `${prefix}/${entry.name}`;
      if (excludedPaths.includes(relativePath)) continue;
      // Refuse links rather than following a nested worktree or silently omitting source.
      if (entry.isSymbolicLink()) throw new Error('EvidenceSourceLinkUnsupported');
      if (entry.isDirectory()) visit(evidencePath(root, relativePath).absolute, relativePath);
      else if (entry.isFile() && SOURCE.test(entry.name) && !/^appsettings\..*local|^secrets\.|^\.env/i.test(entry.name)) {
        files.push(fileInput(root, relativePath));
      }
    }
  }
  visit(target.absolute, target.relative);
  return { path: target.relative, state: 'Present', files };
}

function gitContext(root) {
  try {
    const git = args => execFileSync('git', ['-C', root, ...args], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
    const commit = git(['rev-parse', 'HEAD']);
    const status = git(['status', '--porcelain=v1', '-z', '--untracked-files=all']);
    // Store no absolute paths, remote URLs, command lines, environment values, or status text.
    return { commit, dirty: status.length > 0, dirtyEntryCount: status.split('\0').filter(Boolean).length, dirtyStateSha256: digest(status) };
  } catch { return { commit: null, dirty: null, dirtyEntryCount: null, dirtyStateSha256: null }; }
}

function safeScope(scope = {}) {
  const array = key => Array.isArray(scope[key]) ? scope[key].map(plain).filter(Boolean).slice(0, 200) : [];
  return {
    coverage: 'DeclaredInputsOnly', level: plain(scope.level), configuration: plain(scope.configuration),
    buildTargets: array('buildTargets'), testProjects: array('testProjects'), testModes: array('testModes'), roles: array('roles'),
    testSelectionSha256: typeof scope.testSelectionSha256 === 'string' && /^[a-f0-9]{64}$/i.test(scope.testSelectionSha256) ? scope.testSelectionSha256.toLowerCase() : null,
    noRestore: scope.noRestore === true,
    exclusions: ['ProductMeaningCompletion', 'LiveHttpDb', 'NativeUi', 'DeviceInstall', 'Deployment', 'OperationalEffects']
  };
}

function safeEnvironment(environment = {}) {
  return { platform: process.platform, architecture: process.arch, node: process.version,
    powershell: /^[\d.]+$/.test(environment.powershell ?? '') ? environment.powershell : null,
    dotnet: /^[\w.+-]{1,80}$/.test(environment.dotnet ?? '') ? environment.dotnet : null,
    targetFramework: /^[\w.+-]{1,80}$/.test(environment.targetFramework ?? '') ? environment.targetFramework : null };
}

function writeJson(root, relative, value) {
  const target = evidencePath(root, relative);
  if (fs.existsSync(target.absolute)) throw new Error('EvidenceOutputAlreadyExists');
  fs.mkdirSync(path.dirname(target.absolute), { recursive: true });
  fs.writeFileSync(target.absolute, `${JSON.stringify(value, null, 2)}\n`, { flag: 'wx' });
}

// This must be called and persisted before the wrapped validation/publish command begins.
export function beginEvidence(root, options) {
  const linksPath = options.linksPath || LINKS;
  const { item, bindingSha256 } = registry(root, linksPath, options.workItemId);
  const intakeInputs = collectIntakeInputs(root, item);
  if (intakeInputs.some(input => input.state !== 'Present')) throw new Error('PlanningIntakeInputInvalid');
  const intakePaths = intakeInputs.map(input => input.path);
  if (INTAKE_REFS.some(key => Object.hasOwn(item, key))) intakePaths.push(INTAKE_TOOL);
  const paths = [...new Set([...itemInputs(item), ...intakePaths, ...TOOLS, ...(options.toolPaths ?? []), ...(options.inputPaths ?? [])])].sort();
  const sourceInputs = paths.map(relative => fileInput(root, relative));
  if (intakeInputs.some(input => sourceInputs.find(source => source.path === input.path)?.sha256 !== input.sha256?.toLowerCase())) {
    throw new Error('PlanningIntakeInputInvalid');
  }
  const planMismatch = (item.planRefs ?? []).some(ref => !/^[a-f0-9]{64}$/i.test(ref.sha256 ?? '')
    || sourceInputs.find(input => input.path === ref.path)?.sha256 !== ref.sha256.toLowerCase());
  if (planMismatch) throw new Error('PlanningPlanHashMismatch');
  if (sourceInputs.some(input => input.state === 'Missing')) throw new Error('PlanningInputMissing');
  const roots = [...new Set([...(item.codeRefs ?? []), ...(item.testRefs ?? [])].map(pathOf).map(value => path.posix.dirname(value)).filter(value => value !== '.' && value !== 'eng' && value !== 'docs')
    .concat(options.inputRoots ?? []))].sort();
  const inventoryExclusions = [linksPath, ...(item.resultRefs ?? []).map(pathOf), ...(item.evidenceRefs ?? []).map(pathOf)];
  const manifest = {
    schemaVersion: SCHEMA, workItemId: item.id, kind: plain(options.kind || 'Validation'), result: 'NotRun', freshness: 'Unknown',
    summary: '실행 전 입력 고정. 실행 성공 증거가 아닙니다.',
    execution: { phase: 'BeforeExecution', startedAt: new Date().toISOString(), finishedAt: null },
    binding: { linksPath, itemSha256: bindingSha256, linksFileSha256AtStart: fileInput(root, linksPath).sha256,
      comparison: 'SelectedItemReferencesExcludingEvidenceAndResultAttachments' },
    plans: item.planRefs, sourceInputs, inventoryExclusions, inputRoots: roots.map(relative => inventory(root, relative, inventoryExclusions)),
    scope: safeScope(options.scope), environment: safeEnvironment(options.environment), git: gitContext(root), artifacts: [], checks: []
  };
  const startPath = options.startPath;
  if (typeof startPath !== 'string' || !startPath.endsWith('.start.json')) throw new Error('EvidenceStartPathInvalid');
  writeJson(root, startPath, manifest);
  return { startPath, manifestPath: startPath.replace(/\.start\.json$/, '.json') };
}

function changedInputs(root, manifest) {
  const changed = [];
  for (const input of manifest.sourceInputs) {
    const current = fileInput(root, input.path);
    if (current.sha256 !== input.sha256 || current.state !== input.state) changed.push(input.path);
  }
  for (const sourceRoot of manifest.inputRoots) {
    if (stable(inventory(root, sourceRoot.path, manifest.inventoryExclusions ?? [])) !== stable(sourceRoot)) changed.push(sourceRoot.path);
  }
  return [...new Set(changed)].sort();
}

export function completeEvidence(root, startPath, options) {
  const start = readJson(root, startPath);
  if (start.schemaVersion !== SCHEMA || start.execution?.phase !== 'BeforeExecution' || start.execution.finishedAt !== null) {
    throw new Error('EvidenceStartInvalid');
  }
  if (!RESULTS.has(options.result)) throw new Error('EvidenceResultInvalid');
  const changedPaths = changedInputs(root, start);
  try {
    const current = registry(root, start.binding.linksPath, start.workItemId);
    if (current.bindingSha256 !== start.binding.itemSha256) changedPaths.push(start.binding.linksPath);
  } catch { changedPaths.push(start.binding.linksPath); }
  const artifacts = (options.artifactPaths ?? []).map(relative => fileInput(root, relative));
  const checks = (options.checks ?? []).map(check => ({ name: plain(check.name),
    result: RESULTS.has(check.result) ? check.result : 'Unknown', exitCode: Number.isSafeInteger(check.exitCode) ? check.exitCode : null }));
  const result = options.result === 'Passed' && checks.some(check => check.result === 'Failed') ? 'Failed' : options.result;
  const manifest = { ...start, result, freshness: changedPaths.length ? 'Stale' : artifacts.some(artifact => artifact.state === 'Missing') ? 'Missing' : 'Current',
    summary: result === 'Passed' ? '선언된 실행 범위가 통과했습니다. 제품 전체 완료를 뜻하지 않습니다.' : '선언된 실행 결과를 확인하세요. 현재성과 성공 여부는 별도입니다.',
    execution: { ...start.execution, phase: 'Completed', finishedAt: new Date().toISOString(),
      startPath, startSnapshotSha256: fileInput(root, startPath).sha256 },
    sourceChangedDuringRun: changedPaths, gitAtFinish: gitContext(root), artifacts, checks };
  const manifestPath = startPath.replace(/\.start\.json$/, '.json');
  if (manifestPath === startPath) throw new Error('EvidenceStartPathInvalid');
  writeJson(root, manifestPath, manifest);
  return { manifestPath, result: manifest.result, freshness: manifest.freshness };
}

function validInput(input) {
  return input && typeof input.path === 'string' && ['Present', 'Missing'].includes(input.state)
    && (input.state === 'Present' ? /^[a-f0-9]{64}$/i.test(input.sha256 ?? '') : input.sha256 === null);
}

export function evaluateEvidence(root, ref, item) {
  const output = { kind: plain(ref?.kind || 'Unknown'), path: '', result: 'Unknown', freshness: 'Unknown',
    summary: plain(ref?.summary), diagnostics: [], inputs: [] };
  function issue(code, message, freshness = 'Unknown') {
    output.freshness = freshness;
    output.diagnostics.push({ code, message, path: output.path, severity: 'warning' });
    return output;
  }
  try {
    const file = fileInput(root, ref?.path);
    output.path = file.path;
    output.inputs.push(file);
    if (file.state === 'Missing') return issue('EvidenceMissing', '연결된 증거 파일이 없습니다.', 'Missing');
    if (ref.format === 'HistoricalReport') {
      output.summary ||= '과거 보고서입니다. 실행 당시 입력 기준선이 없어 현재 통과 여부를 판정하지 않습니다.';
      return output;
    }
    if (ref.format !== 'EvidenceManifest') return issue('EvidenceFormatUnknown', '증거 형식이 명시되지 않았습니다.');
    const manifest = readJson(root, ref.path);
    if (manifest.schemaVersion !== SCHEMA || manifest.workItemId !== item.id || !RESULTS.has(manifest.result)
        || typeof manifest.kind !== 'string' || !manifest.kind.trim() || plain(manifest.kind) !== manifest.kind
        || manifest.execution?.phase !== 'Completed' || !Array.isArray(manifest.sourceInputs) || !manifest.sourceInputs.length
        || !manifest.sourceInputs.every(validInput) || !Array.isArray(manifest.inputRoots) || !Array.isArray(manifest.artifacts)
        || !manifest.artifacts.every(validInput) || !manifest.binding?.itemSha256 || !manifest.scope || !manifest.environment
        || !Number.isFinite(Date.parse(manifest.execution.startedAt)) || !Number.isFinite(Date.parse(manifest.execution.finishedAt))
        || Date.parse(manifest.execution.finishedAt) < Date.parse(manifest.execution.startedAt)) {
      return issue('EvidenceManifestInvalid', '완료된 실행과 입력 기준선이 유효한 증거가 아닙니다.');
    }
    output.kind = manifest.kind;
    if (ref.kind !== undefined && ref.kind !== manifest.kind) {
      return issue('EvidenceKindMismatch', '연결된 증거 종류와 실제 실행 종류가 다릅니다.');
    }
    const start = fileInput(root, manifest.execution.startPath);
    output.inputs.push(start);
    if (start.state === 'Missing') return issue('EvidenceStartMissing', '실행 전 입력 기록이 없습니다.', 'Missing');
    if (start.sha256 !== manifest.execution.startSnapshotSha256) return issue('EvidenceStartChanged', '실행 전 입력 기록이 변경되었습니다.', 'Stale');
    const before = readJson(root, start.path);
    if (before.execution?.phase !== 'BeforeExecution' || before.workItemId !== item.id || before.kind !== manifest.kind
        || before.execution.startedAt !== manifest.execution.startedAt || stable(before.sourceInputs) !== stable(manifest.sourceInputs)
        || stable(before.inputRoots) !== stable(manifest.inputRoots) || stable(before.binding) !== stable(manifest.binding)
        || stable(before.inventoryExclusions) !== stable(manifest.inventoryExclusions)
        || stable(before.scope) !== stable(manifest.scope) || stable(before.environment) !== stable(manifest.environment)) {
      return issue('EvidenceBaselineMismatch', '실행 전 입력과 완료 기록의 기준선이 일치하지 않습니다.');
    }
    // Validate every nested path before displaying any path, including stale evidence.
    for (const input of manifest.sourceInputs) evidencePath(root, input.path);
    for (const inputRoot of manifest.inputRoots) {
      evidencePath(root, inputRoot.path);
      if (!Array.isArray(inputRoot.files) || !inputRoot.files.every(validInput)) throw new Error('EvidenceManifestInvalid');
      for (const input of inputRoot.files) evidencePath(root, input.path);
    }
    const safeArtifacts = manifest.artifacts.map(artifact => ({ path: evidencePath(root, artifact.path).relative,
      sha256: artifact.sha256, state: artifact.state }));
    output.result = manifest.result;
    output.summary = plain(ref.summary) || plain(manifest.summary);
    output.scope = safeScope(manifest.scope);
    output.environment = Object.fromEntries(['platform', 'architecture', 'node', 'powershell', 'dotnet', 'targetFramework']
      .map(key => [key, /^[\w.+-]{1,80}$/.test(manifest.environment[key] ?? '') ? manifest.environment[key] : null]));
    output.startedAt = manifest.execution.startedAt;
    output.finishedAt = manifest.execution.finishedAt;
    output.artifacts = safeArtifacts;
    output.inputs.push(...manifest.sourceInputs.map(input => fileInput(root, input.path)));
    const currentBinding = digest(stable(binding(item)));
    const intakeInputs = collectIntakeInputs(root, item);
    const requiredPaths = new Set([...itemInputs(item), ...intakeInputs.map(input => input.path)]);
    if (INTAKE_REFS.some(key => Object.hasOwn(item, key))) requiredPaths.add(INTAKE_TOOL);
    if (intakeInputs.some(input => input.state !== 'Present')) {
      return issue('EvidenceIntakeInputsChanged', '현재 제작 입력이나 그 출처를 실행 당시와 같은 기준으로 확인할 수 없습니다.', 'Stale');
    }
    if (currentBinding !== manifest.binding.itemSha256 || [...requiredPaths].some(relative => !manifest.sourceInputs.some(input => input.path === relative))) {
      return issue('EvidenceBindingChanged', '현재 기획·업무 참조와 실행 당시 결속이 다릅니다.', 'Stale');
    }
    if (manifest.sourceInputs.some(input => input.state === 'Missing')) return issue('EvidenceSourceMissing', '실행 입력에 누락이 있습니다.', 'Missing');
    const changed = changedInputs(root, manifest);
    if (changed.length || manifest.sourceChangedDuringRun?.length) return issue('EvidenceInputsChanged', '실행 도중 또는 이후 관련 입력이 변경되었습니다.', 'Stale');
    for (const artifact of manifest.artifacts) {
      const currentArtifact = fileInput(root, artifact.path);
      output.inputs.push(currentArtifact);
      if (currentArtifact.state === 'Missing') return issue('EvidenceArtifactMissing', '실행 산출물이 없습니다.', 'Missing');
      if (artifact.sha256 !== currentArtifact.sha256) return issue('EvidenceArtifactChanged', '실행 산출물 해시가 변경되었습니다.', 'Stale');
    }
    output.freshness = 'Current';
    return output;
  } catch {
    return issue('EvidenceUnreadableOrUnsafe', '증거 경로 또는 형식이 안전한 읽기 범위를 벗어났습니다.');
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const request = JSON.parse(fs.readFileSync(0, 'utf8').replace(/^\uFEFF/, ''));
    const result = process.argv[2] === 'start' ? beginEvidence(request.root, request)
      : process.argv[2] === 'finish' ? completeEvidence(request.root, request.startPath, request)
        : (() => { throw new Error('EvidenceCommandInvalid'); })();
    process.stdout.write(`${JSON.stringify(result)}\n`);
  } catch (error) {
    // Fixed code only: filesystem error text may contain confidential paths or values.
    const code = /^(Evidence|Planning)[A-Za-z]+$/.test(error.message) ? error.message : 'EvidenceOperationFailed';
    process.stderr.write(`${code}\n`);
    process.exitCode = 1;
  }
}
