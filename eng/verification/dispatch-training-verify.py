"""Observe the real isolated training API; never store login material or resend an uncertain POST."""
import argparse
import json
import os
import pathlib
import time
import urllib.error
import urllib.request
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
ARTIFACTS = ROOT / "artifacts/local/dispatch-training-r58"
BASE = "http://127.0.0.1:5324/"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise RuntimeError("Training redirect is not allowed")


class VerificationFailure(RuntimeError):
    def __init__(self, code):
        super().__init__(code)
        self.code = code


def safe_error(stage, error):
    """Never persist exception messages, response bodies, headers, or authentication material."""
    result = {"stage": stage, "type": type(error).__name__}
    if isinstance(error, VerificationFailure):
        result["code"] = error.code
    if isinstance(error, urllib.error.HTTPError):
        result["httpStatus"] = int(error.code)
    return result


def training_response(path, value):
    """Keep only the existing public training DTO fields; authentication responses never enter evidence."""
    def fields(source, names):
        if not isinstance(source, dict):
            raise VerificationFailure("InvalidTrainingDtoShape")
        return {name: source[name] for name in names.split() if name in source}

    def ai(source):
        return None if source is None else fields(source, "statusCode ruleDriverId shadowDriverId sameChoice elapsedMilliseconds reasonCode modelName")

    def incidents(source):
        return [fields(item, "incidentId orderId kindCode statusCode message recordedAtUtc") for item in source]

    if path.endswith("/current"):
        if not {"phaseCode", "revision", "counts"}.issubset(value):
            raise VerificationFailure("InvalidSessionDtoShape")
        public = fields(value, "runId revision phaseCode serverTimeUtc startedAtUtc generationEndsAtUtc remainingGenerationSeconds durationSeconds orderIntervalSeconds maximumOrders activeDriverCount activatedSupportDriverCount availableSupportDriverCount allowedCommands appliedCommandIds")
        public["counts"] = fields(value["counts"], "generatedOrders waitingOrders assignedOrders inProgressOrders completedOrders failedOrders pendingIncidents")
        public["nextEvent"] = None if value.get("nextEvent") is None else fields(value["nextEvent"], "kindCode dueAtUtc label")
        public["drivers"] = [fields(item, "driverId label statusCode isSupport activeWorkCount completedWorkCount") for item in value.get("drivers", [])]
        public["pendingIncidents"] = incidents(value.get("pendingIncidents", []))
        public["latestAiResult"] = ai(value.get("latestAiResult"))
        return public
    public = fields(value, "runId revision")
    if path.endswith("/incidents"):
        public["items"] = incidents(value.get("items", []))
    else:
        public["items"] = []
        for item in value.get("items", []):
            decision = fields(item, "decisionId orderId recordedAtUtc ruleStatusCode ruleDriverLabel summary")
            decision["aiResult"] = ai(item.get("aiResult"))
            decision["candidates"] = [fields(candidate, "token driverLabel eligibilityCode distanceKm baseScore agingScore totalScore")
                                      for candidate in item.get("candidates", [])]
            public["items"].append(decision)
    return public


def write_evidence(target, evidence):
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(target.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("x", encoding="utf-8") as stream:
            json.dump(evidence, stream, ensure_ascii=False, indent=2)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, target)
    finally:
        temporary.unlink(missing_ok=True)


def parse_args(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--observe-seconds", type=int, default=2200)
    controls = parser.add_mutually_exclusive_group()
    controls.add_argument("--start", action="store_true")
    controls.add_argument("--resume", action="store_true", help="Explicitly resume the preserved paused session")
    parser.add_argument("--support-on-shortage", action="store_true", help="Explicit test-user action, not server automation")
    args = parser.parse_args(argv)
    if not 1 <= args.observe_seconds <= 3600:
        parser.error("Observation must be 1..3600 seconds")
    return args


def main(argv=None):
    args = parse_args(argv)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    token = None
    api = "verification/dispatch-training/"
    current = None
    samples = []
    commands = []
    errors = []
    reads = {}
    final_reads = {"decisions": None, "incidents": None}
    stage = "environment"
    exit_code = 0
    observation_finished = False
    authenticated_read_observed = False
    evidence_id = time.strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex
    target = ARTIFACTS / ("http-verification-" + evidence_id + ".json")

    def request(method, path, body=None, authenticated=True, timeout=30):
        allowed = ((method == "POST" and path == "api/v1/auth/login")
                   or (method == "GET" and path in {api + "current", api + "decisions", api + "incidents"})
                   or (method == "POST" and path in {api + "start", api + "resume", api + "support-drivers"}))
        if not allowed:
            raise VerificationFailure("VerificationPathNotAllowed")
        headers = {"Content-Type": "application/json"}
        if authenticated and token:
            headers["Authorization"] = "Bearer " + token
        req = urllib.request.Request(BASE + path, data=None if body is None else json.dumps(body).encode(), headers=headers, method=method)
        with opener.open(req, timeout=timeout) as response:
            result = json.load(response)
            if not isinstance(result, dict):
                raise VerificationFailure("InvalidApiResponseShape")
            return training_response(path, result) if method == "GET" else result

    def command(action, extra=None):
        nonlocal current, stage
        stage = "command-before-read:" + action
        current = request("GET", api + "current")
        command_id = str(uuid.uuid4())
        payload = {"clientRequestId": command_id, "expectedRevision": current["revision"], "runId": current.get("runId")}
        payload.update(extra or {})
        attempt = {"action": action, "clientRequestId": command_id, "expectedRevision": current["revision"],
                   "runId": current.get("runId"), "outcome": "Unknown", "postRepeated": False}
        commands.append(attempt)
        post_error = None
        stage = "command-post:" + action
        try:
            request("POST", api + action, payload)
            attempt["responseReceived"] = True
        except Exception as error:
            post_error = error
            attempt["postError"] = safe_error(stage, error)
        # Even a successful POST response is verified against the canonical GET. No POST is replayed.
        stage = "command-confirm-read:" + action
        try:
            current = request("GET", api + "current")
        except Exception as error:
            attempt["confirmationError"] = safe_error(stage, error)
            raise VerificationFailure("CommandOutcomeUnknownNoPostReplay") from None
        applied = {str(value).lower() for value in current.get("appliedCommandIds", [])}
        if command_id.lower() in applied:
            attempt["outcome"] = "AppliedCanonical"
            return
        if isinstance(post_error, urllib.error.HTTPError) and 400 <= post_error.code < 500:
            attempt["outcome"] = "RejectedCanonicalNotApplied"
            raise VerificationFailure("CommandRejectedNoPostReplay")
        raise VerificationFailure("CommandOutcomeUnknownNoPostReplay")

    try:
        secrets = dict(line.split("=", 1) for line in (ARTIFACTS / "environment/.env").read_text(encoding="utf-8-sig").splitlines() if "=" in line)
        stage = "authentication"
        login = request("POST", "api/v1/auth/login", {"userNameOrEmail": "dispatch-training-admin", "password": secrets["DISPATCH_TRAINING_ACCOUNT_PASSWORD"]}, False)
        token = login.get("accessToken") or login.get("token")
        secrets.clear()
        login.clear()
        if not token:
            raise VerificationFailure("AuthenticationTokenMissing")
        stage = "initial-current"
        current = request("GET", api + "current")
        authenticated_read_observed = True
        samples.append(current)
        for action, enabled in (("start", args.start), ("resume", args.resume)):
            if enabled:
                if action not in current.get("allowedCommands", []):
                    raise VerificationFailure("ExistingSessionCannot" + action.title())
                command(action)
        end = time.monotonic() + args.observe_seconds
        previous = None
        while time.monotonic() < end:
            stage = "observe-current"
            current = request("GET", api + "current")
            public = {"phase": current["phaseCode"], "counts": current["counts"], "support": current["activatedSupportDriverCount"],
                      "ai": (current.get("latestAiResult") or {}).get("statusCode"), "at": current["serverTimeUtc"]}
            fingerprint = json.dumps({k: v for k, v in public.items() if k != "at"}, sort_keys=True)
            if fingerprint != previous:
                print(json.dumps(public, ensure_ascii=False), flush=True)
                previous = fingerprint
                samples.append(current)
            if args.support_on_shortage and current["availableSupportDriverCount"] and any(x["kindCode"] == "DriverShortage" for x in current.get("pendingIncidents", [])):
                command("support-drivers", {"count": current["availableSupportDriverCount"]})
            if current["phaseCode"] == "Completed":
                break
            time.sleep(min(5, max(0, end - time.monotonic())))
        observation_finished = True
    except KeyboardInterrupt as error:
        errors.append(safe_error(stage, error))
        exit_code = 130
    except Exception as error:
        errors.append(safe_error(stage, error))
        exit_code = 1
    finally:
        # Each diagnostic read is independent: a stopped host or one broken endpoint cannot erase prior evidence.
        for name in ("current", "decisions", "incidents"):
            if not token:
                reads[name] = {"available": False, "reason": "AuthenticationUnavailable"}
                continue
            try:
                value = request("GET", api + name, timeout=10)
                reads[name] = {"available": True, "source": "CanonicalApi"}
                if name == "current":
                    current = value
                    authenticated_read_observed = True
                else:
                    final_reads[name] = value
            except Exception as error:
                reads[name] = {"available": False, "error": safe_error("final-" + name, error)}
                errors.append(reads[name]["error"])
                if exit_code == 0:
                    exit_code = 1
        if reads["current"]["available"]:
            applied = {str(value).lower() for value in current.get("appliedCommandIds", [])}
            for attempt in commands:
                if attempt["outcome"] == "Unknown" and attempt["clientRequestId"].lower() in applied:
                    attempt["outcome"] = "AppliedCanonicalRecovered"
                    attempt["confirmationRecoveredInFinally"] = True
        evidence = {"schemaVersion": "dispatch-training.http-verification.r58", "actualApis": True,
                    "physicalDevice": False, "acceleratedClock": False, "authenticatedReadObserved": authenticated_read_observed,
                    "observationFinished": observation_finished, "requestedObservationSeconds": args.observe_seconds,
                    "samples": samples, "final": current, "finalSource": "CanonicalApi" if reads["current"]["available"] else "LastObserved" if current is not None else "Unavailable",
                    "decisions": final_reads["decisions"], "incidents": final_reads["incidents"],
                    "readAvailability": reads, "commands": commands, "errors": errors, "exitCode": exit_code}
        token = None
        try:
            write_evidence(target, evidence)
        except Exception as error:
            print(json.dumps({"evidenceSaved": False, "error": safe_error("evidence-write", error)}, ensure_ascii=False), flush=True)
            return 1
        print(json.dumps({"evidencePath": str(target), "phase": (current or {}).get("phaseCode"),
                          "counts": (current or {}).get("counts"), "exitCode": exit_code,
                          "finalSource": evidence["finalSource"], "readAvailability": reads}, ensure_ascii=False), flush=True)
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
