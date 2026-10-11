import copy
import io
import json
import unittest
from unittest.mock import patch

import dispatch_advisor as advisor


def advice_for(scenario="balanced"):
    aggregate = advisor.SCENARIOS[scenario]
    baseline = advisor.rule_reference(aggregate)
    return {"regionCode": aggregate["regionCode"],
            "expectedOrdersNextHour": aggregate["expectedOrdersNextHour"],
            "suggestedActiveDrivers": baseline["targetActiveDrivers"],
            "rationaleCode": baseline["rationaleCode"], "confidence": 0.8, "advisoryOnly": True}


class AdvisorTests(unittest.TestCase):
    def test_default_dry_run_never_calls_http(self):
        with patch.object(advisor, "request_advice") as request:
            result = advisor.run("balanced")
        request.assert_not_called()
        self.assertEqual(10, result["ruleReference"]["targetActiveDrivers"])
        self.assertEqual(0, result["operationalApiCalls"])
        self.assertEqual(0, result["stateChanges"])

    def test_synthetic_scenarios_have_expected_baselines(self):
        for scenario, target, reason in (("quiet", 2, "LOW_DEMAND"),
                                        ("balanced", 10, "BALANCED"),
                                        ("surge", 20, "CAPACITY_SHORTAGE")):
            with self.subTest(scenario=scenario):
                result = advisor.run(scenario)
                self.assertEqual(target, result["ruleReference"]["targetActiveDrivers"])
                self.assertEqual(reason, result["ruleReference"]["rationaleCode"])

    def test_external_credentials_redirect_like_endpoints_rejected(self):
        for endpoint in ("https://127.0.0.1:12681", "http://example.com:12681",
                         "http://localhost:12681", "http://0.0.0.0:12681",
                         "http://127.0.0.1", "http://user@127.0.0.1:12681",
                         "http://127.0.0.1:12681/path", "http://127.0.0.1:12681?url=x"):
            with self.subTest(endpoint=endpoint), self.assertRaises(ValueError):
                advisor.run("balanced", endpoint=endpoint)

    def test_redirect_is_never_followed(self):
        with self.assertRaises(ValueError):
            advisor.NoRedirects().redirect_request(None, None, 302, "Found", {}, "https://example.com")

    def test_unknown_aggregate_rejected_before_network(self):
        aggregate = copy.deepcopy(advisor.SCENARIOS["balanced"])
        aggregate["customerAddress"] = "not permitted"
        with self.assertRaises(ValueError):
            advisor.build_request(aggregate)

    def test_prompt_contains_only_fixed_synthetic_data(self):
        payload = advisor.build_request(advisor.SCENARIOS["balanced"])
        self.assertEqual(advisor.SCENARIOS["balanced"], json.loads(payload["messages"][1]["content"]))
        self.assertFalse(payload["think"])
        self.assertEqual(0, payload["keep_alive"])
        self.assertNotIn("tools", payload)

    def test_failed_model_keeps_rule_reference(self):
        for error in (TimeoutError(), ConnectionError(), ValueError(), KeyError(), TypeError()):
            with self.subTest(error=type(error).__name__), patch.object(
                    advisor, "request_advice", side_effect=error):
                result = advisor.run("surge", execute=True)
            self.assertEqual("rule_reference_retained", result["adviceStatus"])
            self.assertEqual(20, result["ruleReference"]["targetActiveDrivers"])
            self.assertEqual(0, result["stateChanges"])

    def test_valid_advice_cannot_change_effective_rule(self):
        with patch.object(advisor, "request_advice", return_value=(advice_for(), {})):
            result = advisor.run("balanced", execute=True)
        self.assertEqual("validated_advisory_only", result["adviceStatus"])
        self.assertEqual("server_rules_only", result["ruleReference"]["dispatchAuthority"])

    def test_rejects_schema_type_range_and_authority_errors(self):
        bad_values = (("advisoryOnly", False), ("regionCode", "other"), ("confidence", float("nan")),
                      ("confidence", True), ("suggestedActiveDrivers", True),
                      ("suggestedActiveDrivers", -1), ("suggestedActiveDrivers", 2001),
                      ("expectedOrdersNextHour", 2.5), ("rationaleCode", "ASSIGN_DRIVER"))
        for key, value in bad_values:
            candidate = advice_for()
            candidate[key] = value
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                advisor.validate_advice(candidate, advisor.SCENARIOS["balanced"])

    def test_extra_driver_assignment_or_missing_fields_rejected(self):
        for candidate in (dict(advice_for(), driverId="synthetic-person"), {"advisoryOnly": True}, []):
            with self.assertRaises(ValueError):
                advisor.validate_advice(candidate, advisor.SCENARIOS["balanced"])

    def test_conflicting_calculation_rejected(self):
        for key, value in (("expectedOrdersNextHour", 999), ("suggestedActiveDrivers", 11),
                           ("rationaleCode", "CAPACITY_SHORTAGE")):
            candidate = advice_for()
            candidate[key] = value
            with self.assertRaises(ValueError):
                advisor.validate_advice(candidate, advisor.SCENARIOS["balanced"])

    def test_network_envelope_and_output_boundaries(self):
        valid = {"model": advisor.MODEL, "done": True,
                 "message": {"content": json.dumps(advice_for())}}
        invalid = [b"x" * (advisor.MAX_RESPONSE_BYTES + 1), b"not json", b"[]", b"null",
                   json.dumps(dict(valid, model="cloud:model")).encode(),
                   json.dumps(dict(valid, done=False)).encode(),
                   json.dumps(dict(valid, message=None)).encode(),
                   json.dumps(dict(valid, message={"content": {}})).encode(),
                   json.dumps(dict(valid, message={"content": "{}", "tool_calls": [{}]})).encode()]
        for raw in invalid:
            response = io.BytesIO(raw)
            with self.subTest(size=len(raw)), patch.object(
                    advisor.urllib.request, "build_opener") as opener:
                opener.return_value.open.return_value.__enter__.return_value = response
                result = advisor.run("balanced", execute=True)
                self.assertEqual("rule_reference_retained", result["adviceStatus"])

    def test_valid_local_response(self):
        raw = json.dumps({"model": advisor.MODEL, "done": True,
                          "message": {"content": json.dumps(advice_for())}, "eval_count": 42}).encode()
        with patch.object(advisor.urllib.request, "build_opener") as opener:
            opener.return_value.open.return_value.__enter__.return_value = io.BytesIO(raw)
            result = advisor.run("balanced", execute=True)
        request = opener.return_value.open.call_args.args[0]
        self.assertEqual("http://127.0.0.1:12681/api/chat", request.full_url)
        self.assertEqual("validated_advisory_only", result["adviceStatus"])
        self.assertEqual(42, result["metrics"]["eval_count"])

    def test_invalid_timeout_does_not_make_request(self):
        for timeout in (0, -1, 121):
            with patch.object(advisor.urllib.request, "build_opener") as opener:
                result = advisor.run("balanced", execute=True, timeout=timeout)
            opener.assert_not_called()
            self.assertEqual("rule_reference_retained", result["adviceStatus"])


if __name__ == "__main__":
    unittest.main()
