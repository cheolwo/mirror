"""합성 집계로만 시험하는 읽기 전용 로컬 LLM 참고 도구."""

from __future__ import annotations

import argparse
import json
import math
import time
import urllib.error
import urllib.parse
import urllib.request
from typing import Any

MODEL = "qwen3:1.7b"
SCENARIOS = {
    "quiet": {"regionCode": "SYNTHETIC-A", "pendingOrders": 0, "expectedOrdersNextHour": 7,
              "activeDrivers": 5, "ordersPerDriverHour": 3.5},
    "balanced": {"regionCode": "SYNTHETIC-B", "pendingOrders": 0, "expectedOrdersNextHour": 35,
                 "activeDrivers": 10, "ordersPerDriverHour": 3.5},
    "surge": {"regionCode": "SYNTHETIC-C", "pendingOrders": 14, "expectedOrdersNextHour": 56,
              "activeDrivers": 10, "ordersPerDriverHour": 3.5},
}
SCHEMA = {
    "type": "object", "additionalProperties": False,
    "required": ["regionCode", "expectedOrdersNextHour", "suggestedActiveDrivers",
                 "rationaleCode", "confidence", "advisoryOnly"],
    "properties": {
        "regionCode": {"type": "string", "enum": [x["regionCode"] for x in SCENARIOS.values()]},
        "expectedOrdersNextHour": {"type": "integer", "minimum": 0, "maximum": 10000},
        "suggestedActiveDrivers": {"type": "integer", "minimum": 0, "maximum": 2000},
        "rationaleCode": {"type": "string", "enum": ["LOW_DEMAND", "BALANCED", "CAPACITY_SHORTAGE"]},
        "confidence": {"type": "number", "minimum": 0, "maximum": 1},
        "advisoryOnly": {"type": "boolean", "const": True},
    },
}
MAX_RESPONSE_BYTES = 65536


def parse_json(raw):
    def unique_pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError("duplicate_json_key")
            result[key] = value
        return result

    def invalid_constant(value):
        raise ValueError("nonfinite_json_number")

    return json.loads(raw, object_pairs_hook=unique_pairs, parse_constant=invalid_constant)


class NoRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise ValueError("redirect_forbidden")


def validate_endpoint(endpoint: str) -> str:
    parsed = urllib.parse.urlsplit(endpoint)
    if (parsed.scheme != "http" or parsed.hostname not in {"127.0.0.1", "::1"}
            or parsed.username or parsed.password or parsed.path not in {"", "/"}
            or parsed.query or parsed.fragment or parsed.port is None):
        raise ValueError("only_explicit_loopback_http_port_allowed")
    return endpoint.rstrip("/")


def rule_reference(aggregate: dict[str, Any]) -> dict[str, Any]:
    """설명용 기준선. 제품 서버의 정책 또는 수요 예측기를 대체하지 않는다."""
    target = math.ceil((aggregate["pendingOrders"] + aggregate["expectedOrdersNextHour"])
                       / aggregate["ordersPerDriverHour"])
    if target > aggregate["activeDrivers"]:
        reason = "CAPACITY_SHORTAGE"
    elif target < aggregate["activeDrivers"]:
        reason = "LOW_DEMAND"
    else:
        reason = "BALANCED"
    return {"targetActiveDrivers": target, "rationaleCode": reason,
            "dispatchAuthority": "server_rules_only", "operationalEffect": False}


def build_request(aggregate: dict[str, Any]) -> dict[str, Any]:
    if aggregate not in SCENARIOS.values():
        raise ValueError("only_built_in_synthetic_aggregates_allowed")
    instructions = (
        "You are a read-only demand planning assistant, not a dispatcher. "
        "These are synthetic aggregates, not real people or orders. "
        "Copy regionCode and expectedOrdersNextHour. Compute suggestedActiveDrivers as "
        "ceil((pendingOrders + expectedOrdersNextHour) / ordersPerDriverHour). "
        "Use CAPACITY_SHORTAGE if target is greater than activeDrivers, LOW_DEMAND if less, "
        "otherwise BALANCED. Do not assign drivers, change state or claim prediction accuracy. "
        "Return only the exact JSON schema. advisoryOnly must be true. "
        "confidence is subjective, not measured forecast accuracy. Schema: " + json.dumps(SCHEMA)
    )
    return {"model": MODEL, "stream": False, "think": False, "keep_alive": 0,
            "format": SCHEMA, "options": {"temperature": 0, "seed": 17, "num_ctx": 4096,
                                         "num_predict": 256},
            "messages": [{"role": "system", "content": instructions},
                         {"role": "user", "content": json.dumps(aggregate)}]}


def validate_advice(value: Any, aggregate: dict[str, Any]) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != set(SCHEMA["required"]):
        raise ValueError("invalid_advice_fields")
    if value["regionCode"] != aggregate["regionCode"] or value["advisoryOnly"] is not True:
        raise ValueError("invalid_advice_authority_or_region")
    for key, upper in (("expectedOrdersNextHour", 10000), ("suggestedActiveDrivers", 2000)):
        if type(value[key]) is not int or not 0 <= value[key] <= upper:
            raise ValueError("invalid_advice_numeric_range")
    if (type(value["confidence"]) not in {int, float} or not math.isfinite(value["confidence"])
            or not 0 <= value["confidence"] <= 1):
        raise ValueError("invalid_advice_confidence")
    if value["rationaleCode"] not in SCHEMA["properties"]["rationaleCode"]["enum"]:
        raise ValueError("invalid_advice_reason")
    # 첫 시험은 수요 예측이 아니라 제공된 집계의 계산·표현 일치 시험이다.
    baseline = rule_reference(aggregate)
    if (value["expectedOrdersNextHour"] != aggregate["expectedOrdersNextHour"]
            or value["suggestedActiveDrivers"] != baseline["targetActiveDrivers"]
            or value["rationaleCode"] != baseline["rationaleCode"]):
        raise ValueError("advice_conflicts_with_rule_reference")
    return value


def request_local_json(endpoint: str, payload: dict[str, Any], timeout: float, response_sink=None) -> tuple[Any, dict]:
    """공통 로컬 전송 경계. 호출자가 합성 입력과 업무별 출력 계약을 검증한다."""
    endpoint = validate_endpoint(endpoint)
    if not 0 < timeout <= 120:
        raise ValueError("timeout_must_be_between_zero_and_120_seconds")
    if (payload.get("model") != MODEL or payload.get("stream") is not False
            or payload.get("tools")):
        raise ValueError("only_fixed_local_model_without_tools_allowed")
    request = urllib.request.Request(endpoint + "/api/chat", data=json.dumps(payload).encode("utf-8"),
                                     headers={"Content-Type": "application/json"}, method="POST")
    # 환경 proxy도 사용하지 않는다. redirect를 통해 외부 endpoint로 빠져나가지 않는다.
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirects())
    with opener.open(request, timeout=timeout) as response:
        raw = response.read(MAX_RESPONSE_BYTES + 1)
    if len(raw) > MAX_RESPONSE_BYTES:
        raise ValueError("model_response_too_large")
    # 합성 검토 호출자는 파싱 실패 응답도 제한된 크기로 비공개 보관할 수 있다.
    if response_sink is not None:
        response_sink(raw)
    envelope = parse_json(raw)
    if not isinstance(envelope, dict) or envelope.get("model") != MODEL or envelope.get("done") is not True:
        raise ValueError("model_or_completion_mismatch")
    message = envelope.get("message")
    if not isinstance(message, dict) or not isinstance(message.get("content"), str):
        raise ValueError("invalid_model_message")
    if message.get("tool_calls"):
        raise ValueError("model_tool_calls_forbidden")
    value = parse_json(message["content"])
    metrics = {key: envelope.get(key) for key in
               ("total_duration", "load_duration", "prompt_eval_count", "eval_count", "eval_duration")}
    return value, metrics


def request_advice(endpoint: str, aggregate: dict[str, Any], timeout: float) -> tuple[dict, dict]:
    value, metrics = request_local_json(endpoint, build_request(aggregate), timeout)
    return validate_advice(value, aggregate), metrics


def run(scenario: str, execute: bool = False, endpoint: str = "http://127.0.0.1:12681",
        timeout: float = 90) -> dict[str, Any]:
    aggregate = dict(SCENARIOS[scenario])
    # 무효 endpoint는 dry-run에서도 즉시 거절한다.
    validate_endpoint(endpoint)
    result = {"scenario": scenario, "syntheticOnly": True, "model": MODEL,
              "mode": "execute_local_advice" if execute else "dry_run",
              "aggregate": aggregate, "ruleReference": rule_reference(aggregate),
              "advice": None, "adviceStatus": "not_requested", "metrics": {},
              "operationalApiCalls": 0, "stateChanges": 0}
    if execute:
        started = time.monotonic()
        try:
            result["advice"], result["metrics"] = request_advice(endpoint, aggregate, timeout)
            result["adviceStatus"] = "validated_advisory_only"
        except (ValueError, KeyError, TypeError, OSError, urllib.error.URLError) as error:
            result["adviceStatus"] = "rule_reference_retained"
            # 원문 오류/응답에는 외부 서비스 내용이 섞일 수 있어 유형만 보존한다.
            result["failureType"] = type(error).__name__
        result["elapsedSeconds"] = round(time.monotonic() - started, 3)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scenario", choices=SCENARIOS, default="balanced")
    parser.add_argument("--execute", action="store_true", help="루프백 로컬 모델에 합성 집계만 보냅니다.")
    parser.add_argument("--endpoint", default="http://127.0.0.1:12681")
    parser.add_argument("--timeout", type=float, default=90)
    args = parser.parse_args()
    print(json.dumps(run(args.scenario, args.execute, args.endpoint, args.timeout),
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
