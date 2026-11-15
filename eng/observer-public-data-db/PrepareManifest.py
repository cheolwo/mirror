"""r159의 고정된 두 준비 결과를 검사·합성한다. DB·네트워크 접근 없음."""
import argparse
import hashlib
import json
from pathlib import Path

FIELDS = "kind sourceId datasetId sourceVersion dataRevision collectedAtUtc evidenceAsOfUtc rawPath rawSha256 contentType officialUrl recordsPath recordsSha256".split()


def build(root: Path, db_storage=False):
    root = root.resolve()
    folder = root / "artifacts/local/public-data-db-20261113"
    parts = [json.loads((folder / name).read_text(encoding="utf-8"))
             for name in ("catalog-manifest.partial.json", "manifest.partial.json")]
    entries = [{key: entry[key] for key in FIELDS} for part in parts for entry in part["entries"]]
    if len(entries) != 44 or sum(e["kind"] == "observations" for e in entries) != 7:
        raise ValueError("ExpectedFixedR159Entries")
    for entry in entries:
        for path_key, hash_key in (("rawPath", "rawSha256"), ("recordsPath", "recordsSha256")):
            if entry[path_key] is None:
                continue
            path = (root / entry[path_key]).resolve()
            if not path.is_relative_to(root / "artifacts/local"):
                raise ValueError("ArtifactPathRequired")
            if hashlib.sha256(path.read_bytes()).hexdigest() != entry[hash_key]:
                raise ValueError("PreparedFileHashMismatch")
    name = "manifest.json"
    if db_storage:
        proof = json.loads((folder / "already-stored-verified.json").read_text(encoding="utf-8"))
        full_body = (json.dumps({"schemaVersion": "observer-public-data-db.v1", "entries": entries},
                                ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        if proof["manifestSha256"] != hashlib.sha256(full_body).hexdigest():
            raise ValueError("ExistingProofManifestMismatch")
        if proof["status"] != "verified" or proof["verifiedRecordCount"] != 12 or proof["verifiedRawCount"] != 3:
            raise ValueError("ExistingMarineVerificationRequired")
        existing = {(b["sourceId"], b["datasetId"], b["rawSha256"]) for b in proof["bindings"]}
        if len(existing) != 3 or any(b["semanticMatch"] is not True for b in proof["bindings"]):
            raise ValueError("ExistingMarineSemanticMatchRequired")
        for binding in proof["bindings"]:
            matches = [e for e in entries if (e["sourceId"], e["datasetId"], e["rawSha256"]) ==
                       (binding["sourceId"], binding["datasetId"], binding["rawSha256"])]
            if len(matches) != 1 or any(matches[0][key] != binding[proof_key] for key, proof_key in
                                       (("rawPath", "rawPath"), ("recordsPath", "preparedRecordsPath"),
                                        ("recordsSha256", "preparedRecordsSha256"))):
                raise ValueError("ExistingProofPreparedBindingMismatch")
        retained = [e for e in entries if (e["sourceId"], e["datasetId"], e["rawSha256"]) not in existing]
        if len(retained) != 41 or any(e["sourceId"] != "kcs" for e in entries if e not in retained):
            raise ValueError("ExpectedOnlyThreeVerifiedMarineExclusions")
        entries, name = retained, "manifest.db-storage.json"
    body = (json.dumps({"schemaVersion": "observer-public-data-db.v1", "entries": entries},
                       ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    target = folder / name
    if target.exists() and target.read_bytes() != body:
        raise ValueError("ExistingFrozenManifestDiffers")
    if not target.exists():
        with target.open("xb") as stream:
            stream.write(body)
    return {"entryCount": len(entries), "manifestSha256": hashlib.sha256(body).hexdigest(),
            "databaseWritePerformed": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--db-storage", action="store_true")
    args = parser.parse_args()
    print(json.dumps(build(args.root, args.db_storage)))
