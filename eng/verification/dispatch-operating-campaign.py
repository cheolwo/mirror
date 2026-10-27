"""Bounded loopback campaign; durable commands are never replayed after an uncertain POST.

This observes an already running dedicated Simulation host. It does not start a host,
Docker, a database or Ollama, and never approves reviews or trains model weights.
"""
import argparse
import datetime as dt
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import stat
import subprocess
import time
import urllib.error
import urllib.request
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_ROOT = "artifacts/local/dispatch-operating-campaign/20261021-r1"
API = "verification/dispatch-training/"
SCHEMA = "dispatch-operating-campaign.v1"
MAX_BYTES = 16 * 1024 * 1024
API_TIMEOUT = 10
IMPORT_TIMEOUT = 120
_spec = importlib.util.spec_from_file_location("dispatch_training_verify", pathlib.Path(__file__).with_name("dispatch-training-verify.py"))
verification = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(verification)
Failure = verification.VerificationFailure


def utc_now():
    return dt.datetime.now(dt.timezone.utc)


def stamp(value):
    return value.isoformat().replace("+00:00", "Z")


def parse_stamp(value):
    parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise Failure("CampaignTimestampInvalid")
    return parsed


def encoded(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def digest(value):
    return hashlib.sha256(value).hexdigest()


def checked_json(data):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise Failure("DuplicateJsonProperty")
            result[key] = value
        return result
    return json.loads(data, object_pairs_hook=unique)


def native(path):
    # Campaign snapshots have full SHA names. Do not let legacy MAX_PATH turn
    # an otherwise safe evidence location into a partial/missing write.
    value = str(path)
    if os.name == "nt" and not value.startswith("\\\\?\\"):
        value = "\\\\?\\UNC\\" + value[2:] if value.startswith("\\\\") else "\\\\?\\" + value
    return pathlib.Path(value)


def reject_links(path):
    for item in [path, *path.parents]:
        try:
            info = native(item).lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 1024):
            raise Failure("CampaignPathLinkRejected")


def private_path(repo, value):
    path = pathlib.Path(os.path.abspath(os.path.join(repo, value)))
    parent = pathlib.Path(os.path.abspath(repo / "artifacts/local"))
    reject_links(path)
    if path == parent or parent not in path.parents:
        raise Failure("CampaignPrivatePathRequired")
    return path


def read_bytes(path):
    reject_links(path)
    with native(path).open("rb") as stream:
        data = stream.read(MAX_BYTES + 1)
    if len(data) > MAX_BYTES:
        raise Failure("CampaignInputTooLarge")
    return data


def atomic_bytes(path, data, immutable=False):
    reject_links(path)
    native(path.parent).mkdir(parents=True, exist_ok=True)
    reject_links(path.parent)
    if immutable and native(path).exists():
        if read_bytes(path) != data:
            raise Failure("CampaignImmutableConflict")
        return
    pending = path.with_name(".pending-" + path.name + "-" + uuid.uuid4().hex)
    try:
        with native(pending).open("xb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        reject_links(path)
        os.replace(native(pending), native(path))
    finally:
        native(pending).unlink(missing_ok=True)


def atomic_json(path, value, immutable=False):
    atomic_bytes(path, encoded(value), immutable)


class CampaignLock:
    """An open OS lock, not a stale file/PID, owns the dedicated host campaign."""
    def __init__(self, path):
        self.path = path
        self.file = None

    def __enter__(self):
        reject_links(self.path)
        native(self.path.parent).mkdir(parents=True, exist_ok=True)
        self.file = native(self.path).open("a+b")
        try:
            self.file.seek(0, os.SEEK_END)
            if self.file.tell() == 0:
                self.file.write(b"0")
                self.file.flush()
            self.file.seek(0)
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(self.file.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(self.file.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except (OSError, BlockingIOError):
            self.file.close()
            self.file = None
            raise Failure("CampaignAlreadyRunning") from None
        return self

    def __exit__(self, *_):
        if self.file:
            if os.name == "nt":
                import msvcrt
                self.file.seek(0)
                msvcrt.locking(self.file.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl
                fcntl.flock(self.file.fileno(), fcntl.LOCK_UN)
            self.file.close()


class TrainingApi:
    def __init__(self, repo):
        self.repo = repo
        self.token = None
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), verification.NoRedirect())

    def request(self, method, path, body=None):
        allowed = (method == "POST" and path == "api/v1/auth/login"
                   or method == "GET" and path in {API + "current", API + "decisions", API + "incidents"}
                   or method == "POST" and path in {API + "start", API + "support-drivers", API + "stop"})
        if not allowed:
            raise Failure("CampaignApiPathNotAllowed")
        headers = {"Content-Type": "application/json"}
        if self.token:
            headers["Authorization"] = "Bearer " + self.token
        request = urllib.request.Request(verification.BASE + path,
            data=None if body is None else encoded(body), headers=headers, method=method)
        with self.opener.open(request, timeout=API_TIMEOUT) as response:
            data = response.read(MAX_BYTES + 1)
        if len(data) > MAX_BYTES:
            raise Failure("CampaignApiResponseTooLarge")
        value = checked_json(data)
        if not isinstance(value, dict):
            raise Failure("CampaignApiResponseInvalid")
        return verification.training_response(path, value) if method == "GET" else value

    def authenticate(self):
        path = private_path(self.repo, "artifacts/local/dispatch-training-r58/environment/.env")
        secrets = dict(line.split("=", 1) for line in read_bytes(path).decode("utf-8-sig").splitlines() if "=" in line)
        login = {}
        try:
            password = secrets["DISPATCH_TRAINING_ACCOUNT_PASSWORD"]
            login = self.request("POST", "api/v1/auth/login", {"userNameOrEmail": "dispatch-training-admin", "password": password})
            self.token = login.get("accessToken") or login.get("token")
            if not isinstance(self.token, str) or not self.token:
                raise Failure("CampaignAuthenticationTokenMissing")
        finally:
            secrets.clear()
            login.clear()

    def close(self):
        self.token = None


def import_snapshot(repo, source, corpus):
    dll = repo / "eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll"
    reject_links(dll)
    if not native(dll).is_file():
        raise Failure("ExperienceCliBuildRequired")
    # Never use dotnet run: this command only calls the existing CLI early branch.
    result = subprocess.run(["dotnet", str(dll), "--experience-import", "--session", str(source),
                             "--experience-root", str(corpus)], cwd=repo, capture_output=True,
                            timeout=IMPORT_TIMEOUT, check=False)
    if result.returncode:
        raise Failure("ExperienceImportFailed")


def actor_projection(raw, session, session_sha):
    """Commands are past acknowledgements, not the current transport authority.

There is no actor revision or per-step timestamp. No photo URL, object name,
driver identifier, login material, raw incident text, or inferred step time is copied.
"""
    actor = checked_json(raw)
    if not isinstance(actor, dict) or actor.get("runId") != session.get("runId"):
        raise Failure("ActorRunMismatch")
    slots = {item["index"]: item for item in session.get("slots", [])}
    rows = []
    unmatched = 0
    stages = {"arrive-pickup", "pickup-complete", "arrive-dropoff", "complete", "photo:pickup-complete", "photo:complete"}
    for order in actor.get("orders", []):
        slot = slots.get(order.get("index"))
        if (not slot or slot.get("clientRequestId") != order.get("clientRequestId")
                or not slot.get("canonicalOrderId")
                or slot.get("canonicalOrderId") != order.get("canonicalId")):
            unmatched += 1
            continue
        commands = []
        for key, outcome in order.get("commands", {}).items():
            if key in stages or re.fullmatch(r"accept:[0-9]+", key):
                if outcome not in {"Confirmed", "Unknown", "Rejected"}:
                    raise Failure("ActorCommandStatusInvalid")
                commands.append({"stage": key, "statusCode": outcome})
        photos = order.get("photos", {})
        def has_receipt(key):
            value = photos.get(key)
            return isinstance(value, dict) and all(isinstance(value.get(k), str) and value[k] for k in ("objectName", "url"))
        rows.append({"orderIndex": slot["index"], "clientRequestId": slot["clientRequestId"],
                     "canonicalOrderId": slot["canonicalOrderId"], "commands": commands,
                     "pickupPhotoReceiptPresent": has_receipt("photo:pickup-complete"),
                     "deliveryPhotoReceiptPresent": has_receipt("photo:complete")})
    return {"schemaVersion": "dispatch-campaign.actor-observations.v1", "synthetic": True,
            "runId": session["runId"], "sessionRevisionObserved": session["revision"],
            "sessionSourceSha256": session_sha, "actorSourceSha256": digest(raw),
            "actorRevisionAvailable": False, "sameAtomicRevisionVerified": False,
            "commandHistoryIsCurrentState": False, "physicalPhoto": False,
            "perStepTimesAvailable": False, "orders": rows, "unmatchedOrderCount": unmatched}


def campaign_identity(state):
    # The live state SHA changes every heartbeat. Bind a stop request to the
    # immutable campaign identity instead, retaining the observed state hash as
    # provenance only, never as an obsolete optimistic-lock requirement.
    keys = ("schemaVersion", "campaignId", "createdAtUtc", "deadlineUtc", "observeSeconds",
            "startRequested", "supportRequested", "sourcePath", "experienceRoot")
    return digest(encoded({key: state[key] for key in keys}))


def read_stop_request(campaign):
    path = campaign.folder / "stop-request.json"
    if not native(path).exists():
        return None
    value = checked_json(read_bytes(path))
    if not isinstance(value, dict):
        raise Failure("CampaignStopRequestCorrupt")
    payload = dict(value)
    expected = payload.pop("requestSha256", None)
    if (expected != digest(encoded(payload)) or payload.get("schemaVersion") != "dispatch-campaign.stop-request.v1"
            or payload.get("campaignId") != campaign.id
            or payload.get("campaignIdentitySha256") != campaign_identity(campaign.state)):
        raise Failure("CampaignStopRequestCorrupt")
    try:
        uuid.UUID(payload["requestId"])
        parse_stamp(payload["requestedAtUtc"])
    except (KeyError, ValueError, TypeError):
        raise Failure("CampaignStopRequestCorrupt") from None
    return value


def request_stop(repo, args, now=utc_now):
    """Write only a local request. Do not acquire the observer lease or use HTTP.

    Atomic state replacement lets this command validate an existing consistent
    state while the observer owns its OS lock. A later observer/restart handles
    the exact run and canonical command receipt under its normal execution lease.
    """
    if not args.campaign_id:
        raise Failure("ExplicitCampaignIdRequired")
    campaign = Campaign(repo, args, now=now)
    campaign.load()
    previous = read_stop_request(campaign)
    if previous:
        return {"campaignId": campaign.id, "stopRequested": True, "reused": True,
                "requestId": previous["requestId"], "httpExecuted": False}
    payload = {"schemaVersion": "dispatch-campaign.stop-request.v1", "campaignId": campaign.id,
               "requestId": str(uuid.uuid4()), "requestedAtUtc": stamp(now()),
               "campaignIdentitySha256": campaign_identity(campaign.state),
               "observedStateSha256": digest(encoded(campaign.state))}
    payload["requestSha256"] = digest(encoded(payload))
    atomic_json(campaign.folder / "stop-request.json", payload, immutable=True)
    return {"campaignId": campaign.id, "stopRequested": True, "reused": False,
            "requestId": payload["requestId"], "httpExecuted": False}


class Campaign:
    def __init__(self, repo, args, api=None, importer=import_snapshot, now=utc_now,
                 monotonic=time.monotonic, sleep=time.sleep, emit=None):
        self.repo = pathlib.Path(repo)
        self.args = args
        self.root = private_path(self.repo, args.campaign_root)
        self.source = private_path(self.repo, "artifacts/local/dispatch-training-r58/session.json")
        self.actors = self.source.with_name("actors.json")
        self.corpus = private_path(self.repo, args.experience_root)
        # The one dedicated host gets one OS lease, also across custom output roots.
        self.lock_path = private_path(self.repo, "artifacts/local/dispatch-operating-campaign/.campaign.lock")
        self.id = str(uuid.UUID(args.campaign_id)) if args.campaign_id else str(uuid.uuid4())
        self.folder = self.root / "campaigns" / self.id
        for left, right in ((self.folder, self.corpus), (self.folder, self.source.parent),
                            (self.corpus, self.source.parent)):
            if left == right or left in right.parents or right in left.parents:
                raise Failure("CampaignSourceOutputOverlap")
        self.state_path = self.folder / "state.json"
        self.state = None
        self.api = api or TrainingApi(self.repo)
        self.importer = importer
        self.now, self.monotonic, self.sleep = now, monotonic, sleep
        self.emit = emit or (lambda value: print(json.dumps(value, ensure_ascii=False), flush=True))
        self.current = None
        self.authenticated = False
        self.end = None

    def error(self, stage, error):
        self.state["errors"].append({**verification.safe_error(stage, error), "atUtc": stamp(self.now())})

    def warning(self, stage, error):
        self.state["warnings"].append({**verification.safe_error(stage, error), "atUtc": stamp(self.now())})

    def save(self):
        self.state["updatedAtUtc"] = stamp(self.now())
        payload = dict(self.state)
        payload["stateSha256"] = digest(encoded(self.state))
        atomic_json(self.state_path, payload)
        status = {key: self.state[key] for key in ("schemaVersion", "campaignId", "phase", "createdAtUtc", "updatedAtUtc", "deadlineUtc",
            "observedRunId", "startedRunId", "exitCode", "commands", "imports", "errors", "warnings")}
        status.update({"synthetic": True, "actualOperationalDispatch": False, "weightsTrained": False,
                       "humanReviewApproved": False, "finalCurrent": self.current or self.state.get("lastCurrent"),
                       "currentSource": "LastObservedCanonicalApi", "lastCanonicalReadAtUtc": self.state.get("lastCanonicalReadAtUtc"),
                       "finalReadAvailability": self.state.get("finalReadAvailability", {}),
                       "statePath": str(self.state_path.relative_to(self.repo)),
                       "snapshotDirectory": str((self.folder / "snapshots").relative_to(self.repo))})
        atomic_json(self.folder / "status.json", status)
        atomic_json(self.root / "current-campaign.json", {"schemaVersion": "dispatch-campaign.current.v1", "campaignId": self.id,
            "updatedAtUtc": self.state["updatedAtUtc"], "statusPath": str((self.folder / "status.json").relative_to(self.repo)),
            "phase": self.state["phase"]})

    def load(self):
        if native(self.state_path).exists():
            state = checked_json(read_bytes(self.state_path))
            if not isinstance(state, dict):
                raise Failure("CampaignStateCorrupt")
            expected = state.pop("stateSha256", None)
            if expected != digest(encoded(state)) or state.get("schemaVersion") != SCHEMA or state.get("campaignId") != self.id:
                raise Failure("CampaignStateCorrupt")
            if (state.get("sourcePath") != str(self.source.relative_to(self.repo))
                    or state.get("experienceRoot") != str(self.corpus.relative_to(self.repo))):
                raise Failure("CampaignConfigurationChanged")
            if (not isinstance(state.get("commands"), list) or not isinstance(state.get("imports"), list)
                    or not isinstance(state.get("errors"), list)):
                raise Failure("CampaignStateCorrupt")
            parse_stamp(state["deadlineUtc"])
            for command in state["commands"]:
                if command.get("action") not in {"start", "stop", "support-drivers"}:
                    raise Failure("CampaignStateCorrupt")
                uuid.UUID(command["clientRequestId"])
            state.setdefault("warnings", [])
            self.state = state
        else:
            if self.args.campaign_id:
                raise Failure("CampaignNotFound")
            self.state = {"schemaVersion": SCHEMA, "campaignId": self.id, "createdAtUtc": stamp(self.now()),
                "deadlineUtc": stamp(self.now() + dt.timedelta(seconds=self.args.observe_seconds)), "phase": "Prepared",
                "observeSeconds": self.args.observe_seconds, "startRequested": self.args.start,
                "supportRequested": self.args.support_on_shortage,
                "sourcePath": str(self.source.relative_to(self.repo)), "experienceRoot": str(self.corpus.relative_to(self.repo)),
                "observedRunId": None, "startedRunId": None, "commands": [], "imports": [], "errors": [], "warnings": [],
                "lastImportedSha256": None, "exitCode": None}
            self.save()

    def read_current(self):
        current = self.api.request("GET", API + "current")
        if not isinstance(current.get("revision"), int) or not isinstance(current.get("allowedCommands"), list):
            raise Failure("CampaignCurrentDtoInvalid")
        run_id = current.get("runId")
        if self.state["observedRunId"] and run_id != self.state["observedRunId"]:
            raise Failure("CampaignRunChanged")
        if run_id and self.state["observedRunId"] is None:
            # A prepared start journal also fixes the expected new run before POST.
            start = next((c for c in self.state["commands"] if c["action"] == "start"), None)
            if start and run_id != start["expectedResultRunId"]:
                raise Failure("CampaignRunChanged")
            self.state["observedRunId"] = run_id
        self.current = current
        self.state["lastCurrent"] = current
        self.state["lastCanonicalReadAtUtc"] = stamp(self.now())
        applied = {str(item).lower() for item in current.get("appliedCommandIds", [])}
        for command in self.state["commands"]:
            if command["clientRequestId"].lower() in applied:
                target = command.get("expectedResultRunId") if command["action"] == "start" else command["runId"]
                if target != run_id:
                    raise Failure("CampaignCommandRunMismatch")
                command["outcome"] = "AppliedCanonical"
                if command["action"] == "start":
                    self.state["startedRunId"] = run_id
        self.save()
        return current

    def command(self, action, count=None):
        # A journal entry is a permanent one-shot reservation, even if the process
        # died between fsync and POST. A restart only reads canonical receipts.
        if any(c["action"] == action for c in self.state["commands"]):
            return
        if action != "stop" and read_stop_request(self):
            return
        current = self.read_current()
        if action not in current["allowedCommands"]:
            raise Failure("CampaignCommandNotAllowed")
        if action != "stop" and (self.now() >= parse_stamp(self.state["deadlineUtc"])
                or self.end is not None and self.monotonic() >= self.end):
            return
        if action != "stop" and read_stop_request(self):
            return
        request_id = str(uuid.uuid4())
        payload = {"clientRequestId": request_id, "expectedRevision": current["revision"], "runId": current.get("runId")}
        if count is not None:
            payload["count"] = count
        attempt = {"action": action, **payload, "outcome": "Unknown", "postRepeated": False,
                   "reservedAtUtc": stamp(self.now())}
        if action == "start":
            attempt["expectedResultRunId"] = "dispatch-training-" + uuid.UUID(request_id).hex
            # The prior Completed session is not the new campaign's observed run.
            self.state["observedRunId"] = None
        self.state["commands"].append(attempt)
        self.save()  # Must succeed before any state-changing HTTP request.
        try:
            self.api.request("POST", API + action, payload)
            attempt["responseReceived"] = True
        except KeyboardInterrupt:
            self.save()
            raise
        except Exception as error:
            attempt["postError"] = verification.safe_error("command-post:" + action, error)
        self.save()
        try:
            self.read_current()
        except Exception as error:
            self.error("command-confirm:" + action, error)
            self.save()
        if attempt["outcome"] != "AppliedCanonical":
            self.state["phase"] = "OutcomeUnknown"
            self.save()

    def checkpoint(self):
        try:
            first = read_bytes(self.source)
            actor = read_bytes(self.actors) if native(self.actors).exists() else None
            second = read_bytes(self.source)
            if first != second:
                self.warning("checkpoint", Failure("CampaignSourceChangedDuringRead"))
                self.save()
                return
            session = checked_json(first)
            run_id = session.get("runId")
            if not run_id:
                return
            if run_id != self.state["observedRunId"]:
                raise Failure("CampaignSourceRunMismatch")
            sha = digest(first)
            folder = self.folder / "snapshots" / sha
            target = folder / "session.json"
            atomic_bytes(target, first, immutable=True)
            if actor is not None:
                try:
                    projection = actor_projection(actor, session, sha)
                    actor_sha = digest(encoded(projection))
                    atomic_json(folder / ("actors-observed-" + actor_sha + ".json"), projection, immutable=True)
                except Exception as error:
                    # actors has no revision and may still belong to the prior
                    # run before the first tick. Missing optional detail must
                    # not turn a verified session/import into an execution failure.
                    self.warning("actor-observation", error)
            if any(item["sourceSha256"] == sha and item["outcome"] == "Imported" for item in self.state["imports"]):
                self.save()
                return
            row = {"sourceSha256": sha, "runId": run_id, "revision": session.get("revision"),
                   "snapshotPath": str(target.relative_to(self.repo)), "attemptedAtUtc": stamp(self.now()), "outcome": "Pending"}
            self.state["imports"].append(row)
            self.save()
            try:
                self.importer(self.repo, target, self.corpus)
                row["outcome"] = "Imported"
                self.state["lastImportedSha256"] = sha
            except Exception as error:
                row["outcome"] = "Failed"
                row["error"] = verification.safe_error("experience-import", error)
                self.error("experience-import", error)
            self.save()
        except Exception as error:
            self.error("checkpoint", error)
            self.save()

    def stop_owned(self):
        try:
            current = self.read_current()
            if (self.state["startedRunId"] and current.get("runId") == self.state["startedRunId"]
                    and "stop" in current.get("allowedCommands", [])):
                self.state["phase"] = "Stopping"
                self.save()
                self.command("stop")
        except Exception as error:
            self.error("stop-owned-run", error)
            self.save()

    def execute(self):
        with CampaignLock(self.lock_path):
            self.load()
            end = self.monotonic() + max(0, min(self.state["observeSeconds"],
                (parse_stamp(self.state["deadlineUtc"]) - self.now()).total_seconds()))
            self.end = end
            exit_code, reason = 0, "DeadlineReached"
            try:
                requested_stop = read_stop_request(self) is not None
                if requested_stop:
                    reason = "RequestedStop"
                self.state["phase"] = "Authenticating"
                self.save()
                self.api.authenticate()
                self.authenticated = True
                current = self.read_current()
                if not requested_stop and self.monotonic() < end and self.state["startRequested"] and not self.state["commands"]:
                    self.command("start")
                self.state["phase"] = "Observing"
                self.save()
                next_checkpoint = self.monotonic()
                while self.monotonic() < end:
                    if read_stop_request(self):
                        reason = "RequestedStop"
                        break
                    current = self.read_current()
                    if self.monotonic() >= next_checkpoint:
                        self.checkpoint()
                        next_checkpoint = self.monotonic() + 60
                    if current.get("phaseCode") == "Completed":
                        reason = "Completed"
                        break
                    if self.state["supportRequested"] and "support-drivers" in current.get("allowedCommands", []):
                        if current.get("availableSupportDriverCount", 0) > 0 and any(
                                x.get("kindCode") == "DriverShortage" for x in current.get("pendingIncidents", [])):
                            self.command("support-drivers", current["availableSupportDriverCount"])
                    self.sleep(min(5, max(0, end - self.monotonic())))
            except KeyboardInterrupt:
                exit_code, reason = 130, "Interrupted"
                self.error("campaign", KeyboardInterrupt())
            except Exception as error:
                exit_code, reason = 1, "Failed"
                self.error("campaign", error)
            finally:
                try:
                    if self.authenticated:
                        # Stop only generation of the exact run started here. It
                        # does not cancel already accepted work or stop the host.
                        self.stop_owned()
                        self.state["finalReadAvailability"] = {}
                        for name in ("current", "decisions", "incidents"):
                            try:
                                value = self.read_current() if name == "current" else self.api.request("GET", API + name)
                                if value.get("runId") != self.state["observedRunId"]:
                                    raise Failure("CampaignDiagnosticRunMismatch")
                                atomic_json(self.folder / ("final-" + name + ".json"), value)
                                self.state["finalReadAvailability"][name] = {"available": True, "observedAtUtc": stamp(self.now())}
                            except Exception as error:
                                self.error("final-read:" + name, error)
                                self.state["finalReadAvailability"][name] = {"available": False,
                                    "error": verification.safe_error("final-read:" + name, error)}
                        self.checkpoint()
                except KeyboardInterrupt:
                    exit_code, reason = 130, "Interrupted"
                    self.error("finalization", KeyboardInterrupt())
                finally:
                    self.api.close()
                    unknown = any(c["outcome"] != "AppliedCanonical" for c in self.state["commands"])
                    if self.state["errors"] or unknown:
                        if exit_code == 0:
                            exit_code = 1
                        if reason not in {"Failed", "Interrupted"}:
                            reason = "NeedsReview"
                    self.state["phase"] = reason
                    self.state["exitCode"] = exit_code
                    self.state["finishedAtUtc"] = stamp(self.now())
                    self.save()
                    self.emit({"campaignId": self.id, "phase": reason, "exitCode": exit_code,
                               "statusPath": str((self.folder / "status.json").relative_to(self.repo)),
                               "synthetic": True, "weightsTrained": False})
            return exit_code


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description="기존 격리 서버의 가상 운영을 유한 시간 관측·누적합니다.")
    parser.add_argument("--start", action="store_true", help="기존 API의 새 가상 회차를 명시적으로 한 번 시작")
    parser.add_argument("--request-stop", action="store_true", help="기존 캠페인에 로컬 정지 요청만 기록(HTTP·상태 초기화 없음)")
    parser.add_argument("--observe-seconds", type=int, default=2200)
    parser.add_argument("--support-on-shortage", action="store_true", help="가상 기사 부족 시 시험 지원 기사 참여를 한 번 요청")
    parser.add_argument("--campaign-id", help="기존 UUID 캠페인을 저장된 마감·명령으로 조회 우선 재개")
    parser.add_argument("--campaign-root", default=DEFAULT_ROOT)
    parser.add_argument("--experience-root", default="artifacts/local/dispatch-experiences")
    args = parser.parse_args(argv)
    if args.request_stop and (not args.campaign_id or args.start or args.support_on_shortage):
        parser.error("request-stop requires campaign-id and cannot start or activate support")
    if not 1 <= args.observe_seconds <= 3600:
        parser.error("Observation must be 1..3600 seconds")
    if args.campaign_id:
        try:
            uuid.UUID(args.campaign_id)
        except ValueError:
            parser.error("campaign-id must be an existing campaign UUID")
    return args


def main(argv=None):
    try:
        args = parse_args(argv)
        if args.request_stop:
            print(json.dumps(request_stop(ROOT, args), ensure_ascii=False))
            return 0
        return Campaign(ROOT, args).execute()
    except KeyboardInterrupt:
        print(json.dumps({"error": {"code": "CampaignInterruptedBeforeOpen"}}))
        return 130
    except Exception as error:
        print(json.dumps({"error": verification.safe_error("campaign-open", error)}, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
