"""Block raw capture paths in the index or any outgoing Git history."""
from __future__ import annotations

import argparse
import hashlib
from pathlib import PurePosixPath
import re
import subprocess
import sys

VIDEO_AUDIO = frozenset('.mp4 .mov .mkv .avi .webm .m4v .mts .m2ts .3gp .mpg .mpeg .wmv .flv .lrv .insv .360 .wav .mp3 .m4a .aac .flac .ogg .aiff .aif .wma'.split())
IMAGES = frozenset('.jpg .jpeg .png .heic .heif .dng .raw .arw .cr2 .cr3 .nef .orf .rw2 .tif .tiff .webp .gif .bmp .thm'.split())
CAPTURE_DIRS = frozenset({'originals', 'footage', 'raw-footage', 'raw-captures', '촬영원본', '원본', '쇼츠용', '롱폼-주행용', '.codex-remote-attachments'})
ZERO = re.compile(r'^0+$')
OID = re.compile(r'^[0-9a-fA-F]{40}(?:[0-9a-fA-F]{24})?$')
# Product notification tones generated for r35/r43 and the two manually reviewed
# r19 synthesized voice assets, never captured audio. Every outgoing version
# must match its exact path and one of these explicitly reviewed SHA-256 values.
MIRROR_NOTIFICATION_ASSETS = {
    'FDriverApp/Platforms/Android/Resources/raw/ssalddel_delivery.wav': frozenset({
        '19846118acc73172933e20068874138c799fbf802701fc1027fedaa07d9f2e61',
        'd0ed818d6e9d6449d7224f849d5dc71ea88f3d2a4b788be1d5aa3828765d577b',
    }),
    'RestaurantDeskApp/Platforms/Android/Resources/raw/ssalddel_order.wav': frozenset({
        'de4c10c92e28d1a0ba4a085713f358fcaea938cda96af105b9894aecdd65dd28',
    }),
    'SsalddelApp/Platforms/Android/Resources/raw/ssalddel_member_delivery.wav': frozenset({
        '19846118acc73172933e20068874138c799fbf802701fc1027fedaa07d9f2e61',
    }),
    'DriverApp/Platforms/Android/Resources/raw/hongdal_dispatch.wav': frozenset({
        '6e0da9aaa1b36521b71a7ce0dff5412edd315cc6531d96c308e385a4c4cbbab2',
    }),
}


def git(*args: str) -> bytes:
    result = subprocess.run(['git', *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError(f'Git inspection failed: {args[0]}')
    return result.stdout


def blocked(path: str, mode: str) -> bool:
    if any(ord(char) < 32 or ord(char) == 127 for char in path):
        return True  # Ambiguous/control-character Git paths fail closed.
    name = PurePosixPath(path.replace('\\', '/').casefold())
    if any(part in CAPTURE_DIRS for part in name.parts):
        return True
    if name.suffix in VIDEO_AUDIO:
        return True
    if name.suffix in IMAGES:
        return mode == 'delivery' or bool(re.match(r'^(dji_|img_|dsc[_0-9]|gopr|gx\d)', name.name))
    return False


def history_revision(local_oid: str, remote_oid: str | None = None) -> list[str]:
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
    return revision


def history_paths(local_oid: str, remote_oid: str | None = None) -> set[str]:
    revision = history_revision(local_oid, remote_oid)
    paths = set()
    # NUL-delimited diff-tree has no log headers mixed into paths. Never strip
    # path characters: a leading LF must not become an allowed resource path.
    # Per-commit changes include reused blobs, merge parents and later deletion.
    for commit in git('rev-list', *revision).decode('ascii').splitlines():
        changed = git('diff-tree', '--root', '-m', '--no-commit-id', '--name-only', '-z', '-r', '--no-renames', commit)
        paths.update(path for path in changed.decode('utf-8').split('\0') if path)
    return paths


def index_paths() -> set[str]:
    return set(git('ls-files', '-z').decode('utf-8').split('\0')) - {''}


def notification_asset_blobs(local_oid: str, remote_oid: str | None = None) -> dict[str, set[str]]:
    """Inspect the exact resource paths in every outgoing commit tree.

    Object enumeration alone can omit a reused blob or only give one of its
    paths. Tree snapshots also retain an altered asset later restored/deleted.
    """
    revision = history_revision(local_oid, remote_oid)
    trees = set(git('log', '--format=%T', *revision).decode('ascii').splitlines())
    result: dict[str, set[str]] = {}
    for tree in trees:
        for entry in git('ls-tree', '-r', '-z', '--full-tree', tree, '--', *MIRROR_NOTIFICATION_ASSETS).split(b'\0'):
            if not entry:
                continue
            metadata, raw_path = entry.split(b'\t', 1)
            mode, kind, raw_oid = metadata.decode('ascii').split()
            path = raw_path.decode('utf-8')
            if path not in MIRROR_NOTIFICATION_ASSETS or mode != '100644' or kind != 'blob':
                raise ValueError('Unexpected notification asset tree entry')
            result.setdefault(path, set()).add(raw_oid)
    return result


def indexed_notification_asset_blobs() -> dict[str, set[str]]:
    result: dict[str, set[str]] = {}
    for entry in git('ls-files', '--stage', '-z', '--', *MIRROR_NOTIFICATION_ASSETS).split(b'\0'):
        if not entry:
            continue
        metadata, raw_path = entry.split(b'\t', 1)
        mode, oid, stage = metadata.decode('ascii').split()
        path = raw_path.decode('utf-8')
        if path not in MIRROR_NOTIFICATION_ASSETS or mode != '100644' or stage != '0':
            raise ValueError('Unexpected notification asset index entry')
        result.setdefault(path, set()).add(oid)
    return result


def check(mode: str, paths: set[str], asset_blobs: dict[str, set[str]] | None = None) -> int:
    hashes: dict[str, str] = {}
    found = []
    for path in sorted(paths):
        if not blocked(path, mode):
            continue
        expected = MIRROR_NOTIFICATION_ASSETS.get(path) if mode == 'mirror' else None
        versions = (asset_blobs or {}).get(path, set())
        allowed = bool(expected and versions)
        for oid in versions if allowed else ():
            if not OID.fullmatch(oid):
                raise ValueError('Invalid notification asset object ID')
            if oid not in hashes:
                hashes[oid] = hashlib.sha256(git('cat-file', 'blob', oid)).hexdigest()
            if hashes[oid] not in expected:
                allowed = False
                break
        if not allowed:
            found.append(path)
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
        assets: dict[str, set[str]] = {}
        if args.audit_index:
            paths = index_paths()
            if args.mode == 'mirror':
                assets = indexed_notification_asset_blobs()
        elif args.audit_head:
            head = git('rev-parse', 'HEAD').decode().strip()
            paths = history_paths(head)
            if args.mode == 'mirror':
                assets = notification_asset_blobs(head)
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
                if args.mode == 'mirror':
                    for path, versions in notification_asset_blobs(local_oid, remote_oid).items():
                        assets.setdefault(path, set()).update(versions)
        return check(args.mode, paths, assets)
    except (RuntimeError, ValueError, UnicodeError, OSError) as exc:
        print(f'PUSH BLOCKED: media inspection did not complete ({exc}).', file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
