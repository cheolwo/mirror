import contextlib
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('raw_media_guard', Path(__file__).with_name('raw_media_guard.py'))
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)


class RawMediaGuardTests(unittest.TestCase):
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
