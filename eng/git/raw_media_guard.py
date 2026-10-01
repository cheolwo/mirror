"""Block raw capture paths in the index or any outgoing Git history."""
from __future__ import annotations

import argparse
from pathlib import PurePosixPath
import re
import subprocess
import sys

VIDEO_AUDIO = frozenset('.mp4 .mov .mkv .avi .webm .m4v .mts .m2ts .3gp .mpg .mpeg .wmv .flv .lrv .insv .360 .wav .mp3 .m4a .aac .flac .ogg .aiff .aif .wma'.split())
IMAGES = frozenset('.jpg .jpeg .png .heic .heif .dng .raw .arw .cr2 .cr3 .nef .orf .rw2 .tif .tiff .webp .gif .bmp .thm'.split())
CAPTURE_DIRS = frozenset({'originals', 'footage', 'raw-footage', 'raw-captures', '촬영원본', '원본', '쇼츠용', '롱폼-주행용', '.codex-remote-attachments'})
ZERO = re.compile(r'^0+$')
OID = re.compile(r'^[0-9a-fA-F]{40}(?:[0-9a-fA-F]{24})?$')


def git(*args: str) -> bytes:
    result = subprocess.run(['git', *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError(f'Git inspection failed: {args[0]}')
    return result.stdout


def blocked(path: str, mode: str) -> bool:
    name = PurePosixPath(path.replace('\\', '/').casefold())
    if any(part in CAPTURE_DIRS for part in name.parts):
        return True
    if name.suffix in VIDEO_AUDIO:
        return True
    if name.suffix in IMAGES:
        return mode == 'delivery' or bool(re.match(r'^(dji_|img_|dsc[_0-9]|gopr|gx\d)', name.name))
    return False


def history_paths(local_oid: str, remote_oid: str | None = None) -> set[str]:
    if not OID.fullmatch(local_oid):
        raise ValueError('Invalid local object ID')
    revision = [local_oid]
    if remote_oid and not ZERO.fullmatch(remote_oid):
        if not OID.fullmatch(remote_oid):
            raise ValueError('Invalid remote object ID')
        known = subprocess.run(['git', 'cat-file', '-e', remote_oid], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if known.returncode == 0:
            revision.append('^' + remote_oid)
        # A remote boundary absent locally cannot justify skipping old history.
    paths = set()
    for line in git('-c', 'core.quotepath=false', 'rev-list', '--objects', *revision).decode('utf-8').splitlines():
        fields = line.split(' ', 1)
        if len(fields) == 2:
            paths.add(fields[1])
    # A renamed path can reuse a blob that already exists on the remote. Object
    # enumeration alone omits that blob; changed paths also catch this case,
    # including a prohibited path introduced and then deleted before HEAD.
    changed = git('-c', 'core.quotepath=false', 'log', '-m', '--format=', '--name-only', '-z', '--no-renames', '--root', *revision)
    paths.update(path.lstrip('\n') for path in changed.decode('utf-8').split('\0') if path.lstrip('\n'))
    return paths


def index_paths() -> set[str]:
    return set(git('ls-files', '-z').decode('utf-8').split('\0')) - {''}


def check(mode: str, paths: set[str]) -> int:
    found = sorted(path for path in paths if blocked(path, mode))
    if found:
        print('PUSH BLOCKED: raw capture/media paths were found in Git history.', file=sys.stderr)
        for path in found[:20]:
            print('  ' + path, file=sys.stderr)
        print('Local files are preserved. Remove media from outgoing history before retrying.', file=sys.stderr)
        return 1
    print('Raw media guard: passed (no prohibited capture/media paths).')
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--mode', choices=['delivery', 'mirror'], required=True)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument('--audit-head', action='store_true')
    action.add_argument('--audit-index', action='store_true')
    action.add_argument('--pre-push', action='store_true')
    args = parser.parse_args(argv)
    try:
        if args.audit_index:
            paths = index_paths()
        elif args.audit_head:
            paths = history_paths(git('rev-parse', 'HEAD').decode().strip())
        else:
            paths = set()
            for line in sys.stdin:
                fields = line.split()
                if not fields:
                    continue
                if len(fields) != 4:
                    raise ValueError('Invalid pre-push input')
                _, local_oid, _, remote_oid = fields
                if ZERO.fullmatch(local_oid):
                    continue  # Deleting a remote ref uploads no footage.
                paths.update(history_paths(local_oid, remote_oid))
        return check(args.mode, paths)
    except (RuntimeError, ValueError, UnicodeError, OSError) as exc:
        print(f'PUSH BLOCKED: media inspection did not complete ({exc}).', file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
