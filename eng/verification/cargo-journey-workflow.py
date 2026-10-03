"""Actual local cargo HTTP/DB journey; synthetic photos are Simulation fixtures.

Uses only Python's standard library. Credentials and all results remain under
artifacts/local/cargo-warehouse-journey-r1; the existing private root workflow
and its results are never read as successful state or overwritten.
"""

import argparse
import base64
import concurrent.futures
import datetime as dt
import json
import math
import pathlib
import sys
import threading
import urllib.error
import urllib.parse
import urllib.request
import uuid


REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
ARTIFACT_ROOT = REPO_ROOT / "artifacts/local/cargo-warehouse-journey-r1"
RESULT_ROOT = ARTIFACT_ROOT / "workflow"
BASE_URL = "http://127.0.0.1:5322/"
DATABASE = "ssalddel_cargo_journey"
STAGES = ("prepare", "arrive", "handoff", "pickup", "complete")
EXPECTED_ACCOUNTS = {
    "shipper": "cargo-journey-shipper",
    "driver": "cargo-journey-driver",
    "warehouse": "cargo-journey-warehouse",
    "admin": "cargo-journey-admin",
}
EXPECTED_FIXTURE = {
    "warehouseId": 910001,
    "inboundItemId": 910003,
    "outboundPlanId": 910004,
    "quantity": 9,
    "expectedRequestId": "warehouse-outbound-910004",
}


def require(condition, message):
    # Do not use Python assert: -O must never disable journey guards.
    if not condition:
        raise RuntimeError(message)


def value(obj, name):
    require(isinstance(obj, dict), "Expected a JSON object.")
    for key, result in obj.items():
        if key.casefold() == name.casefold():
            return result
    raise RuntimeError(f"Required response field missing: {name}")


def utc_z(moment):
    return moment.astimezone(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def db_utc(text):
    require(isinstance(text, str) and bool(text), "Required database UTC timestamp missing.")
    moment = dt.datetime.fromisoformat(text.replace("Z", "+00:00"))
    # MySQL DateTime JSON may omit its zone; this field's contract is stored UTC.
    if moment.tzinfo is None:
        moment = moment.replace(tzinfo=dt.timezone.utc)
    return moment.astimezone(dt.timezone.utc)


class LocalRedirectsOnly(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        parsed = urllib.parse.urlsplit(newurl)
        require(parsed.scheme == "http" and parsed.hostname == "127.0.0.1" and parsed.port == 5322,
                "A response attempted to redirect outside the isolated cargo host.")
        return super().redirect_request(req, fp, code, msg, headers, newurl)


class Journey:
    def __init__(self, mode, pricing_distance_km=None):
        self.mode = mode
        self.pricing_distance_km = pricing_distance_km
        self.trace = []
        self.tokens = {}
        self.secrets = {}
        self.ready = {}
        self.state = None
        self.attempt_started = False
        self.lock = threading.Lock()
        self.state_path = RESULT_ROOT / "workflow-state.json"
        stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        self.result_path = RESULT_ROOT / f"http-{mode}-{stamp}-{uuid.uuid4().hex[:8]}.json"
        # Loopback credentials must not be sent through a machine HTTP proxy.
        self.http = urllib.request.build_opener(urllib.request.ProxyHandler({}), LocalRedirectsOnly())

    def redact(self, data):
        if isinstance(data, dict):
            return {
                key: "[omitted]" if any(part in key.casefold() for part in
                    ("password", "token", "authorization", "secret", "accesskey", "aeskey", "hashsalt"))
                else self.redact(item) for key, item in data.items()
            }
        if isinstance(data, list):
            return [self.redact(item) for item in data]
        if isinstance(data, str):
            for secret in [*self.secrets.values(), *self.tokens.values()]:
                if secret:
                    data = data.replace(secret, "[omitted]")
        return data

    def load(self):
        env_path = ARTIFACT_ROOT / "environment/.env"
        for line in env_path.read_text(encoding="utf-8-sig").splitlines():
            if line.startswith("CARGO_JOURNEY_") and "=" in line:
                name, secret = line.split("=", 1)
                self.secrets[name] = secret
        require(all(self.secrets.get(name) for name in
                    ("CARGO_JOURNEY_ACCOUNT_PASSWORD", "CARGO_JOURNEY_ACCESS_KEY")),
                "Required private cargo credentials are missing.")
        self.ready = json.loads((ARTIFACT_ROOT / "ready.json").read_text(encoding="utf-8-sig"))
        require(self.ready.get("schemaVersion") == "cargo-journey-verification.ready.r1", "Unexpected ready schema.")
        require(self.ready.get("baseUrl", "").rstrip("/") == BASE_URL.rstrip("/"), "Isolated cargo URL mismatch.")
        require(self.ready.get("database") == DATABASE and self.ready.get("environment") == "Development"
                and self.ready.get("executionMode") == "Simulation" and self.ready.get("synthetic") is True,
                "Only the dedicated Development/Simulation cargo fixture is permitted.")
        require(self.ready.get("accounts") == EXPECTED_ACCOUNTS, "Synthetic account binding mismatch.")
        require(all(self.ready.get(key) == expected for key, expected in EXPECTED_FIXTURE.items()),
                "Cargo fixture binding mismatch.")
        require(self.ready.get("vehicleType") == "1톤 카고", "Synthetic driver vehicle type mismatch.")
        run_id = self.ready.get("runStableId", "")
        require(run_id.startswith("cargo-journey:"), "Cargo runStableId prefix mismatch.")
        uuid.UUID(run_id.removeprefix("cargo-journey:"))

    def request(self, actor, method, path, body=None, expected=(200,), raw=None, content_type=None, control_key=True):
        require(path.startswith(("api/v1/", "verification/cargo/")) and "://" not in path,
                "Only fixed local cargo API paths are permitted.")
        headers = {"X-App-Key": {"warehouse": "WarehouseManagerApp", "driver": "DriverApp",
                   "shipper": "ShipperApp"}.get(actor, "CargoJourneyVerification")}
        if actor in self.tokens:
            headers["Authorization"] = "Bearer " + self.tokens[actor]
        if path.startswith("verification/") and control_key:
            headers["x-cargo-journey-key"] = self.secrets["CARGO_JOURNEY_ACCESS_KEY"]
        payload = raw
        if body is not None:
            payload = json.dumps(body, ensure_ascii=False).encode("utf-8")
            headers["Content-Type"] = "application/json; charset=utf-8"
        if content_type:
            headers["Content-Type"] = content_type
        req = urllib.request.Request(BASE_URL + path, data=payload, headers=headers, method=method)
        try:
            with self.http.open(req, timeout=45) as response:
                status, data = response.status, response.read()
        except urllib.error.HTTPError as error:
            status, data = error.code, error.read()
        try:
            result = json.loads(data) if data else None
        except (ValueError, UnicodeDecodeError):
            result = {"responseFormat": "non-json", "responseBytes": len(data)}
        entry = {"actor": actor, "method": method, "path": path, "status": status}
        # Login responses may contain JWT/refresh material: never persist them.
        if path != "api/v1/auth/login":
            entry["response"] = self.redact(result)
        with self.lock:
            self.trace.append(entry)
            print(json.dumps({key: entry[key] for key in ("actor", "method", "path", "status")}, ensure_ascii=False), flush=True)
        require(status in expected, f"Unexpected HTTP status {status} for {path}; see private HTTP result.")
        return result

    def login(self):
        for actor, name in self.ready["accounts"].items():
            result = self.request(actor, "POST", "api/v1/auth/login", {
                "userNameOrEmail": name, "password": self.secrets["CARGO_JOURNEY_ACCOUNT_PASSWORD"]})
            token = result.get("accessToken") or result.get("token")
            require(isinstance(token, str) and bool(token), "Login token missing.")
            self.tokens[actor] = token

    def proof(self):
        evidence = self.request("admin", "GET", "verification/cargo/database-proof")
        current_ready = json.loads((ARTIFACT_ROOT / "ready.json").read_text(encoding="utf-8-sig"))
        require(evidence.get("runStableId") == self.ready["runStableId"] == current_ready.get("runStableId"),
                "runStableId changed; this result cannot be joined to the current cargo run.")
        require(evidence.get("database") == DATABASE, "Database proof binding mismatch.")
        require(all(evidence.get("fixture", {}).get(key) == self.ready[key] for key in
                    ("warehouseId", "inboundItemId", "outboundPlanId")), "Database fixture binding mismatch.")
        require(evidence["outbound"]["quantity"] == self.ready["quantity"], "Outbound quantity binding mismatch.")
        return evidence

    def write_state(self):
        RESULT_ROOT.mkdir(parents=True, exist_ok=True)
        self.state_path.write_text(json.dumps(self.redact(self.state), ensure_ascii=False, indent=2), encoding="utf-8")

    def begin_stage(self):
        if self.mode == "prepare":
            require(not self.state_path.exists(), "A workflow state already exists; prepare requires a new sample and archived prior results.")
            before = self.proof()
            require(before["outbound"]["state"] == "출고준비중" and not before["outbound"]["requestId"]
                    and before["request"] is None and before["transport"] is None,
                    "Prepare requires an unused outbound fixture; existing work cannot be resumed as a new prepare.")
            require(all(before[key] == 0 for key in
                        ("requestCount", "transportCount", "allocationCount", "outboundMovementCount", "outboundHistoryCount"))
                    and before["stock"]["available"] == self.ready["quantity"] and before["stock"]["reserved"] == 0,
                    "Prepare requires pristine request counts and available synthetic stock.")
            self.state = {"schemaVersion": "cargo-journey-workflow.r1", "runStableId": self.ready["runStableId"],
                          "fixture": EXPECTED_FIXTURE, "completedStages": [], "before": before,
                          "deviceUiProof": False, "realCargoPhotoProof": False, "productionDispatcherProof": False}
        else:
            require(self.state_path.exists(), "Tracked workflow prepare state is missing; private root state is not substituted.")
            self.state = json.loads(self.state_path.read_text(encoding="utf-8-sig"))
            require(self.state.get("schemaVersion") == "cargo-journey-workflow.r1"
                    and self.state.get("runStableId") == self.ready["runStableId"]
                    and self.state.get("fixture") == EXPECTED_FIXTURE, "Workflow state binding mismatch.")
            required_stages = list(STAGES[:STAGES.index(self.mode)])
            require(self.state.get("completedStages") == required_stages,
                    "Stages must succeed in order; repeated or failed stages cannot be treated as successful resume.")
            require(self.state.get("lastAttempt", {}).get("status") == "Succeeded", "Prior attempt failed; inspect its private evidence before another mutation.")
            self.assert_bound(self.proof())
        self.state["lastAttempt"] = {"mode": self.mode, "status": "Running", "atUtc": utc_z(dt.datetime.now(dt.timezone.utc))}
        self.attempt_started = True
        self.write_state()

    def assert_bound(self, evidence):
        require(evidence["outbound"]["requestId"] == evidence["request"]["id"]
                == evidence["transport"]["requestId"] == self.state["requestId"] == self.ready["expectedRequestId"],
                "Request IDs differ across cargo roles and the fixture.")
        require(evidence["transport"]["id"] == self.state["transportId"]
                and all(evidence[key] == 1 for key in ("requestCount", "transportCount", "allocationCount")),
                "Transport ID or single request/transport/allocation proof mismatch.")

    def prepare(self):
        plan = self.ready["outboundPlanId"]
        review = self.request("warehouse", "GET", f"api/v1/warehouse-operations/outbound-plan-reviews/{plan}")
        require(value(review, "canStartTransportRequestDraft"), "Fixture cannot start the existing request draft.")
        now = dt.datetime.now(dt.timezone.utc)
        body = {"출고예정Id": plan, "입고상품Id": self.ready["inboundItemId"], "요청수량": self.ready["quantity"],
                "하차지주소": "합성 검증 하차지 도로명 주소", "하차지상세주소": "Simulation 하차 도크",
                "하차담당자명": "합성 수령 담당자", "하차연락처": "010-0000-0002", "화물종류": "격리 검증 상온 상자",
                "차량종류": self.ready["vehicleType"], "희망상차일시": utc_z(now + dt.timedelta(minutes=30)),
                "희망도착일시": utc_z(now + dt.timedelta(hours=2)), "취급메모": "Simulation 표본. 실제 영업·운송 아님."}
        self.state["draft"] = body
        self.write_state()
        created = self.request("warehouse", "POST", "api/v1/warehouse-operations/inventory/reconsignment", body)
        rid = value(created, "의뢰Id")
        require(rid == self.ready["expectedRequestId"], "Created request differs from the outbound fixture.")
        self.state.update(requestId=rid, created=created)
        self.write_state()
        replay = self.request("warehouse", "POST", "api/v1/warehouse-operations/inventory/reconsignment", body)
        require(value(replay, "의뢰Id") == rid and value(replay, "멱등재시도여부"), "Draft replay was not idempotent.")
        pending = self.proof()
        accept_path = f"api/v1/driver/dispatch-actions/{rid}/accept"
        warnings = {"acknowledgedWarningCodes": ["FreightVehicleAdvisory"]}
        blocked = self.request("driver", "POST", accept_path, warnings, expected=(409,))
        require(blocked.get("errorCode") == "FreightSettlementNotReady", "Pending settlement acceptance guard mismatch.")
        blocked = self.request("admin", "POST", "verification/cargo/offer", {"requestId": rid}, expected=(409,))
        require(blocked.get("errorCode") == "CargoJourneyPaymentApprovalRequired", "Pending payment offer guard mismatch.")
        blocked = self.request("admin", "POST", "verification/cargo/offer", {"requestId": "foreign-synthetic-request"}, expected=(409,))
        require(blocked.get("errorCode") == "CargoJourneyRequestBindingMismatch", "Foreign fixture offer guard mismatch.")
        self.request("anonymous", "POST", "verification/cargo/offer", {"requestId": rid}, expected=(401,), control_key=False)
        self.request("anonymous", "GET", "api/v1/driver/transports", expected=(401,))
        self.request("shipper", "GET", "api/v1/driver/transports", expected=(403,))
        after_blocked = self.proof()
        fields = ("stock", "outbound", "request", "transport", "requestCount", "transportCount", "allocationCount",
                  "outboundMovementCount", "outboundHistoryCount")
        require(all(after_blocked[field] == pending[field] for field in fields), "Blocked actions changed cargo business state.")
        self.state["pendingGuardProof"] = after_blocked
        if self.pricing_distance_km is not None:
            # Explicit synthetic distance input; this never claims a real road route.
            pricing = {"요금옵션": {"예상거리Km": self.pricing_distance_km, "최종운임": 1,
                                    "거리계산방식": "forged-client-basis", "단가출처": "forged-client-rate"}}
            quote = self.request("shipper", "POST", "api/v1/shipper/requests/fare-estimate",
                                 {"차량종류": body["차량종류"], "예상거리Km": self.pricing_distance_km})
            saved = self.request("shipper", "PUT", f"api/v1/shipper/requests/{rid}", pricing)
            saved_again = self.request("shipper", "PUT", f"api/v1/shipper/requests/{rid}", pricing)
            require(value(saved, "최종운임") == value(saved_again, "최종운임") == value(quote, "최종운임"), "Server quote was not persisted or replay changed its amount.")
            require(value(saved, "요금옵션")["거리계산방식"] == "입력거리", "Client route basis was trusted.")
            queried = self.request("shipper", "GET", f"api/v1/shipper/requests/{rid}")
            require(value(queried, "요금옵션")["예상거리Km"] == value(quote, "예상거리Km"), "Persisted quote distance requery mismatch.")
            price_proof = self.proof()
            require(price_proof["pricingCount"] == 1 and price_proof["pricing"]["distanceBasis"] == "입력거리", "Price composition or source mismatch.")
            require(price_proof["pricing"]["expectedDistanceKm"] == value(quote, "예상거리Km"), "Database quote distance mismatch.")
            self.state.update(quoteProof=quote, pricingDatabaseProof=price_proof)
        self.request("shipper", "POST", f"api/v1/shipper/requests/{rid}/settlement/postpay/approve", {"승인메모": "Simulation 명시 후불 승인"})
        self.request("admin", "POST", "verification/cargo/offer", {"requestId": rid}, expected=(200, 202))
        self.request("driver", "POST", accept_path, warnings)
        self.request("driver", "POST", accept_path, warnings)
        if self.pricing_distance_km is not None:
            locked = self.request("shipper", "PUT", f"api/v1/shipper/requests/{rid}",
                                  {"요금옵션": {"예상거리Km": self.pricing_distance_km + 1}}, expected=(409,))
            require(locked.get("errorCode") == "FreightPricingLocked", "Accepted pricing edit guard mismatch.")
        current = self.request("driver", "GET", "api/v1/driver/transports/current")
        tid = value(current, "id")
        self.state["transportId"] = tid
        self.write_state()
        detail = self.request("driver", "GET", f"api/v1/driver/transports/{tid}")
        if self.pricing_distance_km is not None:
            quote = self.state["quoteProof"]
            require(value(current, "운임") == value(detail, "운임") == value(quote, "최종운임"), "Accepted driver fare differs from the persisted quote.")
            require(value(current, "예상거리Km") == value(detail, "예상거리Km") == value(quote, "예상거리Km"), "Driver quote distance differs across pages.")
            require(value(detail, "거리계산방식") == "입력거리", "Driver route source mismatch.")
        require(value(detail, "수령자명") == body["하차담당자명"] and value(detail, "수령자연락처") == body["하차연락처"], "Recipient handoff detail mismatch.")
        require(db_utc(value(detail, "상차시간창시작일시")) == db_utc(body["희망상차일시"]), "Pickup UTC time window mismatch.")
        require(db_utc(value(detail, "하차시간창시작일시")) == db_utc(body["희망도착일시"]), "Dropoff UTC time window mismatch.")
        self.request("warehouse", "GET", f"api/v1/warehouse-operations/outbound-plan-reviews/{plan}")
        evidence = self.proof()
        self.assert_bound(evidence)
        require(evidence["request"]["settlementState"] == "후불승인완료" and evidence["request"]["paymentState"] == "결제대기", "Explicit postpay approval proof mismatch.")
        self.state.update(driverDetail=detail, preparedProof=evidence)

    def arrive(self):
        tid = self.state["transportId"]
        self.request("driver", "POST", f"api/v1/driver/transports/{tid}/arrive-pickup")
        self.request("driver", "GET", f"api/v1/driver/transports/{tid}")
        self.assert_bound(self.proof())

    def handoff(self):
        tid, plan = self.state["transportId"], self.ready["outboundPlanId"]
        self.request("driver", "POST", f"api/v1/driver/transports/{tid}/pickup-complete", {}, expected=(409,))
        body = {"driverIdentityConfirmed": True, "vehicleConfirmed": True, "cargoReleasedConfirmed": True, "memo": "Simulation 상품 인계 확인"}
        path = f"api/v1/warehouse-operations/outbound-plan-reviews/{plan}/handoff-complete"
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            results = list(executor.map(lambda _: self.request("warehouse", "POST", path, body), range(2)))
        require(all(value(result, "transportRequestId") == self.state["requestId"] for result in results)
                and sum(bool(value(result, "idempotentReplay")) for result in results) == 1, "Concurrent handoff was not one mutation plus one replay.")
        require(value(self.request("warehouse", "POST", path, body), "idempotentReplay"), "Handoff replay was not idempotent.")
        evidence = self.proof()
        self.assert_bound(evidence)
        self.assert_stock_once(evidence)
        self.state["handoffProof"] = evidence

    def upload(self, kind):
        # One synthetic PNG proves the upload contract, not a photographed cargo.
        png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jmT8AAAAASUVORK5CYII=")
        boundary = "cargo-journey-" + uuid.uuid4().hex
        parts = []
        command = "TransportPickupComplete" if kind == "pickup" else "TransportDropoffComplete"
        for key, text in (("CommandName", command), ("ReferenceId", str(self.state["transportId"]))):
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{text}\r\n'.encode())
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="File"; filename="simulation-{kind}.png"\r\nContent-Type: image/png\r\n\r\n'.encode() + png + b"\r\n")
        parts.append(f"--{boundary}--\r\n".encode())
        return self.request("driver", "POST", "api/v1/files/upload", raw=b"".join(parts), content_type="multipart/form-data; boundary=" + boundary)

    def pickup(self):
        tid = self.state["transportId"]
        uploaded = self.upload("pickup")
        self.request("driver", "POST", f"api/v1/driver/transports/{tid}/pickup-complete", {
            "상차사진ObjectName": value(uploaded, "objectName"), "상차사진Url": value(uploaded, "url")})
        self.request("driver", "GET", f"api/v1/driver/transports/{tid}")
        self.request("shipper", "GET", f"api/v1/shipper/requests/{self.state['requestId']}")
        evidence = self.proof()
        self.assert_bound(evidence)
        self.assert_stock_once(evidence)

    @staticmethod
    def assert_stock_once(evidence):
        require(evidence["stock"]["available"] == 0 and evidence["stock"]["reserved"] == 0
                and evidence["outboundMovementCount"] == 1 and evidence["outboundHistoryCount"] == 1,
                "Stock must be consumed exactly once with one outbound movement and one history record.")

    def complete(self):
        tid = self.state["transportId"]
        self.request("driver", "POST", f"api/v1/driver/transports/{tid}/arrive-dropoff")
        uploaded = self.upload("dropoff")
        self.request("driver", "POST", f"api/v1/driver/transports/{tid}/complete", {
            "하차사진ObjectName": value(uploaded, "objectName"), "하차사진Url": value(uploaded, "url")})
        self.request("driver", "GET", f"api/v1/driver/transports/{tid}")
        self.request("driver", "GET", "api/v1/driver/transports/current", expected=(404,))
        self.request("driver", "GET", "api/v1/driver/transports")
        self.request("shipper", "GET", f"api/v1/shipper/requests/{self.state['requestId']}")
        self.request("warehouse", "GET", f"api/v1/warehouse-operations/outbound-plan-reviews/{self.ready['outboundPlanId']}")
        evidence = self.proof()
        self.assert_bound(evidence)
        self.assert_stock_once(evidence)
        require(evidence["request"]["paymentState"] == "결제대기" and evidence["transport"]["state"] == "인수완료"
                and evidence["mongo"]["documentCount"] == 1, "Final transport/payment/Mongo proof mismatch.")
        self.state.update(completedProof=evidence, httpDbJourneyCompleted=True)

    def run(self):
        self.load()
        self.login()
        if self.mode == "proof":
            evidence = self.proof()
            if self.state_path.exists():
                saved = json.loads(self.state_path.read_text(encoding="utf-8-sig"))
                require(saved.get("runStableId") == self.ready["runStableId"], "Saved workflow runStableId mismatch.")
                if "transportId" in saved:
                    self.state = saved
                    self.assert_bound(evidence)
            return
        self.begin_stage()
        getattr(self, self.mode)()
        self.state["completedStages"].append(self.mode)
        self.state["lastAttempt"]["status"] = "Succeeded"
        self.write_state()

    def save_result(self, error=None):
        RESULT_ROOT.mkdir(parents=True, exist_ok=True)
        if error is not None and self.attempt_started:
            self.state["lastAttempt"].update(status="Failed", errorType=type(error).__name__, error=self.redact(str(error)))
            self.state["httpDbJourneyCompleted"] = False
            self.write_state()
        result = {"schemaVersion": "cargo-journey-http-result.r1", "mode": self.mode,
                  "runStableId": self.ready.get("runStableId"), "status": "Failed" if error else "Succeeded",
                  "httpDbJourneyCompleted": bool(not error and self.mode == "complete"),
                  "deviceUiProof": False, "realCargoPhotoProof": False, "productionDispatcherProof": False,
                  "trace": self.trace}
        if error:
            result.update(errorType=type(error).__name__, error=self.redact(str(error)))
        self.result_path.write_text(json.dumps(self.redact(result), ensure_ascii=False, indent=2), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=(*STAGES, "proof"))
    parser.add_argument("--then", nargs="+", choices=STAGES,
                        help="Run immediately following stages with the same in-memory authentication; no token file.")
    parser.add_argument("--pricing-distance-km", type=float, help="Prepare only: explicit synthetic distance for persisted server quote checks.")
    args = parser.parse_args()
    if args.pricing_distance_km is not None and (args.mode != "prepare" or not math.isfinite(args.pricing_distance_km) or args.pricing_distance_km <= 0):
        parser.error("--pricing-distance-km requires prepare and a finite positive synthetic distance.")
    modes = [args.mode, *(args.then or [])]
    if args.then and (args.mode == "proof" or modes != list(STAGES)[STAGES.index(args.mode):STAGES.index(args.mode) + len(modes)]):
        parser.error("--then must contain only the immediately following journey stages.")
    previous = None
    for mode in modes:
        journey = Journey(mode, args.pricing_distance_km if mode == "prepare" else None)
        error = None
        try:
            journey.load()
            if previous is not None:
                require(previous.ready["runStableId"] == journey.ready["runStableId"], "Host changed during stage batch.")
                journey.tokens = dict(previous.tokens)
                # Stage guards still run; only the existing authenticated session is reused.
                journey.login = lambda: None
            journey.run()
        except Exception as failure:
            error = failure
        journey.save_result(error)
        print(json.dumps({"mode": mode, "status": "Failed" if error else "Succeeded",
                          "errorType": type(error).__name__ if error else None,
                          "resultPath": str(journey.result_path.relative_to(REPO_ROOT))}, ensure_ascii=False), flush=True)
        if error:
            return 1
        previous = journey
    return 0


if __name__ == "__main__":
    sys.exit(main())
