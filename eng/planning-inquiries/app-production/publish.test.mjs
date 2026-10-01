import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const script = fs.readFileSync(fileURLToPath(new URL('../../release/publish-mobile-field-test.ps1', import.meta.url)), 'utf8');
const quote = value => `'${value.replaceAll("'", "''")}'`;
function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'app-package-plan-'));
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'app-package-key-'));
  t.after(() => {
    fs.rmSync(root, { recursive: true, force: true });
    fs.rmSync(outside, { recursive: true, force: true });
  });
  const scriptPath = path.join(root, 'eng/release/publish-mobile-field-test.ps1');
  fs.mkdirSync(path.dirname(scriptPath), { recursive: true });
  fs.writeFileSync(scriptPath, script);
  const environment = { ...process.env };
  delete environment.SSALDDEL_ANDROID_KEYSTORE_PASSWORD;
  delete environment.SSALDDEL_ANDROID_KEY_PASSWORD;
  const run = (args = '', server = 'https://field-test.example.invalid/') => {
    const result = spawnSync('pwsh', ['-NoLogo', '-NoProfile', '-NonInteractive', '-Command',
      `& ${quote(scriptPath)} -ServerBaseAddress ${quote(server)} ${args}`],
    { encoding: 'utf8', env: environment, timeout: 20000, cwd: root });
    assert.equal(result.error, undefined);
    return { ...result, text: `${result.stdout}\n${result.stderr}` };
  };
  return { root, outside, run };
}

test('APK PlanOnly keeps the historical default of all four apps', t => {
  const f = fixture(t); const result = f.run('-PlanOnly');
  assert.equal(result.status, 0, result.text);
  for (const name of ['orderer', 'restaurant', 'driver', 'admin']) assert.match(result.text, new RegExp(`\\b${name}\\b`));
  assert.match(result.text, /formats=APK\+AAB/);
  assert.equal(fs.existsSync(path.join(f.root, 'artifacts')), false);
});

test('APK PlanOnly selects only restaurant and never creates packages or evidence', t => {
  const f = fixture(t); const result = f.run("-PlanOnly -AppNames @('restaurant') -PlanningWorkItemId 'APP-TEST'");
  assert.equal(result.status, 0, result.text);
  assert.match(result.text, /RestaurantDeskApp\/RestaurantDeskApp.csproj/);
  for (const project of ['OrdererApp', 'FDriverApp', 'SsalddelAdminApp']) assert.equal(result.text.includes(project), false);
  assert.equal(fs.existsSync(path.join(f.root, 'artifacts')), false);
});

test('APK target selection rejects unknown, empty, repeated and case-duplicate names', t => {
  const f = fixture(t);
  for (const input of ["@('unknown')", '@()', "@('')", "@('restaurant','restaurant')", "@('restaurant','Restaurant')"]) {
    const result = f.run(`-PlanOnly -AppNames ${input}`);
    assert.notEqual(result.status, 0, `selection unexpectedly accepted: ${input}`);
  }
  assert.equal(fs.existsSync(path.join(f.root, 'artifacts')), false);
});

test('APK target selection keeps public HTTPS and version guards in PlanOnly', t => {
  const f = fixture(t);
  for (const server of ['http://example.invalid/', 'https://localhost/', 'https://10.0.2.2/',
    'https://user:pass@example.invalid/', 'https://example.invalid/?token=x', 'https://example.invalid/#fragment']) {
    assert.notEqual(f.run("-PlanOnly -AppNames @('restaurant')", server).status, 0);
  }
  assert.notEqual(f.run('-PlanOnly -VersionCode 0').status, 0);
  assert.notEqual(f.run("-PlanOnly -Version 'bad'").status, 0);
});

test('APK execution still requires an external key file and both password environment settings', t => {
  const f = fixture(t);
  assert.match(f.run("-AppNames @('restaurant')").text, /KeystorePath is required/);
  const localKey = path.join(f.root, 'fixture.keystore');
  fs.writeFileSync(localKey, 'not a real key');
  const local = f.run(`-AppNames @('restaurant') -KeystorePath ${quote(localKey)}`);
  assert.notEqual(local.status, 0); assert.match(local.text, /KeystorePath must be outside the repository/);
  const externalKey = path.join(f.outside, 'fixture.keystore');
  fs.writeFileSync(externalKey, 'not a real key');
  const external = f.run(`-AppNames @('restaurant') -KeystorePath ${quote(externalKey)}`);
  assert.notEqual(external.status, 0); assert.match(external.text, /Set SSALDDEL_ANDROID_KEYSTORE_PASSWORD/);
  assert.equal(fs.existsSync(path.join(f.root, 'artifacts')), false);
});

test('APK key containment cannot be bypassed through a junction outside the repository', t => {
  const f = fixture(t);
  const localKey = path.join(f.root, 'fixture.keystore');
  fs.writeFileSync(localKey, 'not a real key');
  const linked = path.join(f.outside, 'linked-repo');
  try { fs.symlinkSync(f.root, linked, 'junction'); }
  catch (error) { if (error.code === 'EPERM') { t.skip('junction unavailable'); return; } throw error; }
  const result = f.run(`-AppNames @('restaurant') -KeystorePath ${quote(path.join(linked, 'fixture.keystore'))}`);
  assert.notEqual(result.status, 0); assert.match(result.text, /KeystorePath must not traverse/);
  assert.equal(fs.existsSync(path.join(f.root, 'artifacts')), false);
});
