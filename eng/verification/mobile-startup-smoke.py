"""설치된 자사 앱의 데이터 보존 cold start 관측. 성공은 충돌 원인 해결을 뜻하지 않는다.

새 APK 설치, 앱 데이터 삭제, 권한 응답, 시스템 설정 변경은 수행하지 않는다.
원시 logcat/활동/계정 덤프는 저장하지 않고 선택 package의 구조화된 결과만 남긴다.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
ARTIFACT_ROOT = ROOT / 'artifacts' / 'local'
ALLOWED_PACKAGES = frozenset({
    'com.companyname.ssalddelapp',
    'com.companyname.ssalddelapp.cooperationreview',
    'com.companyname.ssalddelapp.closeoutreview',
    'kr.hongdal.driver', 'kr.ssalddel.fdriver',
    'com.ssalddel.ordererapp', 'com.ssalddel.restaurantdeskapp',
    'com.ssalddel.adminapp', 'com.companyname.warehousemanagerapp',
})
SHA = re.compile(r'^[a-fA-F0-9]{64}$')
SERIAL = re.compile(r'^[A-Za-z0-9][A-Za-z0-9._:-]{0,99}$')
COMPONENT = re.compile(r'^([A-Za-z0-9_.]+)/([A-Za-z0-9_.$]+)$')
REMOTE_APK = re.compile(r'^/data/app/[A-Za-z0-9_~+=./-]+/base\.apk$')
TIMESTAMP = re.compile(r'\b(\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+)')
LOG_PREFIX = re.compile(r'^\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+\s+(\d+)\s+(\d+)\s+[A-Z]\s+')


class SmokeError(RuntimeError):
    """기술 오류 원문이나 계정 정보를 출력하지 않는 고정 코드 오류."""


def require(value, code):
    if not value:
        raise SmokeError(code)


def sha_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha_file(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def checked_local_path(value, *, suffix=None, existing=False, root=None):
    raw = str(value)
    require(raw and not any(c in raw for c in '\x00\r\n"<>|?*'), 'InvalidLocalPath')
    require(not raw.startswith(('\\\\', '//')) and '://' not in raw, 'LocalPathOnly')
    colonless = raw[2:] if re.match(r'^[A-Za-z]:', raw) else raw
    require(':' not in colonless, 'AlternateDataStreamRejected')
    path = Path(raw)
    require(path.is_absolute() and '..' not in path.parts, 'AbsolutePathRequired')
    for part in (path, *path.parents):
        if part.exists() or part.is_symlink():
            metadata = part.lstat()
            require(not stat.S_ISLNK(metadata.st_mode)
                    and not (getattr(metadata, 'st_file_attributes', 0) & 0x400), 'ReparsePathRejected')
    path = path.resolve()
    if root is not None:
        require(path.is_relative_to(Path(root).resolve()) and path != Path(root).resolve(), 'OutputOutsideArtifacts')
    if suffix:
        require(path.suffix.lower() == suffix, 'FileExtensionMismatch')
    if existing:
        require(path.is_file(), 'InputFileMissing')
    return path


def validate_target(serial, package, iterations):
    require(bool(SERIAL.fullmatch(serial)), 'InvalidDeviceSerial')
    require(package in ALLOWED_PACKAGES, 'PackageNotAllowed')
    require(iterations in (3, 4, 5), 'IterationsMustBeThreeToFive')


def parse_exit_info(raw, package):
    """종료 description/state/다른 WebView 프로세스를 복사하지 않는다."""
    result = []
    for block in re.split(r'ApplicationExitInfo\s+#\d+:', raw)[1:]:
        when = re.search(r'timestamp=([0-9-]+\s+[0-9:.]+)\s+pid=(\d+)', block)
        proc = re.search(r'\bprocess=(\S+)\s+reason=(\d+)\s+\([^)]+\).*?\bstatus=(-?\d+)', block, re.S)
        if when and proc and proc.group(1) == package:
            result.append({'timestamp': when.group(1), 'pid': int(when.group(2)),
                           'reason': int(proc.group(2)), 'status': int(proc.group(3))})
    return result


def exit_key(item):
    return tuple(item[k] for k in ('timestamp', 'pid', 'reason', 'status'))


def new_exits(before, after):
    prior = {exit_key(item) for item in before}
    return [item for item in after if exit_key(item) not in prior]


def crash_line_keys(raw):
    """이전 버퍼의 줄은 해시로만 비교하고 다른 앱 내용은 저장하지 않는다."""
    return {sha_bytes(line.rstrip('\r\n').encode('utf-8')) for line in raw.splitlines()}


def safe_native_frame(line):
    frame = re.search(r'#(\d+)\s+pc\s+([a-fA-F0-9]+)\s+(\S+)(?:\s+(.*))?$', line)
    if not frame:
        return None
    module = frame.group(3).rsplit('/', 1)[-1]
    module = re.sub(r'[^A-Za-z0-9_.!+:-]', '_', module)[:100]
    symbol = re.sub(r'\s*\(BuildId:.*$', '', frame.group(4) or '')
    # 주소·기호만 필요하다. 임의 소스 경로나 메모리 문자열을 복제하지 않는다.
    symbol = re.sub(r'[^A-Za-z0-9_.$:+<>~*(),=\[\] -]', '_', symbol)[:180]
    return {'index': int(frame.group(1)), 'pc': frame.group(2).lower(),
            'module': module, 'symbol': symbol}


def extract_target_crashes(raw, package, observed_pids=(), baseline_keys=(), baseline_event_ids=()):
    """별도 crash_dump PID의 블록도 blamed package/PID로 확인해 보존한다."""
    old = set(baseline_keys)
    prior_events = set(baseline_event_ids)
    known = set(observed_pids)
    events = []
    chunks = re.split(r'(?m)(?=^.*\*\*\* \*\*\* \*\*\* \*\*\*)', raw)
    for block in chunks:
        identity = re.search(r'^.*\bpid:\s*(\d+),\s*tid:\s*(\d+),.*>>>\s*([^\s<>]+)\s*<<<.*$', block, re.M)
        if not identity or identity.group(3) != package:
            continue
        identity_line = identity.group(0).rstrip('\r\n')
        line_id = sha_bytes(identity_line.encode('utf-8'))
        stamp = TIMESTAMP.search(identity_line)
        event_id = sha_bytes(json.dumps(['native_backtrace', package, int(identity.group(1)),
                                        int(identity.group(2)), stamp.group(1) if stamp else None]).encode('utf-8'))
        if line_id in old or event_id in prior_events:
            continue
        signal = re.search(r'\bsignal\s+(\d+)\s+\(([A-Z0-9]+)\),\s*code\s+(-?\d+)', block)
        dumper = LOG_PREFIX.match(identity_line)
        # debuggerd의 별도 PID는 허용하되, interleave된 다른 dumper의 frame은 섞지 않는다.
        frames = [item for line in block.splitlines()
                  if dumper and (prefix := LOG_PREFIX.match(line))
                  and prefix.groups() == dumper.groups()
                  and (item := safe_native_frame(line)) is not None]
        events.append({'eventId': event_id, 'kind': 'native_backtrace',
                       'pid': int(identity.group(1)), 'tid': int(identity.group(2)),
                       'timestamp': stamp.group(1) if stamp else None,
                       'signal': int(signal.group(1)) if signal else None,
                       'signalName': signal.group(2) if signal else None,
                       'frames': frames, 'targetIdentityConfirmed': True})
    for line in raw.splitlines():
        fatal = re.search(r'Fatal signal\s+(\d+)\s+\(([A-Z0-9]+)\).*?in tid\s+(\d+)\s+\([^)]*\),\s*pid\s+(\d+)\s+\(', line)
        line = line.rstrip('\r\n')
        line_id = sha_bytes(line.encode('utf-8'))
        if fatal and int(fatal.group(4)) in known and line_id not in old:
            stamp = TIMESTAMP.search(line)
            event_id = sha_bytes(json.dumps(['fatal_signal', int(fatal.group(4)), int(fatal.group(3)),
                                            stamp.group(1) if stamp else None, int(fatal.group(1))]).encode('utf-8'))
            if event_id in prior_events:
                continue
            events.append({'eventId': event_id, 'kind': 'fatal_signal',
                           'pid': int(fatal.group(4)), 'tid': int(fatal.group(3)),
                           'timestamp': stamp.group(1) if stamp else None,
                           'signal': int(fatal.group(1)), 'signalName': fatal.group(2),
                           'frames': [], 'targetIdentityConfirmed': True})
    return events


def device_clock(adb):
    raw, _ = adb.call('shell', 'date', '+%Y-%m-%dT%H:%M:%S%z')
    try:
        return dt.datetime.strptime(raw.strip(), '%Y-%m-%dT%H:%M:%S%z')
    except ValueError:
        raise SmokeError('DeviceClockUnavailable') from None


def partition_crash_window(events, started, ended):
    """MM-DD 로그의 연도는 기기 관측창으로만 복원한다. 시각 불명은 통과시키지 않는다."""
    require(started.tzinfo is not None and ended.tzinfo is not None and ended >= started,
            'InvalidDeviceObservationWindow')
    fresh, uncertain, historical = [], [], 0
    # date가 초 단위라 마지막 초의 소수부만 포함한다. 시작 이전으로 창을 넓히지 않는다.
    upper = ended + dt.timedelta(seconds=1)
    for event in events:
        stamp = event.get('timestamp')
        if not stamp:
            uncertain.append({**event, 'timingStatus': 'Unknown_NoTimestamp'})
            continue
        matches, parsed_any = [], False
        for year in range(started.year - 1, ended.year + 2):
            try:
                value = dt.datetime.strptime(str(year) + '-' + stamp, '%Y-%m-%d %H:%M:%S.%f').replace(tzinfo=started.tzinfo)
                parsed_any = True
                if started <= value < upper:
                    matches.append(value)
            except ValueError:
                continue
        if len(matches) == 1:
            fresh.append({**event, 'deviceObservedAt': matches[0].isoformat(), 'timingStatus': 'InsideIterationWindow'})
        elif not parsed_any:
            uncertain.append({**event, 'timingStatus': 'Unknown_InvalidTimestamp'})
        elif not matches:
            historical += 1
        else:
            uncertain.append({**event, 'timingStatus': 'Unknown_AmbiguousYear'})
    return fresh, uncertain, historical


def foreground_matches(raw, package):
    lines = raw.splitlines()
    # 최신 top 정보가 있으면 과거/다른 display의 mResumedActivity로 대신 통과하지 않는다.
    top = [line for line in lines if re.search(r'\btopResumedActivity\b', line)]
    chosen = top or [line for line in lines if re.search(r'\b(?:mResumedActivity|ResumedActivity)\b', line)]
    if not chosen:
        return False
    for line in chosen:
        components = re.findall(r'(?<![A-Za-z0-9_.])([A-Za-z0-9_.]+)/[A-Za-z0-9_.$]+(?:\s|\})', line)
        if components != [package]:
            return False
    return True


def permission_fingerprint(raw):
    lines = sorted(line.strip() for line in raw.splitlines()
                   if re.search(r'\bandroid\.permission\.[A-Z0-9_]+:\s*granted=(?:true|false)', line))
    require(bool(lines), 'PermissionStateUnavailable')
    return sha_bytes('\n'.join(lines).encode('utf-8'))


def observed_stable(iterations, requested, *, apk_preserved, permissions_preserved, error=None):
    return (not error and len(iterations) == requested and apk_preserved and permissions_preserved
            and all(item.get('launchOk') is True and item.get('pidAliveAfterWait') is True
                    and item.get('foregroundMatches') is True
                    and item.get('newUnexpectedExitCount') == 0
                    and item.get('newNativeCrashCount') == 0
                    and item.get('nativeCrashTimingUnverifiedCount', 0) == 0 for item in iterations))


class Adb:
    def __init__(self, executable, serial):
        self.executable, self.serial = str(executable), serial

    def call(self, *arguments, timeout=35, allow_failure=False):
        try:
            result = subprocess.run([self.executable, '-s', self.serial, *arguments],
                                    stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                    timeout=timeout, check=False, shell=False)
        except (OSError, subprocess.TimeoutExpired):
            raise SmokeError('AdbCommandUnavailable') from None
        require(allow_failure or result.returncode == 0, 'AdbCommandFailed')
        return result.stdout.decode('utf-8', errors='replace'), result.returncode

    def installed_apk_sha(self, remote_path):
        require(bool(REMOTE_APK.fullmatch(remote_path)) and '..' not in remote_path.split('/'), 'RemoteApkPathRejected')
        process = subprocess.Popen([self.executable, '-s', self.serial, 'exec-out', 'cat', remote_path],
                                   stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, shell=False)
        expired = threading.Event()
        def stop():
            expired.set()
            process.kill()
        timer = threading.Timer(60, stop)
        timer.start()
        digest, count = hashlib.sha256(), 0
        try:
            while block := process.stdout.read(1024 * 1024):
                count += len(block)
                require(count <= 512 * 1024 * 1024, 'InstalledApkTooLarge')
                digest.update(block)
            process.wait(timeout=5)
            require(not expired.is_set() and process.returncode == 0 and count > 0, 'InstalledApkReadFailed')
            return digest.hexdigest()
        finally:
            timer.cancel()
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)
            process.stdout.close()


def get_pids(adb, package):
    raw, code = adb.call('shell', 'pidof', package, allow_failure=True)
    if code == 1 and not raw.strip():
        return []
    require(code == 0 and bool(re.fullmatch(r'\s*\d+(?:\s+\d+)*\s*', raw)), 'PidObservationUnavailable')
    pids = [int(value) for value in raw.split()]
    require(len(pids) <= 8 and all(value > 0 for value in pids), 'InvalidPidObservation')
    return pids


def resolve_component(adb, package):
    raw, _ = adb.call('shell', 'cmd', 'package', 'resolve-activity', '--brief',
                      '-a', 'android.intent.action.MAIN', '-c', 'android.intent.category.LAUNCHER', package)
    components = [line.strip() for line in raw.splitlines() if COMPONENT.fullmatch(line.strip())]
    require(len(components) == 1 and components[0].split('/')[0] == package, 'ExactLauncherComponentRequired')
    return components[0]


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temporary.replace(path)


def run(args):
    validate_target(args.serial, args.package, args.iterations)
    require(3 <= args.wait_seconds <= 60, 'WaitMustBeThreeToSixtySeconds')
    adb_path = checked_local_path(args.adb, suffix='.exe', existing=True)
    output = checked_local_path(args.output, root=ARTIFACT_ROOT)
    require(not output.exists(), 'FreshOutputDirectoryRequired')
    expected = args.expected_apk_sha256
    if args.apk:
        expected = sha_file(checked_local_path(args.apk, suffix='.apk', existing=True))
    require(bool(expected and SHA.fullmatch(expected)), 'ExpectedApkSha256Required')
    expected = expected.lower()
    output.mkdir(parents=True, exist_ok=False)
    adb = Adb(adb_path, args.serial)
    result = {'schema': 'mobile-startup-smoke.v1', 'startedAtUtc': utc(),
              'package': args.package, 'serialSha256': sha_bytes(args.serial.encode()),
              'expectedApkSha256': expected, 'toolSha256': sha_file(__file__),
              'requestedIterations': args.iterations, 'waitSeconds': args.wait_seconds,
              'iterations': [], 'apkPreserved': False, 'permissionsPreserved': False,
              'dataClearCommandExecuted': False, 'installationPerformed': False,
              'permissionsOrSystemSettingsChangedByTool': False,
              'crashCauseFixed': False, 'workflowComplete': False,
              'verdict': 'NotVerified', 'errorCode': None}
    remote_apk, permission_before, installed_before = None, None, None
    write_json(output / 'result.json', result)
    try:
        state, _ = adb.call('get-state')
        require(state.strip() == 'device', 'DeviceNotReady')
        raw_paths, _ = adb.call('shell', 'pm', 'path', args.package)
        paths = [line.removeprefix('package:').strip() for line in raw_paths.splitlines() if line.startswith('package:')]
        require(len(paths) == 1 and REMOTE_APK.fullmatch(paths[0]), 'SingleInstalledBaseApkRequired')
        remote_apk = paths[0]
        installed_before = adb.installed_apk_sha(remote_apk)
        require(installed_before == expected, 'InstalledApkSha256Mismatch')
        package_dump, _ = adb.call('shell', 'dumpsys', 'package', args.package)
        permission_before = permission_fingerprint(package_dump)
        component = resolve_component(adb, args.package)
        baseline_exit, _ = adb.call('shell', 'dumpsys', 'activity', 'exit-info', args.package)
        write_json(output / 'baseline.json', {'installedApkSha256': installed_before,
                   'permissionStateSha256': permission_before, 'launcherComponent': component,
                   'priorTargetExitEvents': parse_exit_info(baseline_exit, args.package)})
        for index in range(args.iterations):
            adb.call('shell', 'am', 'force-stop', args.package)
            require(not get_pids(adb, args.package), 'ForceStopNotObserved')
            before_exit, _ = adb.call('shell', 'dumpsys', 'activity', 'exit-info', args.package)
            before_crash, _ = adb.call('logcat', '-b', 'crash', '-d', '-v', 'threadtime')
            before_events = extract_target_crashes(before_crash, args.package)
            require(resolve_component(adb, args.package) == component, 'LauncherComponentChanged')
            device_started = device_clock(adb)
            launch, launch_code = adb.call('shell', 'am', 'start', '-W', '-n', component,
                                           timeout=60, allow_failure=True)
            launch_ok = launch_code == 0 and bool(re.search(r'^Status:\s*ok\s*$', launch, re.M)) and 'Error:' not in launch
            pids_immediate = get_pids(adb, args.package)
            time.sleep(args.wait_seconds)
            pids_after = get_pids(adb, args.package)
            activity, _ = adb.call('shell', 'dumpsys', 'activity', 'activities')
            after_exit, _ = adb.call('shell', 'dumpsys', 'activity', 'exit-info', args.package)
            after_crash, _ = adb.call('logcat', '-b', 'crash', '-d', '-v', 'threadtime')
            device_ended = device_clock(adb)
            exits = new_exits(parse_exit_info(before_exit, args.package), parse_exit_info(after_exit, args.package))
            known = set(pids_immediate + pids_after + [item['pid'] for item in exits])
            novel_crashes = extract_target_crashes(after_crash, args.package, known, crash_line_keys(before_crash),
                                                   [event['eventId'] for event in before_events])
            crashes, uncertain_crashes, historical_count = partition_crash_window(novel_crashes, device_started, device_ended)
            item = {'iteration': index + 1, 'observedAtUtc': utc(), 'launchOk': launch_ok,
                    'pidsImmediatelyAfterLaunch': pids_immediate, 'pidsAfterWait': pids_after,
                    'pidAliveAfterWait': bool(pids_after) and bool(set(pids_immediate) & set(pids_after)),
                    'foregroundMatches': foreground_matches(activity, args.package),
                    'newUnexpectedExitCount': len(exits), 'newUnexpectedExits': exits,
                    'deviceWindowStartedAt': device_started.isoformat(), 'deviceWindowEndedAt': device_ended.isoformat(),
                    'newNativeCrashCount': len(crashes), 'newNativeCrashes': crashes,
                    'nativeCrashTimingUnverifiedCount': len(uncertain_crashes),
                    'nativeCrashTimingUnverified': uncertain_crashes, 'historicalCrashesOutsideWindowIgnored': historical_count}
            result['iterations'].append(item)
            write_json(output / ('iteration-' + str(index + 1) + '.json'), item)
            write_json(output / 'result.json', result)
            if not (item['launchOk'] and item['pidAliveAfterWait'] and item['foregroundMatches']
                    and not exits and not crashes and not uncertain_crashes):
                result['errorCode'] = 'NativeCrashTimingUnverified' if uncertain_crashes else 'StartupObservationFailed'
                break
    except SmokeError as error:
        result['errorCode'] = str(error)
    except (OSError, ValueError, subprocess.SubprocessError):
        result['errorCode'] = 'UnexpectedObservationFailure'
    finally:
        if remote_apk and installed_before:
            try:
                result['installedApkAfterSha256'] = adb.installed_apk_sha(remote_apk)
                result['apkPreserved'] = result['installedApkAfterSha256'] == installed_before == expected
                after_dump, _ = adb.call('shell', 'dumpsys', 'package', args.package)
                result['permissionStateAfterSha256'] = permission_fingerprint(after_dump)
                result['permissionsPreserved'] = result['permissionStateAfterSha256'] == permission_before
            except (SmokeError, OSError, subprocess.SubprocessError):
                result['preservationCheckError'] = True
        success = observed_stable(result['iterations'], args.iterations,
                                  apk_preserved=result['apkPreserved'],
                                  permissions_preserved=result['permissionsPreserved'], error=result['errorCode'])
        result.update(endedAtUtc=utc(), completedIterations=len(result['iterations']),
                      verdict='ObservedStable' if success else 'Failed', passed=bool(success))
        write_json(output / 'result.json', result)
    print(json.dumps({'verdict': result['verdict'], 'passed': result['passed'],
                      'completedIterations': result['completedIterations'],
                      'errorCode': result['errorCode'], 'crashCauseFixed': False}, ensure_ascii=False))
    return 0 if result['passed'] else 1


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--adb', required=True, help='설치된 adb.exe 절대 경로')
    parser.add_argument('--serial', required=True)
    parser.add_argument('--package', required=True)
    expected = parser.add_mutually_exclusive_group(required=True)
    expected.add_argument('--expected-apk-sha256')
    expected.add_argument('--apk', help='설치된 base APK와 정확히 같은 로컬 APK')
    parser.add_argument('--iterations', type=int, choices=(3, 4, 5), default=3)
    parser.add_argument('--wait-seconds', type=int, default=10)
    parser.add_argument('--output', required=True, help='저장소 artifacts/local 아래 존재하지 않는 새 폴더')
    try:
        return run(parser.parse_args())
    except SmokeError as error:
        print(json.dumps({'passed': False, 'verdict': 'NotVerified', 'errorCode': str(error)}, ensure_ascii=False))
        return 2
    except (OSError, ValueError):
        print(json.dumps({'passed': False, 'verdict': 'NotVerified', 'errorCode': 'PreflightUnavailable'}, ensure_ascii=False))
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
