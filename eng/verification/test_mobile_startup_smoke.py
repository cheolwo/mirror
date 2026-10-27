"""합성 로그/경로만 검사한다. adb나 휴대폰을 실행하지 않는다."""
import importlib.util
import datetime as dt
from pathlib import Path
import tempfile
import stat
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('mobile_startup_smoke', Path(__file__).with_name('mobile-startup-smoke.py'))
smoke = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(smoke)
PACKAGE = 'com.companyname.ssalddelapp.cooperationreview'


def exit_dump(timestamp, pid=100, reason=5, status=11, package=PACKAGE):
    return f'''ApplicationExitInfo #0:
 timestamp={timestamp} pid={pid} realUid=1000
 process={package} reason={reason} (APP CRASH(NATIVE)) subreason=0 (UNKNOWN) status={status}
 description=account-secret state=71 bytes trace=null'''


def native_block(package=PACKAGE, blamed_pid=100, dumper_pid=900):
    return f'''10-25 13:00:01.111 {dumper_pid} {dumper_pid} F DEBUG : *** *** *** *** *** *** *** *** *** *** *** ***
10-25 13:00:01.112 {dumper_pid} {dumper_pid} F DEBUG : pid: {blamed_pid}, tid: 120, name: .NET TP Worker  >>> {package} <<<
10-25 13:00:01.113 {dumper_pid} {dumper_pid} F DEBUG : signal 11 (SIGSEGV), code 2 (SEGV_ACCERR), fault addr 0x1234
10-25 13:00:01.114 {dumper_pid} {dumper_pid} F DEBUG : account-secret=do-not-copy
10-25 13:00:01.115 {dumper_pid} {dumper_pid} F DEBUG :       #00 pc 0000000000012345 /data/app/private-path/lib/arm64/libmonosgen-2.0.so (mono_sample+12) (BuildId: abc)
10-25 13:00:01.116 {dumper_pid} {dumper_pid} F DEBUG :       #01 pc 0000000000023456 /apex/com.android.runtime/lib64/bionic/libc.so (__pthread_start+44)
'''


class StartupSmokeTests(unittest.TestCase):
    def test_target_whitelist_and_exact_serial(self):
        smoke.validate_target('TEST123', PACKAGE, 3)
        for serial in ('x; reboot', 'a b', '-s', 'x\nother'):
            with self.subTest(serial=serial), self.assertRaises(smoke.SmokeError):
                smoke.validate_target(serial, PACKAGE, 3)

    def test_other_package_and_unbounded_iterations_rejected(self):
        for package, count in [('com.bank.app', 3), (PACKAGE + ';rm', 3), (PACKAGE, 1), (PACKAGE, 6)]:
            with self.subTest(package=package, count=count), self.assertRaises(smoke.SmokeError):
                smoke.validate_target('TEST123', package, count)

    def test_old_exit_info_is_not_new_failure(self):
        old = smoke.parse_exit_info(exit_dump('2026-10-24 18:24:40.613'), PACKAGE)
        self.assertEqual([], smoke.new_exits(old, old))

    def test_only_new_target_exit_is_kept(self):
        old = smoke.parse_exit_info(exit_dump('2026-10-24 18:24:40.613'), PACKAGE)
        raw = exit_dump('2026-10-25 13:00:02.000', 200) + exit_dump('2026-10-24 18:24:40.613')
        raw += exit_dump('2026-10-25 13:00:03.000', 300, package='com.google.android.webview:sandboxed_process0')
        events = smoke.new_exits(old, smoke.parse_exit_info(raw, PACKAGE))
        self.assertEqual([200], [event['pid'] for event in events])
        self.assertNotIn('secret', str(events))

    def test_crash_dump_other_pid_backtrace_is_preserved(self):
        events = smoke.extract_target_crashes(native_block(), PACKAGE, [100])
        self.assertEqual(1, len(events))
        self.assertEqual(100, events[0]['pid'])
        self.assertEqual(2, len(events[0]['frames']))
        self.assertEqual('libmonosgen-2.0.so', events[0]['frames'][0]['module'])
        self.assertNotIn('private-path', str(events))
        self.assertNotIn('account-secret', str(events))

    def test_other_app_native_block_is_not_copied(self):
        raw = native_block('com.bank.app', 200) + native_block()
        events = smoke.extract_target_crashes(raw, PACKAGE, [100])
        self.assertEqual(1, len(events))
        self.assertNotIn('com.bank', str(events))

    def test_interleaved_other_dumper_frames_are_not_copied(self):
        raw = native_block() + '10-25 13:00:01.119 901 901 F DEBUG : #02 pc 0000000000034567 /data/app/other-account/lib/libbank.so (secret_account+3)\n'
        events = smoke.extract_target_crashes(raw, PACKAGE, [100])
        self.assertEqual(2, len(events[0]['frames']))
        self.assertNotIn('libbank', str(events))

    def test_old_native_crash_block_is_ignored(self):
        raw = native_block()
        self.assertEqual([], smoke.extract_target_crashes(raw, PACKAGE, [100], smoke.crash_line_keys(raw)))

    def test_crlf_baseline_and_parsed_event_ids_ignore_old_crash(self):
        before = native_block().replace('\n', '\r\n')
        baseline = smoke.extract_target_crashes(before, PACKAGE)
        self.assertEqual(1, len(baseline))
        self.assertEqual([], smoke.extract_target_crashes(before, PACKAGE, [100], smoke.crash_line_keys(before)))
        # logger의 공백이 바뀌어도 이미 파싱한 같은 사건의 identity로 제외한다.
        reformatted = before.replace('900 900', ' 900  900')
        self.assertEqual([], smoke.extract_target_crashes(reformatted, PACKAGE, [100], baseline_event_ids=[baseline[0]['eventId']]))

    def test_yesterday_backtrace_is_not_today_failure(self):
        events = smoke.extract_target_crashes(native_block().replace('10-25', '10-24'), PACKAGE, [100])
        start = dt.datetime.fromisoformat('2026-10-25T13:00:00+09:00')
        fresh, uncertain, historical = smoke.partition_crash_window(events, start, start + dt.timedelta(seconds=20))
        self.assertEqual([], fresh)
        self.assertEqual([], uncertain)
        self.assertEqual(1, historical)

    def test_year_boundary_assigns_previous_december_to_current_window(self):
        events = smoke.extract_target_crashes(native_block().replace('10-25 13:00:01', '12-31 23:59:59'), PACKAGE, [100])
        start = dt.datetime.fromisoformat('2026-12-31T23:59:58+09:00')
        end = dt.datetime.fromisoformat('2027-01-01T00:00:10+09:00')
        fresh, uncertain, historical = smoke.partition_crash_window(events, start, end)
        self.assertEqual(1, len(fresh))
        self.assertTrue(fresh[0]['deviceObservedAt'].startswith('2026-12-31'))
        self.assertEqual([], uncertain)
        self.assertEqual(0, historical)

    def test_timestamp_missing_is_unverified_not_stable(self):
        events = [{'eventId': 'test-only', 'timestamp': None, 'frames': []}]
        start = dt.datetime.fromisoformat('2026-10-25T13:00:00+09:00')
        fresh, uncertain, _ = smoke.partition_crash_window(events, start, start)
        self.assertEqual([], fresh)
        self.assertEqual(1, len(uncertain))
        good = dict(launchOk=True, pidAliveAfterWait=True, foregroundMatches=True,
                    newUnexpectedExitCount=0, newNativeCrashCount=0, nativeCrashTimingUnverifiedCount=1)
        self.assertFalse(smoke.observed_stable([good] * 3, 3, apk_preserved=True, permissions_preserved=True))

    def test_standalone_fatal_requires_target_pid(self):
        raw = '10-25 13:00:01.000 100 120 F libc : Fatal signal 11 (SIGSEGV), code 2, fault addr 0x1234 in tid 120 (.NET TP Worker), pid 100 (shortname)'
        self.assertEqual([], smoke.extract_target_crashes(raw, PACKAGE, [200]))
        self.assertEqual(1, len(smoke.extract_target_crashes(raw, PACKAGE, [100])))
        self.assertEqual([], smoke.extract_target_crashes(raw, PACKAGE, [100], smoke.crash_line_keys(raw)))

    def test_foreground_exact_package_only(self):
        self.assertTrue(smoke.foreground_matches(f'topResumedActivity=ActivityRecord{{aa u0 {PACKAGE}/.MainActivity t33}}', PACKAGE))
        self.assertFalse(smoke.foreground_matches(f'mResumedActivity: ActivityRecord{{aa u0 {PACKAGE}.other/.MainActivity t33}}', PACKAGE))
        self.assertFalse(smoke.foreground_matches(f'baseActivity=ActivityRecord{{aa u0 {PACKAGE}/.MainActivity t33}}', PACKAGE))

    def test_current_other_top_overrides_stale_target_resumed(self):
        raw = f'mResumedActivity: ActivityRecord{{aa u0 {PACKAGE}/.MainActivity t33}}\n'
        raw += 'topResumedActivity=ActivityRecord{bb u0 com.other.app/.MainActivity t34}\n'
        self.assertFalse(smoke.foreground_matches(raw, PACKAGE))

    def test_ambiguous_multiple_tops_cannot_pass(self):
        raw = f'topResumedActivity=ActivityRecord{{aa u0 {PACKAGE}/.MainActivity t33}}\n'
        self.assertFalse(smoke.foreground_matches(raw + 'topResumedActivity=null\n', PACKAGE))
        self.assertFalse(smoke.foreground_matches(raw + 'topResumedActivity=ActivityRecord{bb u0 com.other.app/.MainActivity t34}\n', PACKAGE))

    def test_path_injection_and_unc_are_rejected(self):
        for path in ('https://example/a.apk', '//server/share/a.apk', '\\\\server\\share\\a.apk', 'C:\\tmp\\a.apk:stream', 'C:\\tmp\\a.apk\nother', 'C:\\tmp\\a.apk; reboot', '../a.apk'):
            with self.subTest(path=path), self.assertRaises(smoke.SmokeError):
                smoke.checked_local_path(path, suffix='.apk', existing=True)

    def test_output_must_be_inside_artifacts(self):
        with tempfile.TemporaryDirectory() as root:
            base = Path(root)
            self.assertEqual(base / 'new', smoke.checked_local_path(base / 'new', root=base))
            with self.assertRaises(smoke.SmokeError):
                smoke.checked_local_path(base.parent / 'outside', root=base)

    def test_symlink_is_rejected(self):
        with tempfile.TemporaryDirectory() as root:
            base = Path(root); (base / 'file.apk').write_bytes(b'apk')
            try:
                (base / 'link.apk').symlink_to(base / 'file.apk')
            except OSError:
                self.skipTest('host symlink privilege unavailable')
            with self.assertRaises(smoke.SmokeError):
                smoke.checked_local_path(base / 'link.apk', suffix='.apk', existing=True)

    def test_windows_reparse_attribute_is_rejected_without_symlink_privilege(self):
        metadata = SimpleNamespace(st_mode=stat.S_IFREG, st_file_attributes=0x400)
        with patch.object(Path, 'lstat', return_value=metadata), self.assertRaises(smoke.SmokeError):
            smoke.checked_local_path(Path(sys.executable).absolute(), existing=True)

    def test_remote_path_injection_rejected(self):
        for path in ('/data/app/a/base.apk;reboot', '/data/app/../base.apk', '/sdcard/base.apk', '/data/app/a/split.apk'):
            self.assertFalse(bool(smoke.REMOTE_APK.fullmatch(path)) and '..' not in path.split('/'))

    def test_permission_hash_omits_other_package_fields(self):
        raw = 'account=user-secret\n android.permission.CAMERA: granted=true, flags=[USER_SET]\n'
        changed = raw.replace('true', 'false')
        self.assertNotEqual(smoke.permission_fingerprint(raw), smoke.permission_fingerprint(changed))
        self.assertEqual(64, len(smoke.permission_fingerprint(raw)))

    def test_failed_or_incomplete_iterations_cannot_pass(self):
        good = dict(launchOk=True, pidAliveAfterWait=True, foregroundMatches=True, newUnexpectedExitCount=0, newNativeCrashCount=0)
        self.assertTrue(smoke.observed_stable([good] * 3, 3, apk_preserved=True, permissions_preserved=True))
        self.assertFalse(smoke.observed_stable([good], 3, apk_preserved=True, permissions_preserved=True))
        for field in ('launchOk', 'pidAliveAfterWait', 'foregroundMatches'):
            bad = dict(good, **{field: False})
            self.assertFalse(smoke.observed_stable([good, bad, good], 3, apk_preserved=True, permissions_preserved=True))
        for field in ('newUnexpectedExitCount', 'newNativeCrashCount'):
            self.assertFalse(smoke.observed_stable([dict(good, **{field: 1})] * 3, 3, apk_preserved=True, permissions_preserved=True))
        self.assertFalse(smoke.observed_stable([good] * 3, 3, apk_preserved=False, permissions_preserved=True))
        self.assertFalse(smoke.observed_stable([good] * 3, 3, apk_preserved=True, permissions_preserved=False))
        self.assertFalse(smoke.observed_stable([good] * 3, 3, apk_preserved=True, permissions_preserved=True, error='AnyError'))

    def test_fake_run_observes_three_starts_without_mutating_commands(self):
        self._fake_run(mismatch=False)

    def test_apk_mismatch_blocks_force_stop_and_launch(self):
        self._fake_run(mismatch=True)

    def _fake_run(self, mismatch):
        class FakeAdb:
            def __init__(self):
                self.commands, self.running = [], False
            def installed_apk_sha(self, path):
                return ('b' if mismatch else 'a') * 64
            def call(self, *args, **kwargs):
                self.commands.append(args)
                if args == ('get-state',): return 'device\n', 0
                if args[:2] == ('shell', 'date'): return '2026-10-25T13:00:00+0900\n', 0
                if args[:3] == ('shell', 'pm', 'path'): return 'package:/data/app/valid/base.apk\n', 0
                if args[:3] == ('shell', 'dumpsys', 'package'): return 'android.permission.INTERNET: granted=true\n', 0
                if args[:4] == ('shell', 'cmd', 'package', 'resolve-activity'): return PACKAGE + '/.MainActivity\n', 0
                if args[:4] == ('shell', 'dumpsys', 'activity', 'exit-info'): return exit_dump('2026-10-24 18:24:40.613'), 0
                if args[:3] == ('shell', 'am', 'force-stop'): self.running = False; return '', 0
                if args[:2] == ('shell', 'pidof'): return ('100\n', 0) if self.running else ('', 1)
                if args[:3] == ('shell', 'am', 'start'): self.running = True; return 'Status: ok\n', 0
                if args[:1] == ('logcat',): return '', 0
                if args[:4] == ('shell', 'dumpsys', 'activity', 'activities'): return f'topResumedActivity=ActivityRecord{{ab u0 {PACKAGE}/.MainActivity t5}}\n', 0
                raise AssertionError(args)
        with tempfile.TemporaryDirectory() as root:
            base = Path(root); adb_path = base / 'adb.exe'; adb_path.write_bytes(b'not executed')
            output = base / 'fresh'
            args = SimpleNamespace(adb=str(adb_path), serial='TEST123', package=PACKAGE,
                                   iterations=3, wait_seconds=3, output=str(output),
                                   expected_apk_sha256='a' * 64, apk=None)
            fake = FakeAdb()
            with patch.object(smoke, 'ARTIFACT_ROOT', base), patch.object(smoke, 'Adb', return_value=fake), patch.object(smoke.time, 'sleep'):
                code = smoke.run(args)
            import json
            result = json.loads((output / 'result.json').read_text(encoding='utf-8'))
            stops = [cmd for cmd in fake.commands if cmd[:3] == ('shell', 'am', 'force-stop')]
            self.assertEqual(0 if mismatch else 3, len(stops))
            self.assertEqual(1 if mismatch else 0, code)
            self.assertEqual(not mismatch, result['passed'])
            self.assertFalse(result['crashCauseFixed'])
            self.assertFalse(result['dataClearCommandExecuted'])
            for command in fake.commands:
                self.assertNotIn('clear', command)
                self.assertNotIn('install', command)
                self.assertNotIn('grant', command)


if __name__ == '__main__':
    unittest.main()
