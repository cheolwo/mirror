"""Build the local role workflow research index from the reviewed handoff inputs."""

import argparse
import hashlib
import json
import re
import sqlite3
from contextlib import closing
from pathlib import Path
from urllib.parse import parse_qsl, urlsplit


REPO = Path(__file__).resolve().parents[2]
INTAKE = REPO / "artifacts/local/public-data/role-tile-intake-20261126"
CASE_FILES = (
    "driver-cargo.json",
    "merchant-resident-platform.json",
    "observer-and-handoff.json",
    "wholesale-farm.json",
)
STAGES = (
    ("prepare", 1, "준비"),
    ("work", 2, "작업"),
    ("handoff", 3, "인계"),
    ("review", 4, "결과 확인"),
)
ID_PATTERN = re.compile(r"[A-Za-z0-9]+(?:[._-][A-Za-z0-9]+)*\Z")

SCHEMA = """
CREATE TABLE input_file (name TEXT PRIMARY KEY, sha256 TEXT NOT NULL);
CREATE TABLE role (id TEXT PRIMARY KEY, label TEXT NOT NULL, position INTEGER NOT NULL UNIQUE);
CREATE TABLE stage (id TEXT PRIMARY KEY, position INTEGER NOT NULL UNIQUE, label TEXT NOT NULL);
CREATE TABLE situation (
    id TEXT PRIMARY KEY,
    role_id TEXT NOT NULL REFERENCES role(id),
    stage_id TEXT NOT NULL REFERENCES stage(id),
    question TEXT NOT NULL,
    surface TEXT NOT NULL,
    existing_evidence TEXT NOT NULL,
    public_candidate TEXT NOT NULL,
    workflow_record TEXT NOT NULL,
    next_decision TEXT NOT NULL,
    intake_file TEXT NOT NULL REFERENCES input_file(name)
);
CREATE TABLE situation_counterparty (
    situation_id TEXT NOT NULL REFERENCES situation(id),
    position INTEGER NOT NULL CHECK(position > 0),
    label TEXT NOT NULL,
    PRIMARY KEY(situation_id, position)
) WITHOUT ROWID;
CREATE TABLE needed_information (
    situation_id TEXT NOT NULL REFERENCES situation(id),
    position INTEGER NOT NULL CHECK(position > 0),
    label TEXT NOT NULL,
    PRIMARY KEY(situation_id, position),
    UNIQUE(situation_id, label)
) WITHOUT ROWID;
CREATE TABLE evidence_location (
    id INTEGER PRIMARY KEY,
    locator_kind TEXT NOT NULL CHECK(locator_kind IN ('public_url','local_path')),
    locator TEXT NOT NULL,
    UNIQUE(locator_kind, locator)
);
CREATE TABLE situation_source (
    situation_id TEXT NOT NULL REFERENCES situation(id),
    position INTEGER NOT NULL CHECK(position > 0),
    location_id INTEGER NOT NULL REFERENCES evidence_location(id),
    title TEXT NOT NULL,
    evidence_type TEXT NOT NULL,
    period TEXT NOT NULL,
    note TEXT NOT NULL,
    line_number INTEGER CHECK(line_number > 0),
    PRIMARY KEY(situation_id, position)
) WITHOUT ROWID;
CREATE TABLE example_flow (
    id TEXT PRIMARY KEY,
    question TEXT NOT NULL,
    handoff_boundary TEXT NOT NULL
);
CREATE TABLE example_flow_step (
    flow_id TEXT NOT NULL REFERENCES example_flow(id),
    position INTEGER NOT NULL CHECK(position > 0),
    situation_id TEXT NOT NULL REFERENCES situation(id),
    handoff_fact TEXT NOT NULL,
    PRIMARY KEY(flow_id, position),
    UNIQUE(flow_id, situation_id)
) WITHOUT ROWID;
CREATE INDEX situation_by_role_stage ON situation(role_id, stage_id);
CREATE INDEX situation_source_by_location ON situation_source(location_id);

CREATE VIEW v_role_workflow_research AS
SELECT role.id AS role_id, role.label AS role_name, role.position AS role_order,
       stage.id AS stage_id, stage.label AS stage_name, stage.position AS stage_order,
       situation.id AS situation_id, question, surface, existing_evidence,
       public_candidate, workflow_record, next_decision, intake_file
FROM situation JOIN role ON role.id = role_id JOIN stage ON stage.id = stage_id;

CREATE VIEW v_role_workflow_fields AS
SELECT role_id, stage_id, situation.id AS situation_id, position, label
FROM needed_information JOIN situation ON situation.id = situation_id;

CREATE VIEW v_role_workflow_sources AS
SELECT situation.role_id, situation.id AS situation_id, situation_source.position,
       title, evidence_type, period, note, locator_kind, locator, line_number
FROM situation_source JOIN situation ON situation.id = situation_id
JOIN evidence_location ON evidence_location.id = location_id;

CREATE VIEW v_role_workflow_example_steps AS
SELECT example_flow.id AS flow_id, example_flow.question AS flow_question, position,
       situation.id AS situation_id, role_id, stage_id, handoff_fact, handoff_boundary
FROM example_flow_step JOIN example_flow ON example_flow.id = flow_id
JOIN situation ON situation.id = situation_id;
"""


def read_input(db, path):
    raw = path.read_bytes()
    db.execute("INSERT INTO input_file VALUES (?, ?)", (path.name, hashlib.sha256(raw).hexdigest()))
    return json.loads(raw.decode("utf-8-sig"))


def required(record, key):
    value = record.get(key)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{record.get('id', '<source>')}: missing {key}")
    return value.strip()


def identifier(record, key):
    value = required(record, key)
    if not ID_PATTERN.fullmatch(value):
        raise ValueError(f"Invalid identifier {key}={value}")
    return value


def entries(record, key, minimum=1):
    values = record.get(key)
    if not isinstance(values, list) or len(values) < minimum:
        raise ValueError(f"{record.get('id', '<document>')}: expected {key}")
    return values


def check_source(source):
    if bool(source.get("url")) == bool(source.get("path")):
        raise ValueError("Each source must be one external URL or one local path")
    if source.get("path"):
        return "local_path", required(source, "path")

    locator = required(source, "url")
    url = urlsplit(locator)
    sensitive_parameters = {"apikey", "servicekey", "access_token", "token", "signature"}
    if (
        url.scheme != "https" or not url.hostname or url.username or url.password
        or ";jsessionid=" in locator.lower()
        or sensitive_parameters.intersection(key.lower() for key, _ in parse_qsl(url.query, keep_blank_values=True))
    ):
        raise ValueError("Expected a stable public HTTPS URL without credentials")
    return "public_url", locator


def insert_situation(db, situation, filename):
    situation_id = identifier(situation, "id")
    db.execute(
        "INSERT INTO situation VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        (situation_id, required(situation, "role"), required(situation, "stage"),
         required(situation, "question"), required(situation, "surface"),
         required(situation, "existing"), required(situation, "publicCandidate"),
         required(situation, "workflowRecord"), required(situation, "nextDecision"), filename),
    )
    counterparties = situation.get("counterparty", [])
    if isinstance(counterparties, str):
        counterparties = [counterparties]
    if not isinstance(counterparties, list):
        raise ValueError(f"{situation_id}: expected a list of counterparties")
    for position, label in enumerate(counterparties, 1):
        db.execute("INSERT INTO situation_counterparty VALUES (?, ?, ?)",
                   (situation_id, position, required({"label": label}, "label")))
    for position, label in enumerate(entries(situation, "needed"), 1):
        db.execute("INSERT INTO needed_information VALUES (?, ?, ?)",
                   (situation_id, position, required({"label": label}, "label")))
    for position, source in enumerate(entries(situation, "sources"), 1):
        kind, locator = check_source(source)
        db.execute("INSERT OR IGNORE INTO evidence_location (locator_kind, locator) VALUES (?, ?)",
                   (kind, locator))
        location_id, = db.execute(
            "SELECT id FROM evidence_location WHERE locator_kind = ? AND locator = ?", (kind, locator)
        ).fetchone()
        line = source.get("line")
        if line is not None and (type(line) is not int or line <= 0 or kind != "local_path"):
            raise ValueError(f"{situation_id}: invalid local source line")
        db.execute("INSERT INTO situation_source VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                   (situation_id, position, location_id, required(source, "title"),
                    required(source, "type"), required(source, "period"), required(source, "note"), line))


def insert_flows(db, records):
    for flow in records:
        flow_id = identifier(flow, "id")
        db.execute("INSERT INTO example_flow VALUES (?, ?, ?)",
                   (flow_id, required(flow, "question"), required(flow, "handoffBoundary")))
        for position, step in enumerate(entries(flow, "steps", 2), 1):
            db.execute("INSERT INTO example_flow_step VALUES (?, ?, ?, ?)",
                       (flow_id, position, identifier(step, "situation"), required(step, "handoffFact")))


def verify(db):
    if db.execute("PRAGMA integrity_check").fetchall() != [("ok",)] or db.execute(
        "PRAGMA foreign_key_check"
    ).fetchone():
        raise ValueError("SQLite integrity or foreign-key validation failed")
    roles = dict(db.execute("""
        SELECT role.id, COUNT(situation.id) FROM role
        LEFT JOIN situation ON situation.role_id = role.id GROUP BY role.id ORDER BY role.position
    """))
    missing_roles = [role for role, count in roles.items() if count == 0]
    if not roles or missing_roles:
        raise ValueError(f"Role coverage incomplete: {missing_roles}")
    single_role_flows = db.execute("""
        SELECT flow_id FROM v_role_workflow_example_steps GROUP BY flow_id HAVING COUNT(DISTINCT role_id) < 2
    """).fetchall()
    if single_role_flows:
        raise ValueError(f"Expected inter-role handoffs in example flows: {single_role_flows}")
    return {
        "caseCounts": roles,
        "situationCount": sum(roles.values()),
        "stageCounts": dict(db.execute("""
            SELECT stage_id, COUNT(*) FROM situation JOIN stage ON stage.id = stage_id
            GROUP BY stage_id ORDER BY stage.position
        """)),
        "neededInformationCount": db.execute("SELECT COUNT(*) FROM needed_information").fetchone()[0],
        "counterpartyLinkCount": db.execute("SELECT COUNT(*) FROM situation_counterparty").fetchone()[0],
        "sourceLinkCount": db.execute("SELECT COUNT(*) FROM situation_source").fetchone()[0],
        "uniqueSourceLocations": dict(db.execute("SELECT locator_kind, COUNT(*) FROM evidence_location GROUP BY locator_kind")),
        "exampleFlowCount": db.execute("SELECT COUNT(*) FROM example_flow").fetchone()[0],
        "inputSha256": dict(db.execute("SELECT name, sha256 FROM input_file ORDER BY name")),
        "integrityCheck": "ok",
        "foreignKeyViolations": 0,
    }


def build(intake, case_files=CASE_FILES):
    research = intake / "role-research"
    catalog = intake / "role-tile-catalog.sqlite"
    with closing(sqlite3.connect(catalog.resolve().as_uri() + "?mode=ro", uri=True)) as source:
        roles = source.execute("SELECT id, label, menu_order FROM role ORDER BY menu_order").fetchall()
    result = research / "role-workflow-research.sqlite"
    temporary = research / "role-workflow-research.building.sqlite"
    temporary.unlink(missing_ok=True)
    try:
        with closing(sqlite3.connect(temporary)) as db, db:
            db.execute("PRAGMA foreign_keys=ON")
            db.executescript(SCHEMA)
            db.execute("INSERT INTO input_file VALUES (?, ?)",
                       (catalog.name, hashlib.sha256(catalog.read_bytes()).hexdigest()))
            db.executemany("INSERT INTO role VALUES (?, ?, ?)", roles)
            db.executemany("INSERT INTO stage VALUES (?, ?, ?)", STAGES)
            for name in case_files:
                contents = read_input(db, research / name)
                cases = contents if isinstance(contents, list) else entries(contents, "cases")
                if not cases:
                    raise ValueError(f"No situations in {name}")
                for case in cases:
                    insert_situation(db, case, name)
            insert_flows(db, entries(read_input(db, research / "example-flows.json"), "flows"))
            report = verify(db)
    except Exception:
        temporary.unlink(missing_ok=True)
        raise
    temporary.replace(result)
    report["databaseSha256"] = hashlib.sha256(result.read_bytes()).hexdigest()
    (research / "workflow-research-verification.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--intake", type=Path, default=INTAKE)
    args = parser.parse_args()
    print(json.dumps(build(args.intake), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
