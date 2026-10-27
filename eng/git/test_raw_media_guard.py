import contextlib
import importlib.util
import io
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location('raw_media_guard', Path(__file__).with_name('raw_media_guard.py'))
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)
SOURCE_REPO = Path(__file__).resolve().parents[2]


@contextlib.contextmanager
def fixture_repo():
    with tempfile.TemporaryDirectory() as directory:
        repo = Path(directory)
        def git(*args):
            return subprocess.check_output(['git', *args], cwd=repo, stderr=subprocess.DEVNULL).decode().strip()
        git('init')
        git('config', 'user.name', 'Media guard test')
        git('config', 'user.email', 'test@example.invalid')
        (repo / 'readme.md').write_text('fixture', encoding='utf-8')
        git('add', '.')
        git('commit', '-m', 'clean')
        with contextlib.chdir(repo):
            yield repo, git


def put(repo, path, data):
    target = repo / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)


def tone_bytes(path):
    # Read the committed product resource, not an unstaged working-copy file.
    return subprocess.check_output(['git', 'show', 'HEAD:' + path], cwd=SOURCE_REPO)


def quiet_check(mode, paths, assets=None):
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        return guard.check(mode, paths, assets)


class RawMediaGuardTests(unittest.TestCase):
    def test_leading_lf_path_cannot_borrow_approved_tone_exception(self):
        path = next(iter(guard.MIRROR_NOTIFICATION_ASSETS))
        disguised = '\n' + path
        with fixture_repo() as (repo, git):
            base = git('rev-parse', 'HEAD')
            put(repo, path, tone_bytes(path))
            git('add', '.')
            # Windows cannot create this filename. Git objects/index still can
            # contain it, so construct the hostile committed path directly.
            git('config', 'core.protectNTFS', 'false')
            bad_blob = subprocess.check_output(['git', 'hash-object', '-w', '--stdin'], cwd=repo, input=b'captured voice').decode().strip()
            git('update-index', '--add', '--cacheinfo', '100644,' + bad_blob + ',' + disguised)
            git('commit', '-m', 'approved resource plus disguised capture')
            first = git('rev-parse', 'HEAD')
            paths = guard.history_paths(first, base)
            self.assertIn(disguised, paths)
            self.assertIn(path, paths)
            self.assertEqual(quiet_check('mirror', paths, guard.notification_asset_blobs(first, base)), 1)
            self.assertEqual(quiet_check('mirror', guard.index_paths(), guard.indexed_notification_asset_blobs()), 1)
            git('update-index', '--force-remove', disguised)
            git('commit', '-m', 'delete disguised capture')
            head = git('rev-parse', 'HEAD')
            self.assertIn(disguised, guard.history_paths(head, base))
            self.assertEqual(quiet_check('mirror', guard.history_paths(head, base), guard.notification_asset_blobs(head, base)), 1)

    def test_control_characters_fail_closed_even_after_extension(self):
        for path in ['recording.wav\n', 'clip.mp4\t', '\x01notes.txt', 'notes\x7f.txt']:
            for mode in ['mirror', 'delivery']:
                with self.subTest(path=repr(path), mode=mode):
                    self.assertTrue(guard.blocked(path, mode))

    def test_exact_product_tones_allowed_in_mirror_only(self):
        with fixture_repo() as (repo, git):
            for path in guard.MIRROR_NOTIFICATION_ASSETS:
                put(repo, path, tone_bytes(path))
            git('add', '.')
            assets = guard.indexed_notification_asset_blobs()
            self.assertEqual(quiet_check('mirror', guard.index_paths(), assets), 0)
            self.assertEqual(quiet_check('delivery', guard.index_paths(), assets), 1)
            # A path-only call never grants the content exception.
            self.assertEqual(quiet_check('mirror', guard.index_paths()), 1)

    def test_changed_index_tone_and_same_bytes_at_other_path_stay_blocked(self):
        path = next(iter(guard.MIRROR_NOTIFICATION_ASSETS))
        with fixture_repo() as (repo, git):
            original = tone_bytes(path)
            put(repo, path, original)
            git('add', '.')
            put(repo, path, b'captured audio')
            # Index auditing checks the staged bytes, not the working copy.
            self.assertEqual(quiet_check('mirror', guard.index_paths(), guard.indexed_notification_asset_blobs()), 0)
            git('add', '.')
            self.assertEqual(quiet_check('mirror', guard.index_paths(), guard.indexed_notification_asset_blobs()), 1)
            put(repo, path, original)
            put(repo, 'other/ssalddel_delivery.wav', original)
            git('add', '.')
            self.assertEqual(quiet_check('mirror', guard.index_paths(), guard.indexed_notification_asset_blobs()), 1)

    def test_changed_historical_tone_cannot_hide_behind_restored_tone(self):
        path = next(iter(guard.MIRROR_NOTIFICATION_ASSETS))
        with fixture_repo() as (repo, git):
            base = git('rev-parse', 'HEAD')
            put(repo, path, b'captured voice')
            git('add', '.')
            git('commit', '-m', 'unapproved content')
            put(repo, path, tone_bytes(path))
            git('add', '.')
            git('commit', '-m', 'restore exact tone')
            head = git('rev-parse', 'HEAD')
            assets = guard.notification_asset_blobs(head, base)
            self.assertEqual(len(assets[path]), 2)
            self.assertEqual(quiet_check('mirror', guard.history_paths(head, base), assets), 1)

    def test_changed_historical_tone_cannot_hide_behind_deletion(self):
        path = next(iter(guard.MIRROR_NOTIFICATION_ASSETS))
        with fixture_repo() as (repo, git):
            base = git('rev-parse', 'HEAD')
            put(repo, path, b'captured voice')
            git('add', '.')
            git('commit', '-m', 'unapproved content')
            git('rm', path)
            git('commit', '-m', 'remove content')
            head = git('rev-parse', 'HEAD')
            for remote in [base, '0' * 40, 'a' * 40]:
                with self.subTest(remote=remote):
                    self.assertEqual(quiet_check('mirror', guard.history_paths(head, remote), guard.notification_asset_blobs(head, remote)), 1)

    def test_reused_remote_blob_renamed_into_allowed_path_is_checked(self):
        path = next(iter(guard.MIRROR_NOTIFICATION_ASSETS))
        with fixture_repo() as (repo, git):
            put(repo, 'source.dat', b'captured voice')
            git('add', '.')
            git('commit', '-m', 'remote existing blob')
            base = git('rev-parse', 'HEAD')
            (repo / path).parent.mkdir(parents=True, exist_ok=True)
            git('mv', 'source.dat', path)
            git('commit', '-m', 'reuse blob at resource path')
            head = git('rev-parse', 'HEAD')
            self.assertEqual(quiet_check('mirror', guard.history_paths(head, base), guard.notification_asset_blobs(head, base)), 1)

    def test_good_tone_renamed_as_other_audio_stays_blocked(self):
        path = next(iter(guard.MIRROR_NOTIFICATION_ASSETS))
        with fixture_repo() as (repo, git):
            put(repo, path, tone_bytes(path))
            git('add', '.')
            git('commit', '-m', 'remote approved resource')
            base = git('rev-parse', 'HEAD')
            git('mv', path, 'recording.wav')
            git('commit', '-m', 'reused blob at other path')
            head = git('rev-parse', 'HEAD')
            self.assertEqual(quiet_check('mirror', guard.history_paths(head, base), guard.notification_asset_blobs(head, base)), 1)

    def test_unchanged_resource_in_outgoing_commit_is_verified(self):
        path = next(iter(guard.MIRROR_NOTIFICATION_ASSETS))
        with fixture_repo() as (repo, git):
            base = git('rev-parse', 'HEAD')
            put(repo, path, tone_bytes(path))
            git('add', '.')
            git('commit', '-m', 'approved tone')
            put(repo, 'readme.md', b'next state')
            git('add', '.')
            git('commit', '-m', 'unrelated change')
            head = git('rev-parse', 'HEAD')
            self.assertEqual(quiet_check('mirror', guard.history_paths(head, base), guard.notification_asset_blobs(head, base)), 0)

    def test_multiple_push_refs_do_not_overwrite_unapproved_history(self):
        path = next(iter(guard.MIRROR_NOTIFICATION_ASSETS))
        with fixture_repo() as (repo, git):
            base = git('rev-parse', 'HEAD')
            put(repo, path, b'captured voice')
            git('add', '.')
            git('commit', '-m', 'unapproved tone')
            bad = git('rev-parse', 'HEAD')
            git('checkout', '-b', 'good', base)
            put(repo, path, tone_bytes(path))
            git('add', '.')
            git('commit', '-m', 'approved tone')
            good = git('rev-parse', 'HEAD')
            stdin = f'refs/heads/bad {bad} refs/heads/bad {base}\nrefs/heads/good {good} refs/heads/good {base}\n'
            with mock.patch.object(guard.sys, 'stdin', io.StringIO(stdin)), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(guard.main(['--mode', 'mirror', '--pre-push']), 1)

    def test_mixed_case_and_camera_formats_are_blocked(self):
        for path in ['clip.Mp4', 'photo.JpEg', 'photo.HEIC', 'sound.WAV', 'clip.LRV', 'DJI_clip.png']:
            with self.subTest(path=path):
                self.assertTrue(guard.blocked(path, 'delivery'))

    def test_capture_folders_block_renamed_files(self):
        for path in ['day/originals/renamed.bin', '원본/note.txt', '.codex-remote-attachments/data.json', 'day/FOOTAGE/file.dat']:
            self.assertTrue(guard.blocked(path, 'mirror'))

    def test_mirror_keeps_product_images_but_not_camera_images(self):
        self.assertFalse(guard.blocked('docs/assets/changes/menu-list.png', 'mirror'))
        self.assertTrue(guard.blocked('images/IMG_20261001.JPG', 'mirror'))
        self.assertTrue(guard.blocked('movie.webm', 'mirror'))
        self.assertFalse(guard.blocked('models/scene.cs', 'delivery'))

    def test_deleted_media_still_blocks_and_unknown_remote_checks_history(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            def git(*args):
                return subprocess.check_output(['git', *args], cwd=repo, stderr=subprocess.DEVNULL).decode().strip()
            git('init')
            git('config', 'user.name', 'Media guard test')
            git('config', 'user.email', 'test@example.invalid')
            (repo / 'readme.md').write_text('fixture', encoding='utf-8')
            git('add', '.'); git('commit', '-m', 'clean')
            base = git('rev-parse', 'HEAD')
            # Same blob as the remote readme: the path change must still block.
            (repo / 'clip.MP4').write_bytes(b'fixture')
            git('add', '.'); git('commit', '-m', 'media')
            git('rm', 'clip.MP4'); git('commit', '-m', 'remove')
            head = git('rev-parse', 'HEAD')
            with contextlib.chdir(repo):
                self.assertNotIn('clip.MP4', guard.index_paths())
                self.assertIn('clip.MP4', guard.history_paths(head, base))
                self.assertIn('clip.MP4', guard.history_paths(head, '0' * 40))
                self.assertIn('clip.MP4', guard.history_paths(head, 'a' * 40))
                self.assertEqual(guard.history_paths(head, head), set())


if __name__ == '__main__':
    unittest.main()
