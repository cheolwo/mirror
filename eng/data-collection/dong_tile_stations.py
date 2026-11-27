"""Bind public transfer instructions to verified station centres, never to exits."""

from contextlib import closing
import csv
import json
from pathlib import Path
import re
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION
from dong_tile_index import DBNAME, OUTPUT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import PUBLIC, ROOT, digest


INPUTS = {
    "15130556": ("철도운영기관코드", "선명", "역명", "환승이동순서", "이동내용상세", "환승이동내용"),
    "15166080": ("철도운영기관명", "선명", "역명", "지상구분", "역층", "출구번호", "점자설비구분", "점자설비명"),
}
STATION_KEYS = PUBLIC / "diorama-public-layer-intake-20261123/r9-station-services/station-services.sqlite"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source (id TEXT PRIMARY KEY,url TEXT NOT NULL,path TEXT NOT NULL,
    sha256 TEXT NOT NULL,edition TEXT NOT NULL);
CREATE TABLE station_scope (
    source_id TEXT NOT NULL REFERENCES source, operator TEXT NOT NULL,
    source_line TEXT NOT NULL,station_name TEXT NOT NULL,
    reviewed_station_id TEXT, q INTEGER,r INTEGER,slot INTEGER CHECK(slot BETWEEN 0 AND 6),
    binding_state TEXT NOT NULL CHECK(binding_state IN ('station_centre','source_station_label_only')),
    CHECK((binding_state='station_centre')=(q IS NOT NULL AND r IS NOT NULL AND slot IS NOT NULL
        AND reviewed_station_id IS NOT NULL)),
    PRIMARY KEY(source_id,operator,source_line,station_name)
) WITHOUT ROWID;
CREATE INDEX station_scope_tile ON station_scope(q,r,slot) WHERE binding_state='station_centre';
CREATE TABLE transfer_step (
    source_id TEXT NOT NULL,source_row INTEGER NOT NULL,operator TEXT NOT NULL,
    source_line TEXT NOT NULL,station_name TEXT NOT NULL,ordered_block INTEGER NOT NULL,
    reported_step INTEGER NOT NULL,step_text TEXT,description TEXT,
    PRIMARY KEY(source_id,source_row),
    FOREIGN KEY(source_id,operator,source_line,station_name) REFERENCES station_scope
);
CREATE TABLE braille_facility (
    source_id TEXT NOT NULL,source_row INTEGER NOT NULL,operator TEXT NOT NULL,
    source_line TEXT NOT NULL,station_name TEXT NOT NULL,ground_label TEXT,level_label TEXT,exit_label TEXT,
    type_label TEXT,facility_label TEXT,PRIMARY KEY(source_id,source_row),
    FOREIGN KEY(source_id,operator,source_line,station_name) REFERENCES station_scope
);
CREATE VIEW v_station_access_reference AS
SELECT sc.q,sc.r,sc.slot,sc.reviewed_station_id,sc.station_name,sc.source_line,
       COUNT(*) AS transfer_steps,COUNT(DISTINCT st.ordered_block) AS ordered_blocks,sc.binding_state
FROM station_scope sc JOIN transfer_step st USING(source_id,operator,source_line,station_name)
WHERE sc.binding_state='station_centre'
GROUP BY sc.reviewed_station_id,sc.station_name,sc.source_line,sc.q,sc.r,sc.slot;
"""


def public_source(directory, source_id):
    receipt = json.loads((directory / (source_id + "-receipt.json")).read_text(encoding="utf-8"))
    path = directory / (source_id + ".csv")
    if digest(path) != receipt["sha256"] or path.stat().st_size != receipt["bytes"]:
        raise ValueError(f"station_source_fingerprint_mismatch:{source_id}")
    with path.open(encoding=receipt["encoding"], newline="") as stream:
        reader = csv.reader(stream)
        if tuple(next(reader)) != INPUTS[source_id]:
            raise ValueError(f"unexpected_station_source_columns:{source_id}")
        rows = list(reader)
    if len(rows) != receipt["rows"] or any(len(row) != len(INPUTS[source_id]) for row in rows):
        raise ValueError(f"unexpected_station_source_rows:{source_id}")
    fields = (source_id, receipt["officialUrl"], path.relative_to(ROOT).as_posix(),
              receipt["sha256"], receipt["sourceVersion"])
    return fields, rows


def verified_centres():
    with closing(read_only(STATION_KEYS)) as stations, closing(read_only(OUTPUT / DBNAME)) as core:
        local = {place: (name, line, q, r, slot) for place, name, line, q, r, slot in core.execute(
            "SELECT place_id,name,line_or_ars,q,r,slot FROM transport_child_position WHERE mode='rail'")}
        centres = {}
        for line, name, code in stations.execute("SELECT line,name,metro_station_code FROM station"):
            if not code or not code.isdecimal() or not line or not line.isdecimal() or not 1 <= int(line) <= 8:
                continue
            normalized_line = line + "호선"
            metro = code.zfill(4)
            match = local.get(metro)
            if match is not None and match[:2] == (name, normalized_line):
                centres[normalized_line, name] = (metro, *match[2:])
    return centres


def load_source(db, directory, source_id, centres):
    source, rows = public_source(directory, source_id)
    db.execute("INSERT INTO source VALUES (?,?,?,?,?)", source)
    scopes = set()
    source_steps = []
    previous_identity = None
    previous_step = 0
    block = 0
    unexpected_steps = 0
    for source_row, row in enumerate(rows, 2):
        operator, line, name = (value.strip() for value in row[:3])
        if any(not value for value in (operator, line, name)):
            raise ValueError(f"incomplete_station_scope:{source_id}:{source_row}")
        identity = source_id, operator, line, name
        if identity not in scopes:
            centre = centres.get((line, name)) if operator == "S1(서울교통공사)" else None
            db.execute("INSERT INTO station_scope VALUES (?,?,?,?,?,?,?,?,?)", (
                *identity, *(centre or (None,)*4), "station_centre" if centre else "source_station_label_only"))
            scopes.add(identity)
        fields = [value.strip() or None for value in row[3:]]
        if source_id == "15130556":
            step = int(fields[0])
            if previous_identity != identity or step == 1:
                block += 1
                previous_step = 0
            unexpected_steps += int(step != previous_step + 1)
            if fields[1] and (prefix := re.match(r"(\d+)\)", fields[1])):
                unexpected_steps += int(int(prefix.group(1)) != step)
            source_steps.append((source_id, source_row, *identity[1:], block, step, *fields[1:]))
            previous_identity, previous_step = identity, step
        else:
            source_steps.append((source_id, source_row, *identity[1:], *fields))
    if source_id == "15130556":
        db.executemany("INSERT INTO transfer_step VALUES (?,?,?,?,?,?,?,?,?)", source_steps)
    else:
        db.executemany("INSERT INTO braille_facility VALUES (?,?,?,?,?,?,?,?,?,?)", source_steps)
    return {"sourceRows": len(rows), "stationOperatorLineGroups": len(scopes), "orderedSourceBlocks": block,
            "numberingMismatchesPreserved": unexpected_steps}


def build():
    directory = OUTPUT / "stations"
    centres = verified_centres()
    target = directory / "station-access.sqlite"
    with TemporaryDirectory(dir=directory) as tmp:
        draft = Path(tmp) / target.name
        with closing(sqlite3.connect(draft)) as db:
            db.executescript(SCHEMA)
            with db:
                counts = {identifier: load_source(db, directory, identifier, centres) for identifier in INPUTS}
                db.execute("INSERT INTO source VALUES (?,?,?,?,?)", (
                    "seoul-metro-keys", "https://www.data.go.kr/data/15044230/fileData.do",
                    STATION_KEYS.relative_to(ROOT).as_posix(), digest(STATION_KEYS), "2026-09-01 and published BIS key"))
            check_database(db)
            linked, steps = db.execute("SELECT COUNT(*),SUM(transfer_steps) FROM v_station_access_reference").fetchone()
            linked_tiles = db.execute("""SELECT COUNT(*) FROM
                (SELECT DISTINCT q,r,slot FROM v_station_access_reference)""").fetchone()[0]
        draft.replace(target)
    result = {"geometryRevision": REVISION, "distribution": "local-review", "sources": counts,
              "linkedStationCentres": linked, "linkedChildTiles": linked_tiles, "linkedTransferSteps": steps,
              "actualExitPositionsInSource": 0, "routingAuthorized": False, "databaseSha256": digest(target)}
    atomic_json(directory / "station-access-summary.json", result)
    return result


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
