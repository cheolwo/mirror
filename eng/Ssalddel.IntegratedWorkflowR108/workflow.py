"""전용 r108 합성 계정의 제품 HTTP·원장·응답 유실 후 정본 재조회 검증.

원시 인증/연락처/요청 본문을 결과에 쓰지 않는다. 불명확한 POST는 자동 반복하지 않는다.
--pause-after-accept 는 서버 재시작 전후를 분리한다. 새 실행은 같은 private journal을 읽는다.
"""
import argparse
import base64
import datetime as dt
import hashlib
import json
import pathlib
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

REPO = pathlib.Path(__file__).resolve().parents[2]
BASE = "http://127.0.0.1:5378"
NAMES = {"driver": "cargo-journey-driver", "shipper": "cargo-journey-shipper", "warehouse": "cargo-journey-warehouse",
         "admin": "cargo-journey-admin", "food-driver": "driver1@ssalddel.local", "restaurant": "restaurant101", "orderer": "field-orderer"}
APPS = {"driver": "DriverApp", "food-driver": "FDriverApp", "shipper": "ShipperApp", "warehouse": "WarehouseManagerApp",
        "restaurant": "RestaurantDeskApp", "orderer": "OrdererApp", "admin": "SsalddelAdminApp"}
STANDALONE_CARGO_CASES = frozenset(f"cargo-phone-quick-{number:02d}" for number in range(2, 6))

def check(condition, code):
    if not condition:
        raise RuntimeError(code)

def get(obj, key, default=None):
    return next((v for k, v in obj.items() if k.casefold() == key.casefold()), default) if isinstance(obj, dict) else default

def canonical_run_id(value):
    """구판 camel/Pascal 표기는 읽되 중복 별칭에서 임의의 값을 택하지 않는다."""
    matches = [v for k, v in value.items() if k.casefold() == "runstableid"] if isinstance(value, dict) else []
    check(len(matches) == 1 and isinstance(matches[0], str) and bool(matches[0]), "R108SingleRunStableIdRequired")
    return matches[0]

def standalone_cargo_case(state, journal_case):
    """기존 journal은 종전 marker02를 유지하고 새 허용 사례만 각각 결속한다."""
    case_key = state.get("cargoCaseKey")
    if case_key is None:
        legacy_request = bool(state.get("requestId") or state.get("attempts"))
        case_key = journal_case if not legacy_request and journal_case in STANDALONE_CARGO_CASES else "cargo-phone-quick-02"
    check(isinstance(case_key, str) and case_key in STANDALONE_CARGO_CASES, "CargoCaseKeyNotAllowed")
    if state.get("journalCase") is not None:
        check(state["journalCase"] == journal_case, "CargoJournalCaseMismatch")
        expected = journal_case if journal_case in STANDALONE_CARGO_CASES else "cargo-phone-quick-02"
        check(case_key == expected, "CargoMarkerJournalBindingMismatch")
    return case_key

def cargo_transport_identity(value, request_id, transport_id=None):
    """제품 계약의 운송번호가 의뢰 ID다. 존재하는 다른 운송을 복구 성공으로 쓰지 않는다."""
    actual_id = get(value, "id")
    check(type(actual_id) is int and actual_id > 0 and get(value, "운송번호") == request_id,
          "CargoTransportRequestIdentityMismatch")
    if get(value, "의뢰Id") is not None:
        check(get(value, "의뢰Id") == request_id, "CargoTransportRequestIdentityMismatch")
    if transport_id is not None:
        check(actual_id == transport_id, "CargoTransportIdChanged")
    return actual_id

def cargo_schema_boundaries(ready):
    mode = get(ready, "schemaMode", "CurrentModel")
    check(mode in ("CurrentModel", "MigrationFirst"), "R108SchemaModeBindingMismatch")
    return {"schemaMode": mode, "freshEnsureCreatedNotMigration": mode == "CurrentModel",
            "isolatedMigrationChainApplied": mode == "MigrationFirst", "operationalMigrationVerified": False}

def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)

def now():
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")

def current_cargo_recommendation(proposals, request_id):
    check(isinstance(proposals, list), "CargoRecommendationResponseShapeInvalid")
    matches = [item for item in proposals if get(item, "의뢰Id") == request_id]
    check(len(matches) <= 1, "CargoRecommendationDuplicateRequest")
    check(bool(matches), "CargoRecommendationHeld_NoCurrentOffer_CheckCandidateEvidence")
    return matches[0]


def validate_live_boundary(root, ready, environment, preparation=None):
    """같은 GUID 실행의 준비/서버/비공개 입력을 대조하며 HTTP나 상태 변경은 하지 않는다."""
    root = pathlib.Path(root).resolve()
    owned = (REPO / "artifacts/local/integrated-workflow-r108").resolve()
    check(root.parent == owned, "OwnedR108OutputRequired")
    database = get(ready, "database")
    match = re.fullmatch(r"(?P<prefix>mirror_workflow_r108_|r108m_)(?P<id>[a-f0-9]{32})", database) if isinstance(database, str) else None
    check(get(ready, "schemaVersion") == "integrated-workflow-r108.ready.r1"
          and get(ready, "baseUrl") == BASE and get(ready, "executionMode") == "Simulation"
          and get(ready, "externalHttpBlocked") is True and bool(match), "R108LiveBoundaryRequired")
    run_id = "integrated-r108:" + match["id"]
    check(root.name == match["id"] and canonical_run_id(ready) == run_id, "R108RunBindingMismatch")
    environment_root = get(environment, "R108_ARTIFACT_ROOT")
    check(get(environment, "R108_DATABASE") == database and isinstance(environment_root, str)
          and pathlib.Path(environment_root).resolve() == root, "R108EnvironmentBindingMismatch")
    ready_mode = get(ready, "schemaMode", "CurrentModel")
    environment_mode = get(environment, "R108_SCHEMA_MODE", "CurrentModel")
    short_name = match["prefix"] == "r108m_"
    expected_mode = "MigrationFirst" if short_name else "CurrentModel"
    check(ready_mode == environment_mode == expected_mode, "R108SchemaModeBindingMismatch")
    if short_name:
        check(get(ready, "isolatedMigrationChainApplied") is True and preparation is not None,
              "R108AppliedMigrationPreparationRequired")
    if preparation is not None:
        preparation_root = get(preparation, "outputRoot")
        check(get(preparation, "schemaVersion") == "integrated-workflow-r108.preparation.r1"
              and get(preparation, "status") == "FreshDedicatedDatabasePrepared"
              and get(preparation, "database") == database and canonical_run_id(preparation) == run_id
              and get(preparation, "schemaMode") == expected_mode and isinstance(preparation_root, str)
              and pathlib.Path(preparation_root).resolve() == root, "R108PreparationBindingMismatch")

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None

class Workflow:
    def __init__(self, options):
        self.options = options
        self.root = pathlib.Path(options.output_root).resolve()
        check(self.root.is_relative_to(REPO / "artifacts/local/integrated-workflow-r108"), "OwnedR108OutputRequired")
        self.ready = read(self.root / "ready.json")
        self.run_id = canonical_run_id(self.ready)
        self.env = read(self.root / ".private/environment.json")
        preparation_path = self.root / "preparation.json"
        validate_live_boundary(self.root, self.ready, self.env, read(preparation_path) if preparation_path.exists() else None)
        self.accounts = {get(x, "UserName"): x for x in read(self.root / ".private/accounts.json")["accounts"]}
        case = options.case or options.scenario
        check(bool(re.fullmatch(r"[a-z0-9][a-z0-9-]{0,47}", case)), "SafeCaseRequired")
        self.path = self.root / "workflow" / (case + ".json")
        self.state = read(self.path) if self.path.exists() else {"scenario": options.scenario, "runStableId": self.run_id, "journalCase": case, "attempts": {}, "trace": [], "checks": []}
        check(canonical_run_id(self.state) == self.run_id, "RunStableIdChanged")
        check(self.state.get("scenario") == options.scenario, "JournalScenarioChanged")
        check(self.state.get("journalCase", case) == case, "CargoJournalCaseMismatch")
        self.journal_case = case
        self.tokens = {}
        self.http = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())

    def call(self, actor, method, path, body=None, expected=(200,), raw=None, content_type=None):
        check(path.startswith(("/api/v1/", "/verification/")) and ".." not in path and "://" not in path, "LoopbackOnly")
        headers = {"X-App-Key": APPS.get(actor, "R108Verification")}
        if actor in self.tokens:
            headers["Authorization"] = "Bearer " + self.tokens[actor]
        if path.startswith("/verification/"):
            headers["x-usb-r67-key"] = self.env["USB_R74_ACCESS_KEY"]
            headers["x-cargo-journey-key"] = self.env["USB_R74_ACCESS_KEY"]
            headers["x-r108-key"] = self.env["USB_R74_ACCESS_KEY"]
        data = raw
        if body is not None:
            data = json.dumps(body, ensure_ascii=False).encode("utf-8")
            headers["Content-Type"] = "application/json; charset=utf-8"
        if content_type:
            headers["Content-Type"] = content_type
        request = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
        try:
            with self.http.open(request, timeout=60) as response:
                status, raw_result = response.status, response.read(4 * 1024 * 1024)
        except urllib.error.HTTPError as error:
            status, raw_result = error.code, error.read(4 * 1024 * 1024)
        try:
            result = json.loads(raw_result) if raw_result else None
        except (ValueError, UnicodeDecodeError):
            result = None
        safe = {"actor": actor, "method": method, "path": path, "status": status,
                "code": get(result, "errorCode") or get(result, "code"), "atUtc": now()}
        if status >= 400:
            # Only validation field names and stable codes, no raw messages/private response body.
            errors = get(result, "errors")
            safe["validationFields"] = list(errors.keys())[:12] if isinstance(errors, dict) else []
            safe["resultKeys"] = list(result.keys())[:20] if isinstance(result, dict) else []
        self.state["trace"].append(safe)
        save(self.path, self.state)
        print(json.dumps(safe, ensure_ascii=False), flush=True)
        check(status in expected, f"UnexpectedHttp:{status}:{path}")
        return result

    def login(self, actor):
        if actor in self.tokens:
            return
        account = self.accounts[NAMES[actor]]
        result = self.call(actor, "POST", "/api/v1/auth/login", {"userNameOrEmail": NAMES[actor], "password": get(account, "Password")})
        self.tokens[actor] = get(result, "accessToken") or get(result, "token")
        check(bool(self.tokens[actor]), "LoginTokenMissing")

    def mutation(self, key, actor, method, path, body=None, expected=(200, 201, 204), discard=False):
        prior = self.state["attempts"].get(key)
        check(prior is None, "PriorPostAttempt_ReadCanonical_DoNotRepost:" + key)
        payload_hash = hashlib.sha256(json.dumps(body, sort_keys=True, ensure_ascii=False).encode()).hexdigest()
        self.state["attempts"][key] = {"state": "RunningOrUncertain", "path": path, "requestSha256": payload_hash, "atUtc": now()}
        save(self.path, self.state)
        result = self.call(actor, method, path, body, expected)
        rejected = self.state["trace"][-1]["status"] >= 400
        self.state["attempts"][key]["state"] = "ExpectedBusinessRejection" if rejected else "SuccessfulResponseDiscarded_CanonicalReadRequired" if discard else "HttpSucceeded"
        save(self.path, self.state)
        return None if discard else result

    def checked(self, code, condition=True):
        check(condition, code)
        self.state["checks"].append({"code": code, "passed": True, "atUtc": now()})
        save(self.path, self.state)

    def driver_start(self, food=False):
        actor = "food-driver" if food else "driver"
        self.login(actor)
        if "participation" not in self.state["attempts"]:
            participation = self.call(actor, "GET", "/api/v1/driver/work-participation")
            settings = get(participation, "settings", {})
            revision = get(settings, "revision", 0)
            self.mutation("participation", actor, "PUT", "/api/v1/driver/work-participation", {
                "expectedRevision": revision, "settings": {"revision": revision, "foodDelivery": food,
                    "quickDelivery": not food, "cargoTransport": not food, "directPicking": not food,
                    "vehicleKind": "Motorcycle" if food else "Truck", "maxConcurrentJobs": 1,
                    "capacityKg": 10 if food else 1000, "capacityLitres": 80 if food else 10000,
                    "receiveAdditionalOffers": True, "foodCargoSeparationConfirmed": False}})
        prefix = "/api/v1/driver/food-deliveries/work" if food else "/api/v1/driver/work"
        current = self.call(actor, "GET", prefix + "/status")
        if "start" not in self.state["attempts"]:
            if get(current, "status") == "운행중":
                self.state["attempts"]["start"] = {"state": "ExistingShiftObserved_NoStartPost", "atUtc": now()}
            else:
                self.mutation("start", actor, "POST", prefix + "/start", {"clientRequestId": str(uuid.uuid4()), "시작모드": "바로시작", "시작위치": "r108 합성 검증", "커뮤니티운행공개": False})
        self.call(actor, "GET", prefix + "/status")
        lat, lon = (37.580450, 127.087950) if food else (37.588, 127.085)
        self.call(actor, "POST", prefix + "/location", {"appKey": "FoodDeliveryDriverApp" if food else "CargoYongdalDriverApp", "위도": lat, "경도": lon, "정확도_m": 5, "운행상태": "운행중", "기록시각": now()})
        if "reception-on" not in self.state["attempts"]:
            self.mutation("reception-on", actor, "PUT", "/api/v1/driver/operational-dispatch/availability/intent", {"클라이언트요청Id": str(uuid.uuid4()), "수신의사Code": "On"})

    def food(self):
        self.driver_start(True)
        for actor in ("orderer", "restaurant"):
            self.login(actor)
        request_id = self.state.setdefault("createClientRequestId", str(uuid.uuid4()))
        save(self.path, self.state)
        if "food-create" not in self.state["attempts"]:
            public = self.call("orderer", "GET", "/api/v1/orderer/restaurants/101")
            menu = next(x for x in get(public, "메뉴목록") if get(x, "id") == 10101)
            self.mutation("food-create", "orderer", "POST", "/api/v1/food-orders", {
                "클라이언트요청Id": request_id, "음식점Id": 101, "메뉴가격확인필요": True, "결제수단": "현장결제",
                "수령인정보": {"수령인명": "r108 합성 수령인", "연락처": "010-0000-0000", "주소": "모바일 음식 배달 시험 수령지", "상세주소": "합성 검증", "주문자본인수령여부": True},
                "상품목록": [{"메뉴Id": 10101, "상품명": get(menu, "메뉴명"), "수량": 1, "단가": get(menu, "판매가")}]}, discard=True)
        order = self.call("orderer", "GET", "/api/v1/food-orders/client-requests/" + request_id)
        order_no = get(order, "주문번호"); self.checked("FoodCreateLostResponseRecoveredByClientRequestId", bool(order_no))
        self.state["orderNo"] = order_no
        encoded = urllib.parse.quote(order_no, safe="")
        if "restaurant-accept" not in self.state["attempts"]:
            self.mutation("restaurant-accept", "restaurant", "POST", f"/api/v1/food-orders/{encoded}/restaurant-acceptance", {
                "클라이언트요청Id": str(uuid.uuid4()), "음식점명": "사가정 한상 샘플", "음식점주소": "서울특별시 중랑구 사가정역 내부 검증권역",
                "음식점위도": 37.580450, "음식점경도": 127.087950, "조리예상분": 12, "즉시픽업가능여부": False, "수락메모": "r108 합성 업무"})
        if "food-offer" not in self.state["attempts"]:
            self.mutation("food-offer", None, "POST", "/verification/food/offer", {"orderNo": order_no})
        proof = self.call(None, "GET", "/verification/food/proof?orderNo=" + encoded)
        if not get(proof, "recommendedSyntheticDriver") and not get(proof, "confirmedSyntheticDriver") and get(proof, "recommendationRound") == 0:
            self.mutation("food-offer-after-profile", None, "POST", "/verification/food/offer", {"orderNo": order_no})
            proof = self.call(None, "GET", "/verification/food/proof?orderNo=" + encoded)
        self.checked("FoodCandidateEngineAndFrozenPayout", get(proof, "recommendedSyntheticDriver") or get(proof, "confirmedSyntheticDriver"))
        self.state["frozenPayoutKrw"] = get(proof, "frozenDriverPayout")
        if "food-accept" not in self.state["attempts"]:
            self.notifications()
            self.mutation("food-accept", "food-driver", "POST", f"/api/v1/driver/food-deliveries/offers/{encoded}/accept", discard=True)
        workspace = self.call("food-driver", "GET", "/api/v1/driver/food-deliveries/workspace/current")
        canonical = self.call(None, "GET", "/verification/food/proof?orderNo=" + encoded)
        self.checked("FoodAcceptLostResponseRecoveredCanonical", get(canonical, "confirmedSyntheticDriver"))
        self.state["acceptedWorkspaceKeys"] = list(workspace.keys()) if isinstance(workspace, dict) else []
        if self.options.pause_after_accept:
            self.state["pausedAfterAccept"] = True
            return
        for action, label in (("조리시작", "cook"), ("픽업준비", "ready")):
            if label not in self.state["attempts"]:
                inbox = self.call("restaurant", "GET", f"/api/v1/food-orders/restaurant/inbox/{encoded}")
                self.mutation(label, "restaurant", "POST", f"/api/v1/food-orders/{encoded}/restaurant-progress", {"클라이언트요청Id": str(uuid.uuid4()), "예상Revision": get(inbox, "revision"), "작업": action})
        if "arrive" not in self.state["attempts"]:
            self.mutation("arrive", "food-driver", "POST", f"/api/v1/driver/food-deliveries/offers/{encoded}/restaurant-arrival", {"클라이언트요청Id": str(uuid.uuid4())})
        for key, endpoint in (("pickup", "pickup-complete"), ("deliver", "delivery-complete")):
            if key not in self.state["attempts"]:
                if key == "deliver":
                    self.call("food-driver","POST","/api/v1/driver/food-deliveries/work/location",{"appKey":"FoodDeliveryDriverApp","위도":37.582000,"경도":127.090000,"정확도_m":5,"운행상태":"운행중","기록시각":now()})
                self.mutation(key, "food-driver", "POST", f"/api/v1/driver/food-deliveries/offers/{encoded}/{endpoint}")
        if "receipt" not in self.state["attempts"]:
            self.mutation("receipt", "orderer", "POST", f"/api/v1/food-orders/{encoded}/receipt-confirmation", {"클라이언트요청Id": str(uuid.uuid4()), "확인메모": "r108 합성 수령"})
        self.call("food-driver", "GET", "/api/v1/driver/food-deliveries/settlements/daily")
        final = self.call(None, "GET", "/verification/food/proof?orderNo=" + encoded)
        self.state["finalOrderState"] = get(final, "orderStatus")
        self.checked("FoodDeliveryAndSeparateReceiptCompleted", get(final, "confirmedSyntheticDriver") and get(final, "orderStatus") == "수령확인")
        self.state["completed"] = True

    def cargo_proof(self, rid, warehouse):
        path = "/verification/cargo/database-proof" if warehouse else "/verification/cargo/standalone-proof?requestId=" + urllib.parse.quote(rid, safe="")
        proof = self.call(None, "GET", path)
        check(canonical_run_id(proof) == self.run_id and get(proof, "database") == get(self.ready, "database"), "CanonicalDatabaseBindingMismatch")
        proof_request = get(get(proof, "request", {}), "id") if warehouse else get(proof, "requestId")
        check(proof_request == rid, "CargoProofRequestIdentityMismatch")
        if not warehouse:
            check(get(proof, "caseKey") == self.state["cargoCaseKey"], "CargoProofCaseMismatch")
        if self.state.get("transportId") is not None:
            proof_transport = get(get(proof, "transport", {}), "id") if warehouse else get(proof, "transportId")
            check(proof_transport == self.state["transportId"], "CargoProofTransportIdentityMismatch")
        return proof

    def upload(self, tid, pickup):
        key = "pickup-photo" if pickup else "delivery-photo"
        existing = self.state.get("photos", {}).get(key)
        if existing is not None:
            return existing
        check(key not in self.state["attempts"], "UncertainPhotoUploadRequiresCanonicalReceiptReview_NoReupload")
        png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jmT8AAAAASUVORK5CYII=")
        boundary = "r108-" + uuid.uuid4().hex
        command = "TransportPickupComplete" if pickup else "TransportDropoffComplete"
        parts = [f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{val}\r\n'.encode() for key, val in (("CommandName", command), ("ReferenceId", tid))]
        parts += [f'--{boundary}\r\nContent-Disposition: form-data; name="File"; filename="synthetic.png"\r\nContent-Type: image/png\r\n\r\n'.encode() + png + b"\r\n", f"--{boundary}--\r\n".encode()]
        self.state["attempts"][key] = {"state": "RunningOrUncertain", "pngSha256": hashlib.sha256(png).hexdigest(), "atUtc": now()}
        save(self.path,self.state)
        result = self.call("driver", "POST", "/api/v1/files/upload", raw=b"".join(parts), content_type="multipart/form-data; boundary=" + boundary)
        self.checked("SyntheticPhotoUpload_" + command, bool(get(result, "objectName")))
        self.state.setdefault("photos", {})[key] = {"objectName":get(result,"objectName"),"url":get(result,"url")}
        self.state["attempts"][key]["state"] = "UploadReceiptObserved"
        save(self.path,self.state)
        return result

    def cargo(self, warehouse):
        if not warehouse:
            self.state["cargoCaseKey"] = standalone_cargo_case(self.state, self.journal_case)
            save(self.path, self.state)
        self.driver_start(False)
        for actor in (("shipper", "warehouse") if warehouse else ("shipper",)):
            self.login(actor)
        rid = self.state.get("requestId")
        if rid is None:
            start = dt.datetime.now(dt.timezone.utc)
            if warehouse:
                self.call("warehouse", "GET", "/api/v1/warehouse-operations/outbound-plan-reviews/910004")
                created = self.mutation("cargo-create", "warehouse", "POST", "/api/v1/warehouse-operations/inventory/reconsignment", {
                    "출고예정Id": 910004, "입고상품Id": 910003, "요청수량": 9, "하차지주소": "합성 검증 하차지 도로명 주소", "하차지상세주소": "합성 도크",
                    "하차담당자명": "합성 수령 담당자", "하차연락처": "010-0000-0002", "화물종류": "격리 검증 상온 상자", "차량종류": "1톤 카고",
                    "희망상차일시": now(), "희망도착일시": (start + dt.timedelta(hours=2)).isoformat(), "취급메모": "r108 합성 업무"})
            else:
                consent = str(uuid.uuid4())
                self.mutation("consent", "shipper", "POST", "/api/v1/common/application-privacy-consents", {"증적Id": consent, "업무Code": "transport-proxy", "출처Code": "community-map", "동의문버전": "application-privacy-consent-draft-2026-08-04", "수집이용동의": True, "연령요건확인": True})
                def stop(address, lat, lon):
                    return {"주소": {"도로명주소": address, "상세주소": "", "위도": lat, "경도": lon}, "연락처": {"이름": "합성 담당자", "전화번호": "010-0000-0000"}, "시간창": {"시작일시": now(), "종료일시": (start + dt.timedelta(hours=2)).isoformat()}}
                created = self.mutation("cargo-create", "shipper", "POST", "/api/v1/shipper/requests", {
                    "신청개인정보동의증적Id": consent, "신청출처Code": "community-map", "운송방식": "퀵", "차량종류": "1톤 카고", "결제수단": "별도정산", "결제예정금액": 6000,
                    "정산조건": {"결제수단": 4, "정산시점": 2, "증빙방식": 0, "수납주체": 0},
                    "화물": {"화물종류": "USB r67 독립 소형 상자", "설명": "합성 검증", "수량": 1, "길이Mm": 200, "폭Mm": 200, "높이Mm": 100, "중량Kg": 1, "부피Cbm": 0.004, "팔레트개수": 0, "바닥회전가능여부": True, "적층가능여부": True, "온도조건": "상온"},
                    "픽업": stop("격리 검증 상차 창고", 37.588, 127.085), "하차": stop("합성 검증 하차지 도로명 주소", 37.590, 127.087),
                    "요금옵션": {"화주직접운임제안": True, "기사지급예정운임": 6000, "플랫폼수수료": 0, "요청사항": "USB r67 standalone cargo; case=" + self.state["cargoCaseKey"] + "; ActualMapRequest=false", "서비스레벨": "일반"}, "클라이언트요청Id": "shipper-app-" + uuid.uuid4().hex})
            rid = get(created, "의뢰Id"); check(bool(rid), "CargoCreateIdMissing")
            self.state["requestId"] = rid; save(self.path, self.state)
        encoded = urllib.parse.quote(rid, safe="")
        proof = self.cargo_proof(rid, warehouse)
        if "cargo-approve" not in self.state["attempts"]:
            if warehouse and "warehouse-route-and-price-confirmed" not in self.state["attempts"]:
                if "warehouse-unready-approval-blocked" not in self.state["attempts"]:
                    blocked = self.mutation("warehouse-unready-approval-blocked", "shipper", "POST",
                        f"/api/v1/shipper/requests/{encoded}/settlement/postpay/approve",
                        {"승인메모": "합성 검증 · 준비되지 않은 초안은 승인되지 않아야 함"}, expected=(409,))
                    self.checked("WarehouseUnreadyApprovalBlockedBeforeLock",
                        get(blocked,"errorCode") == "WarehouseReconsignmentCoordinatesMissing" or get(blocked,"code") == "WarehouseReconsignmentCoordinatesMissing")
                quote = self.call("shipper","POST","/api/v1/shipper/requests/fare-estimate",{
                    "차량종류":"1톤 카고","예상거리Km":2.5,"상차위도":37.588,"상차경도":127.085,"하차위도":37.590,"하차경도":127.087})
                amount = get(quote,"최종운임")
                check(isinstance(amount,(int,float)) and amount>0,"WarehouseCanonicalFareRequired")
                self.mutation("warehouse-route-and-price-confirmed","shipper","PUT",f"/api/v1/shipper/requests/{encoded}",{
                    "픽업":{"주소":{"도로명주소":"격리 검증 상차 창고","위도":37.588,"경도":127.085}},
                    "하차":{"주소":{"도로명주소":"합성 검증 하차지 도로명 주소","위도":37.590,"경도":127.087}},
                    "화물":{"중량Kg":9,"부피Cbm":0.09,"팔레트개수":0,"바닥회전가능여부":True,"적층가능여부":True},
                    "요금옵션":{"예상거리Km":2.5,"기사지급예정운임":amount,"플랫폼수수료":0}})
                self.state["warehouseRoutePriceInput"]={"synthetic":True,"declaredFixtureWeightKg":9,"declaredFixtureCbm":0.09,
                    "distanceInputKm":2.5,"canonicalSyntheticDistanceKm":get(quote,"예상거리Km"),"canonicalFareKrw":amount,
                    "source":"ExistingShipperApiBeforeApproval","actualRouteVerified":False}
            self.mutation("cargo-approve", "shipper", "POST", f"/api/v1/shipper/requests/{encoded}/settlement/postpay/approve", {"승인메모": "r108 합성 후불 승인 · 실제 지급 아님"})
        if "cargo-offer" not in self.state["attempts"]:
            self.mutation("cargo-offer", None, "POST", "/verification/cargo/offer" if warehouse else "/verification/cargo/standalone-offer", {"requestId": rid})
        if "cargo-accept" not in self.state["attempts"]:
            self.notifications()
            if self.options.inject_acceptance_failure and not warehouse:
                self.mutation("fault-arm", None, "POST", "/verification/r108/acceptance/fault-arm", {})
            proposals = self.call("driver", "GET", "/api/v1/driver/recommendations")
            self.state["currentRecommendationObservation"] = {
                "requestedRequestVisible": isinstance(proposals, list) and any(get(x, "의뢰Id") == rid for x in proposals),
                "source": "AuthenticatedCurrentRecommendationGet", "actualRouteVerified": False}
            save(self.path, self.state)
            item = current_cargo_recommendation(proposals, rid)
            self.mutation("cargo-accept", "driver", "POST", f"/api/v1/driver/dispatch-actions/{encoded}/accept", {"ExpectedRecommendationRound": get(item, "추천라운드"), "AcknowledgedWarningCodes": ["FreightVehicleAdvisory"] if get(item, "차량경고", []) else []}, discard=True)
        current = self.call("driver", "GET", "/api/v1/driver/transports/current")
        tid = cargo_transport_identity(current, rid, self.state.get("transportId"))
        canonical_detail = self.call("driver", "GET", f"/api/v1/driver/transports/{tid}")
        cargo_transport_identity(canonical_detail, rid, tid)
        self.state["transportId"] = tid
        self.cargo_proof(rid, warehouse)
        self.checked("CargoAcceptLostResponseRecoveredCanonical")
        if self.options.runtime_controls:
            fault = self.call(None,"GET","/verification/r108/proof")
            job = next((x for x in get(fault,"acceptanceJobs",[]) if get(x,"requestId")==rid),None)
            if job is not None and get(job,"state")=="Retry":
                self.state["acceptanceFaultBeforeRecovery"] = fault
                self.checked("AcceptancePreservedDespiteNotificationInsertFailure",get(job,"notificationPrepared") is False)
                due = get(job,"nextAttemptAtUtc")
                due_at = dt.datetime.fromisoformat(due.replace("Z","+00:00")) if due else dt.datetime.now(dt.timezone.utc)
                if due_at.tzinfo is None: due_at = due_at.replace(tzinfo=dt.timezone.utc)
                delay = max(0,(due_at-dt.datetime.now(dt.timezone.utc)).total_seconds()+0.2)
                check(delay<60,"RecoveryDelayExceedsBoundedHarnessWait")
                time.sleep(delay)
            recovered = self.call(None, "POST", "/verification/r108/acceptance/recover", {"requestId": rid})
            self.checked("CargoAcceptanceRecoveryRepeated_NoDuplicateNotification", get(recovered, "duplicateNotificationsAdded") is False)
            self.state["acceptanceRecovery"] = recovered
        if self.options.pause_after_accept:
            self.state["pausedAfterAccept"] = True; return
        if "arrive-pickup" not in self.state["attempts"]:
            self.mutation("arrive-pickup", "driver", "POST", f"/api/v1/driver/transports/{tid}/arrive-pickup")
        if warehouse and "handoff" not in self.state["attempts"]:
            self.mutation("pre-handoff-pickup-blocked", "driver", "POST", f"/api/v1/driver/transports/{tid}/pickup-complete", {}, expected=(409,))
            self.mutation("handoff", "warehouse", "POST", "/api/v1/warehouse-operations/outbound-plan-reviews/910004/handoff-complete", {"driverIdentityConfirmed": True, "vehicleConfirmed": True, "cargoReleasedConfirmed": True, "memo": "r108 합성 인계"})
        if "pickup" not in self.state["attempts"]:
            uploaded = self.upload(tid, True)
            self.mutation("pickup", "driver", "POST", f"/api/v1/driver/transports/{tid}/pickup-complete", {"상차사진ObjectName": get(uploaded, "objectName"), "상차사진Url": get(uploaded, "url")})
        if "arrive-dropoff" not in self.state["attempts"]:
            self.mutation("arrive-dropoff", "driver", "POST", f"/api/v1/driver/transports/{tid}/arrive-dropoff")
        if "deliver" not in self.state["attempts"]:
            uploaded = self.upload(tid, False)
            self.mutation("deliver", "driver", "POST", f"/api/v1/driver/transports/{tid}/complete", {"하차사진ObjectName": get(uploaded, "objectName"), "하차사진Url": get(uploaded, "url")})
        detail = self.call("driver", "GET", f"/api/v1/driver/transports/{tid}")
        cargo_transport_identity(detail, rid, tid)
        shipper = self.call("shipper", "GET", f"/api/v1/shipper/requests/{encoded}")
        check(get(shipper, "의뢰Id") == rid, "CargoShipperRequestIdentityMismatch")
        self.call("driver", "GET", "/api/v1/driver/payout-preparations")
        proof = self.cargo_proof(rid, warehouse)
        self.state["finalTransportState"] = get(get(proof, "transport", {}), "state") if warehouse else get(proof, "transportState")
        self.checked("CargoDeliveryCompletedAndPaymentStillSeparate", self.state["finalTransportState"] == get(detail, "상태") == "인수완료")
        self.state["completed"] = True
        if warehouse:
            self.checked("WarehouseStockConsumedExactlyOnce", get(proof, "outboundMovementCount") == 1 and get(proof, "outboundHistoryCount") == 1)
            self.state["mongoDocumentCount"] = get(get(proof, "mongo", {}), "documentCount")
        # The legacy cargo receipt API registers an invoice, not a customer's receipt confirmation.
        self.state["customerReceiptConfirmationVerified"] = False
        self.state["warehouseIngressPickingPackingVerified"] = False

    def notifications(self):
        if not self.options.runtime_controls:
            return
        first = self.call(None, "POST", "/verification/r108/notifications/send", {})
        again = self.call(None, "POST", "/verification/r108/notifications/send", {})
        self.checked("ProductRecommendationOutbox_ExplicitFakeTransport_NoDuplicate", get(again, "food") == 0 and get(again, "cargo") == 0)
        proof = self.call(None, "GET", "/verification/r108/proof")
        self.state["outboxProof"] = proof
        self.state["localNotificationProcessed"] = {"food": get(first,"food"), "cargo": get(first,"cargo")}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scenario", choices=("food", "quick", "warehouse", "all", "extras"))
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--pause-after-accept", action="store_true")
    parser.add_argument("--case")
    parser.add_argument("--runtime-controls", action="store_true")
    parser.add_argument("--inject-acceptance-failure", action="store_true")
    parser.add_argument("--additional-recovery-cases", action="store_true")
    options = parser.parse_args()
    flow = None
    try:
        scenarios = [("food",None),("quick",None),("warehouse",None)] if options.scenario == "all" else [("food","food-notification"),("quick","quick-fault")] if options.scenario == "extras" else [(options.scenario,options.case)]
        if options.additional_recovery_cases:
            check(options.scenario == "all" and options.runtime_controls,"AdditionalCasesRequireAllAndRuntimeControls")
            scenarios += [("food","food-notification"),("quick","quick-fault")]
        tokens = {}
        results = []
        for scenario,case in scenarios:
            options.scenario = scenario
            options.case = case
            flow = Workflow(options)
            flow.tokens = tokens
            if "failure" in flow.state:
                flow.state.setdefault("historicalFailures", []).append(flow.state.pop("failure"))
            flow.state["status"] = "Running"
            if not flow.state.get("completed"):
                flow.food() if scenario == "food" else flow.cargo(scenario == "warehouse")
            flow.state["status"] = "Passed" if flow.state.get("completed") else "PausedAfterAccept"
            flow.state["boundaries"] = {"actualMySqlMongo": True, "syntheticBusinessData": True, **cargo_schema_boundaries(flow.ready),
                "actualPhoneUi": False, "realGpsCamera": False, "actualFcm": False, "externalEffects": False, "realPaymentOrPayout": False,
                "controlledRecommendationInvocation": True, "hostedWorkersRun": False, "foodPhotoRequiredByCurrentApi": False,
                "declaredCargoProviderGuardEmulated": scenario != "food", "actualCargoRoadVerified": False,
                "responseLossMethod": "SuccessfulResponseDiscardedByHarnessThenCanonicalGet_NoAutomaticPostRepeat"}
            save(flow.path, flow.state)
            results.append({"status": flow.state["status"], "scenario": scenario, "checks": len(flow.state["checks"]), "result": str(flow.path)})
        print(json.dumps(results, ensure_ascii=False))
        return 0
    except Exception as error:
        if flow is not None:
            flow.state["status"] = "Failed"
            flow.state["failure"] = {"type": type(error).__name__, "code": str(error)[:240] if isinstance(error, RuntimeError) else "DependencyOrImplementationFailure"}
            save(flow.path, flow.state)
        print(json.dumps({"status": "Failed", "type": type(error).__name__, "code": str(error)[:240] if isinstance(error, RuntimeError) else "DependencyOrImplementationFailure"}))
        return 1

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
