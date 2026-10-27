"""r21 소스 경로/해시 결속만 확인한다. API·DB·환경 자격값은 읽지 않는다."""
import hashlib
import os
from pathlib import Path
import xml.etree.ElementTree as ET

SCHEMA = 'cargo-operations-r21.source-binding.v1'
PROJECT = 'eng/CargoOperationsVerificationR21/CargoOperationsVerificationR21.csproj'
EXCLUDED = {'bin', 'obj', '.git', '.vs', '.private', 'private', 'artifacts', '.codex-build', 'node_modules', '__pycache__'}
EXTENSIONS = {'.cs', '.csproj', '.props', '.targets', '.razor', '.cshtml', '.js', '.css', '.json', '.py', '.ps1'}

def _need(condition, code):
    if not condition: raise RuntimeError(code)

def _snapshot(root):
    files, projects = set(), set()
    def full(path):
        path = Path(os.path.abspath(path))
        _need(path.is_relative_to(root), 'R21SourceOutsideRepository')
        return path
    def add(path):
        path = full(path)
        _need(path.is_file(), 'R21SourceMissing')
        relative = path.relative_to(root)
        _need(not any(part.casefold() in EXCLUDED - {'artifacts'} for part in relative.parts), 'R21ExcludedSource')
        files.add(relative.as_posix())
    def visit(project):
        project = full(project)
        key = str(project).casefold()
        if key in projects: return
        projects.add(key)
        add(project)
        for directory, children, names in os.walk(project.parent):
            children[:] = [name for name in children if name.casefold() not in EXCLUDED
                           and not Path(directory, name).is_symlink()
                           and not getattr(Path(directory, name), 'is_junction', lambda: False)()]
            for name in names:
                lower = name.casefold()
                extension = Path(name).suffix.casefold()
                if extension in EXTENSIONS and '.local.' not in lower and not lower.endswith('.local.json') and not (extension == '.json' and 'secret' in lower):
                    add(Path(directory, name))
        for item in ET.parse(project).iter():
            tag = item.tag.rsplit('}', 1)[-1]
            if tag not in ('ProjectReference', 'Compile', 'EmbeddedResource'): continue
            include = item.get('Include')
            if not include or not include.strip(): continue
            _need(not any(value in include for value in '$*?;'), 'R21SourceIncludeUnsupported')
            target = full(project.parent / include.replace('\\', '/'))
            visit(target) if tag == 'ProjectReference' else add(target)
    visit(root / PROJECT)
    add(root / 'eng/verification/cargo-ai-device-r114.py')
    add(root / 'eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json')
    for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'NuGet.Config', 'global.json'):
        if (root / name).is_file(): add(root / name)
    return [{'path': path, 'sha256': hashlib.sha256((root / path).read_bytes()).hexdigest()} for path in sorted(files)]

def validate(root, ready, manifest):
    _need(ready.get('schemaVersion') == 'cargo-operations-verification-r21.r1', 'R21ReadySchemaMismatch')
    _need(ready.get('executionMode') == 'Simulation'
          and ready.get('database') == 'mirror_operations_r21_20261022'
          and ready.get('baseUrl') == 'http://127.0.0.1:5369'
          and str(ready.get('runStableId', '')).startswith('cargo-operations-r21:'), 'R21ReadyEnvironmentMismatch')
    binding = ready.get('sourceBinding') or {}
    _need(manifest.get('schemaVersion') == SCHEMA and binding.get('schemaVersion') == SCHEMA, 'R21SourceBindingMissing')
    _need(manifest.get('runStableId') == ready.get('runStableId') and manifest.get('database') == ready.get('database'), 'R21SourceRunMismatch')
    current = _snapshot(root)
    _need(manifest.get('sources') == current, 'R21SourceChangedWithinRun')
    digest = hashlib.sha256(''.join(item['path'] + '\t' + item['sha256'] + '\n' for item in current).encode('utf-8')).hexdigest()
    _need(manifest.get('manifestSha256') == digest == binding.get('manifestSha256')
          and binding.get('sourceCount') == len(current), 'R21SourceBindingMismatch')
    return digest
