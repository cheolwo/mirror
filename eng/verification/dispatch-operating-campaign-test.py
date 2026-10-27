"""Offline contracts for the bounded campaign. No host, login, DB or model is used."""
import argparse
import copy
import datetime as dt
import importlib.util
import json
import os
import pathlib
import tempfile
import unittest
from unittest import mock
from types import SimpleNamespace
import uuid

_spec = importlib.util.spec_from_file_location("operating_campaign", pathlib.Path(__file__).with_name("dispatch-operating-campaign.py"))
m = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(m)


class Clock:
    def __init__(self):
        self.elapsed = 0
        self.origin = dt.datetime(2026, 10, 21, tzinfo=dt.timezone.utc)
        self.interrupt = False

    def now(self):
        return self.origin + dt.timedelta(seconds=self.elapsed)

    def monotonic(self):
        return self.elapsed

    def sleep(self, seconds):
        if self.interrupt:
            self.interrupt = False
            raise KeyboardInterrupt()
        self.elapsed += seconds


class Api:
    def __init__(self, repo, clock, phase="Idle", run_id=None):
        self.repo, self.clock = repo, clock
        self.current = {"phaseCode": phase, "runId": run_id, "revision": 1, "counts": {},
            "allowedCommands": ["start"] if phase in {"Idle", "Completed"} else ["stop", "support-drivers"],
            "availableSupportDriverCount": 2, "activatedSupportDriverCount": 0,
            "appliedCommandIds": [], "pendingIncidents": []}
        self.calls, self.hooks = [], {}
        self.fail_after_apply, self.fail_before_apply = set(), set()
        self.get_failures = 0
        self.closed = False
        self.before_post = None
        self.authenticate_error = None

    def authenticate(self):
        if self.authenticate_error:
            raise self.authenticate_error

    def close(self):
        self.closed = True

    def write_session(self):
        path = self.repo / "artifacts/local/dispatch-training-r58/session.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        value = {"schemaVersion": 1, "runId": self.current["runId"], "revision": self.current["revision"],
                 "phaseCode": self.current["phaseCode"], "slots": [], "commands": [], "events": [],
                 "decisions": [], "incidents": [], "lastObservation": {"canonicalReadSucceeded": False}}
        path.write_text(json.dumps(value), encoding="utf-8")

    def request(self, method, path, body=None):
        self.calls.append((method, path, copy.deepcopy(body)))
        if method == "GET":
            if self.get_failures:
                self.get_failures -= 1
                raise TimeoutError("secret response never belongs in evidence")
            if path == m.API + "current":
                if "current" in self.hooks:
                    self.hooks["current"](self)
                self.write_session()
                return copy.deepcopy(self.current)
            return {"runId": self.current["runId"], "revision": self.current["revision"], "items": []}
        action = path.removeprefix(m.API)
        if self.before_post:
            self.before_post(action, body)
        if action in self.fail_before_apply:
            raise TimeoutError("do not persist bearer test-secret")
        self.current["revision"] += 1
        self.current["appliedCommandIds"].append(body["clientRequestId"])
        if action == "start":
            self.current.update(phaseCode="Running", runId="dispatch-training-" + uuid.UUID(body["clientRequestId"]).hex,
                                allowedCommands=["stop", "support-drivers"])
        elif action == "stop":
            self.current.update(phaseCode="Draining", allowedCommands=[])
        elif action == "support-drivers":
            self.current.update(availableSupportDriverCount=0, activatedSupportDriverCount=body["count"])
        self.write_session()
        if action in self.fail_after_apply:
            raise TimeoutError("post was applied; body must never be logged")
        return {}


class CampaignTests(unittest.TestCase):
    def setUp(self):
        parent = m.ROOT / "artifacts/local/dispatch-campaign-offline-tests"
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=parent)
        self.repo = pathlib.Path(self.temp.name)
        self.test_parent = parent
        self.clock = Clock()
        self.api = Api(self.repo, self.clock)
        self.imports = []

    def tearDown(self):
        # Verify this disposable test workspace before recursive cleanup, and
        # retain the extended prefix for long nested SHA snapshot paths.
        assert self.repo.parent == self.test_parent and self.repo.name.startswith("tmp")
        self.temp.name = str(m.native(self.repo))
        self.temp.cleanup()

    def args(self, **changes):
        result = dict(start=False, support_on_shortage=False, observe_seconds=6,
            campaign_id=None, campaign_root=m.DEFAULT_ROOT, experience_root="artifacts/local/dispatch-experiences")
        result.update(changes)
        return argparse.Namespace(**result)

    def importer(self, repo, source, corpus):
        self.imports.append((m.digest(m.read_bytes(source)), source))

    def campaign(self, api=None, importer=None, **args):
        return m.Campaign(self.repo, self.args(**args), api=api or self.api,
            importer=importer or self.importer, now=self.clock.now, monotonic=self.clock.monotonic,
            sleep=self.clock.sleep, emit=lambda _: None)

    def posts(self, action):
        return [c for c in self.api.calls if c[:2] == ("POST", m.API + action)]

    def test_durable_reservation_precedes_post_and_canonical_owns_stop(self):
        campaign = self.campaign(start=True)
        def before_post(action, body):
            disk = m.checked_json(m.read_bytes(campaign.state_path))
            row = next(c for c in disk["commands"] if c["action"] == action)
            self.assertEqual(body["clientRequestId"], row["clientRequestId"])
            self.assertEqual("Unknown", row["outcome"])
        self.api.before_post = before_post
        self.assertEqual(0, campaign.execute())
        self.assertEqual(1, len(self.posts("start")))
        self.assertEqual(1, len(self.posts("stop")))
        self.assertEqual(campaign.state["startedRunId"], self.posts("stop")[0][2]["runId"])
        self.assertTrue(self.api.closed)
        self.assertTrue(self.imports)

    def test_applied_timeout_is_confirmed_without_repost(self):
        self.api.fail_after_apply.add("start")
        campaign = self.campaign(start=True)
        self.assertEqual(0, campaign.execute())
        self.assertEqual("AppliedCanonical", campaign.state["commands"][0]["outcome"])
        self.assertEqual(1, len(self.posts("start")))
        self.assertEqual(1, len(self.posts("stop")))

    def test_unapplied_timeout_and_resume_never_repost(self):
        self.api.fail_before_apply.add("start")
        first = self.campaign(start=True)
        self.assertEqual(1, first.execute())
        second = self.campaign(campaign_id=first.id)
        second.execute()
        self.assertEqual(1, len(self.posts("start")))
        self.assertEqual(0, len(self.posts("stop")))
        self.assertIsNone(second.state["startedRunId"])
        self.assertNotIn("test-secret", m.read_bytes(first.state_path).decode())

    def test_reserved_crash_window_is_read_only_on_restart(self):
        first = self.campaign(start=True)
        first.load()
        request_id = str(uuid.uuid4())
        first.state["commands"].append({"action": "start", "clientRequestId": request_id,
            "runId": None, "expectedRevision": 1, "outcome": "Unknown", "postRepeated": False,
            "expectedResultRunId": "dispatch-training-" + uuid.UUID(request_id).hex})
        first.save()
        second = self.campaign(campaign_id=first.id)
        second.execute()
        self.assertFalse(self.posts("start"))

    def test_restart_recovers_applied_start_receipt_and_can_stop_its_run(self):
        first = self.campaign(start=True)
        first.load()
        request_id = str(uuid.uuid4())
        run_id = "dispatch-training-" + uuid.UUID(request_id).hex
        first.state["commands"].append({"action": "start", "clientRequestId": request_id,
            "runId": None, "expectedRevision": 1, "outcome": "Unknown", "postRepeated": False,
            "expectedResultRunId": run_id})
        first.save()
        self.api.current.update(runId=run_id, phaseCode="Running", allowedCommands=["stop"],
                                appliedCommandIds=[request_id])
        second = self.campaign(campaign_id=first.id)
        self.assertEqual(0, second.execute())
        self.assertFalse(self.posts("start"))
        self.assertEqual(run_id, second.state["startedRunId"])
        self.assertEqual(1, len(self.posts("stop")))

    def test_uncertain_stop_is_not_replayed_during_final_reads_or_restart(self):
        self.api.fail_before_apply.add("stop")
        campaign = self.campaign(start=True)
        self.assertEqual(1, campaign.execute())
        self.assertEqual("Running", self.api.current["phaseCode"])
        again = self.campaign(campaign_id=campaign.id)
        again.execute()
        self.assertEqual(1, len(self.posts("stop")))
        self.assertEqual("Unknown", campaign.state["commands"][-1]["outcome"])

    def test_existing_observed_run_is_never_stopped(self):
        self.api = Api(self.repo, self.clock, "Running", "somebody-elses-run")
        self.campaign().execute()
        self.assertFalse(self.posts("stop"))
        self.assertFalse(self.posts("start"))

    def test_run_switch_blocks_support_and_stop(self):
        self.api = Api(self.repo, self.clock, "Running", "first-run")
        def switch(api):
            if self.clock.elapsed >= 5:
                api.current["runId"] = "different-run"
        self.api.hooks["current"] = switch
        campaign = self.campaign()
        self.assertEqual(1, campaign.execute())
        self.assertFalse(self.posts("stop"))
        self.assertIn("CampaignRunChanged", json.dumps(campaign.state))

    def test_deadline_does_not_reset_on_restart(self):
        first = self.campaign(start=True, observe_seconds=6)
        first.execute()
        before = len(self.api.calls)
        original = first.state["deadlineUtc"]
        second = self.campaign(campaign_id=first.id, observe_seconds=3600, start=True)
        second.execute()
        self.assertEqual(original, second.state["deadlineUtc"])
        self.assertEqual(6, self.clock.elapsed)
        self.assertFalse(any(c[0] == "POST" for c in self.api.calls[before:]))

    def test_ctrl_c_stops_only_owned_run_and_preserves_evidence(self):
        self.clock.interrupt = True
        campaign = self.campaign(start=True)
        self.assertEqual(130, campaign.execute())
        self.assertEqual(1, len(self.posts("stop")))
        self.assertEqual("Interrupted", campaign.state["phase"])
        self.assertTrue((campaign.folder / "status.json").exists())
        self.assertTrue((campaign.folder / "final-current.json").exists())

    def test_local_stop_request_works_while_observer_lock_is_held_and_changes_no_state(self):
        campaign = self.campaign(start=True)
        campaign.load()
        before = m.read_bytes(campaign.state_path)
        args = self.args(campaign_id=campaign.id)
        with m.CampaignLock(campaign.lock_path):
            result = m.request_stop(self.repo, args, now=self.clock.now)
            repeated = m.request_stop(self.repo, args, now=self.clock.now)
        self.assertFalse(result["httpExecuted"])
        self.assertFalse(result["reused"])
        self.assertTrue(repeated["reused"])
        self.assertEqual(result["requestId"], repeated["requestId"])
        self.assertEqual(before, m.read_bytes(campaign.state_path))
        self.assertFalse(self.api.calls)

    def test_active_observer_sees_stop_flag_and_stops_only_its_intake(self):
        campaign = self.campaign(start=True, observe_seconds=120)
        original_sleep = self.clock.sleep
        def request_during_sleep(seconds):
            original_sleep(seconds)
            m.request_stop(self.repo, self.args(campaign_id=campaign.id), now=self.clock.now)
        campaign.sleep = request_during_sleep
        self.assertEqual(0, campaign.execute())
        self.assertEqual("RequestedStop", campaign.state["phase"])
        self.assertEqual(5, self.clock.elapsed)
        self.assertEqual(1, len(self.posts("start")))
        self.assertEqual(1, len(self.posts("stop")))

    def test_restart_consumes_stop_request_without_new_start_or_deadline(self):
        first = self.campaign(start=True, observe_seconds=120)
        first.load()
        first.read_current()
        first.command("start")
        deadline = first.state["deadlineUtc"]
        m.request_stop(self.repo, self.args(campaign_id=first.id), now=self.clock.now)
        again = self.campaign(campaign_id=first.id, observe_seconds=3600)
        self.assertEqual(0, again.execute())
        self.assertEqual("RequestedStop", again.state["phase"])
        self.assertEqual(deadline, again.state["deadlineUtc"])
        self.assertEqual(0, self.clock.elapsed)
        self.assertEqual(1, len(self.posts("start")))
        self.assertEqual(1, len(self.posts("stop")))

    def test_stop_request_rejects_unknown_campaign_and_tampered_flag(self):
        campaign = self.campaign()
        campaign.load()
        before = m.read_bytes(campaign.state_path)
        with self.assertRaises(m.Failure):
            m.request_stop(self.repo, self.args(campaign_id=str(uuid.uuid4())), now=self.clock.now)
        self.assertEqual(before, m.read_bytes(campaign.state_path))
        m.request_stop(self.repo, self.args(campaign_id=campaign.id), now=self.clock.now)
        path = campaign.folder / "stop-request.json"
        value = m.checked_json(m.read_bytes(path))
        value["campaignId"] = str(uuid.uuid4())
        value.pop("requestSha256")
        value["requestSha256"] = m.digest(m.encoded(value))
        m.atomic_json(path, value)
        with self.assertRaises(m.Failure) as error:
            m.read_stop_request(campaign)
        self.assertEqual("CampaignStopRequestCorrupt", error.exception.code)
        self.assertFalse(self.api.calls)

    def test_corrupt_stop_flag_does_not_trigger_http_or_reinitialize_campaign(self):
        campaign = self.campaign()
        campaign.load()
        m.request_stop(self.repo, self.args(campaign_id=campaign.id), now=self.clock.now)
        path = campaign.folder / "stop-request.json"
        value = m.checked_json(m.read_bytes(path))
        value["requestId"] = str(uuid.uuid4())  # old digest must not be accepted
        m.atomic_json(path, value)
        again = self.campaign(campaign_id=campaign.id)
        self.assertEqual(1, again.execute())
        self.assertFalse(self.api.calls)
        self.assertEqual("Failed", again.state["phase"])
        self.assertEqual(campaign.state["createdAtUtc"], again.state["createdAtUtc"])

    def test_support_requires_explicit_option_and_is_one_shot(self):
        self.api = Api(self.repo, self.clock, "Running", "existing-run")
        self.api.current["pendingIncidents"] = [{"kindCode": "DriverShortage"}]
        self.campaign().execute()
        self.assertFalse(self.posts("support-drivers"))
        self.clock.elapsed = 0
        self.api.fail_before_apply.add("support-drivers")
        campaign = self.campaign(support_on_shortage=True)
        campaign.execute()
        self.assertEqual(1, len(self.posts("support-drivers")))

    def test_slow_import_cannot_create_post_deadline_support(self):
        self.api = Api(self.repo, self.clock, "Running", "existing-run")
        self.api.current["pendingIncidents"] = [{"kindCode": "DriverShortage"}]
        def importer(*_):
            self.clock.elapsed += 20
        self.campaign(support_on_shortage=True, importer=importer).execute()
        self.assertFalse(self.posts("support-drivers"))

    def test_failed_import_keeps_source_and_status_without_secrets(self):
        def broken(*_):
            raise TimeoutError("Bearer raw-secret stdout")
        campaign = self.campaign(start=True, importer=broken)
        self.assertEqual(1, campaign.execute())
        self.assertTrue(list(m.native(campaign.folder / "snapshots").glob("*/session.json")))
        self.assertNotIn("raw-secret", m.read_bytes(campaign.state_path).decode())
        self.assertTrue(any(x["outcome"] == "Failed" for x in campaign.state["imports"]))

    def test_same_session_does_not_repeat_successful_import(self):
        self.api = Api(self.repo, self.clock, "Running", "existing-run")
        campaign = self.campaign(observe_seconds=125)
        self.assertEqual(0, campaign.execute())
        self.assertEqual(1, len(self.imports))
        self.assertEqual(125, self.clock.elapsed)

    def test_completed_observation_finishes_early_without_starting_another_run(self):
        self.api = Api(self.repo, self.clock, "Completed", "previous-completed-run")
        campaign = self.campaign(observe_seconds=120)
        self.assertEqual(0, campaign.execute())
        self.assertEqual("Completed", campaign.state["phase"])
        self.assertEqual(0, self.clock.elapsed)
        self.assertFalse(self.posts("start"))
        self.assertFalse(self.posts("stop"))

    def test_final_diagnostics_from_another_run_are_rejected_and_not_written(self):
        original = self.api.request
        def changed_diagnostics(method, path, body=None):
            if path in {m.API + "decisions", m.API + "incidents"}:
                return {"runId": "another-run", "revision": 999, "items": [{"decisionId": "wrong-run"}]}
            return original(method, path, body)
        self.api.request = changed_diagnostics
        campaign = self.campaign(start=True)
        self.assertEqual(1, campaign.execute())
        self.assertFalse(m.native(campaign.folder / "final-decisions.json").exists())
        self.assertFalse(m.native(campaign.folder / "final-incidents.json").exists())
        self.assertFalse(campaign.state["finalReadAvailability"]["decisions"]["available"])
        self.assertTrue(any(e.get("code") == "CampaignDiagnosticRunMismatch" for e in campaign.state["errors"]))

    def test_concurrent_campaigns_share_os_lock(self):
        campaign = self.campaign()
        with m.CampaignLock(campaign.lock_path):
            with self.assertRaises(m.Failure) as error:
                campaign.execute()
        self.assertEqual("CampaignAlreadyRunning", error.exception.code)
        self.assertFalse(self.api.calls)

    def test_corrupt_state_is_not_initialized_or_used_for_http(self):
        campaign = self.campaign()
        campaign.load()
        campaign.state_path.write_text('{"schemaVersion":"corrupt"}', encoding="utf-8")
        again = self.campaign(campaign_id=campaign.id)
        with self.assertRaises(m.Failure):
            again.execute()
        self.assertFalse(self.api.calls)
        self.assertEqual('{"schemaVersion":"corrupt"}', campaign.state_path.read_text())

    def test_outside_path_and_trailing_source_overlap_rejected(self):
        with self.assertRaises(m.Failure):
            self.campaign(campaign_root="../outside")
        with self.assertRaises(m.Failure):
            self.campaign(campaign_root="artifacts/local/dispatch-training-r58/")

    def test_reparse_point_input_is_rejected(self):
        target = self.repo / "target"
        target.mkdir()
        link = self.repo / "artifacts/local/link"
        link.parent.mkdir(parents=True)
        try:
            os.symlink(target, link, target_is_directory=True)
        except OSError:
            # Windows may deny creating symlinks. Still exercise the precise
            # FILE_ATTRIBUTE_REPARSE_POINT guard without changing OS settings.
            attributes = SimpleNamespace(st_mode=m.stat.S_IFDIR, st_file_attributes=1024)
            with mock.patch.object(pathlib.Path, "lstat", return_value=attributes):
                with self.assertRaises(m.Failure):
                    m.private_path(self.repo, "artifacts/local/link/output")
            return
        with self.assertRaises(m.Failure):
            m.private_path(self.repo, "artifacts/local/link/output")

    def test_actor_projection_only_joins_exact_ids_and_discards_sensitive_fields(self):
        request_id = str(uuid.uuid4())
        session = {"runId": "run-a", "revision": 10, "slots": [
            {"index": 0, "clientRequestId": request_id, "canonicalOrderId": "cargo-1"}]}
        actor = {"runId": "run-a", "token": "secret-token", "orders": [
            {"index": 0, "clientRequestId": request_id, "canonicalId": "cargo-1",
             "commands": {"accept:2": "Confirmed", "complete": "Unknown", "secret-token": "secret-token"},
             "photos": {"photo:pickup-complete": {"objectName": "private-object", "url": "https://private/signed-token"},
                        "photo:complete": {}}, "rejectedOnce": True},
            {"index": 0, "clientRequestId": str(uuid.uuid4()), "canonicalId": "cargo-1", "commands": {}}]}
        result = m.actor_projection(m.encoded(actor), session, "a" * 64)
        self.assertEqual(1, result["unmatchedOrderCount"])
        self.assertTrue(result["orders"][0]["pickupPhotoReceiptPresent"])
        self.assertFalse(result["orders"][0]["deliveryPhotoReceiptPresent"])
        self.assertFalse(result["sameAtomicRevisionVerified"])
        self.assertFalse(result["physicalPhoto"])
        serialized = json.dumps(result)
        for forbidden in ("secret-token", "private-object", "signed-token", "rejectedOnce", "createdAtUtc"):
            self.assertNotIn(forbidden, serialized)

    def test_actor_run_mismatch_is_not_a_lifecycle_observation(self):
        with self.assertRaises(m.Failure):
            m.actor_projection(m.encoded({"runId": "other", "orders": []}), {"runId": "this"}, "a" * 64)

    def test_session_change_during_actor_read_defers_the_snapshot(self):
        self.api = Api(self.repo, self.clock, "Running", "existing-run")
        campaign = self.campaign()
        campaign.load()
        campaign.read_current()
        original_reader = m.read_bytes
        reads = 0
        def source_changes(path):
            nonlocal reads
            data = original_reader(path)
            if path == campaign.source:
                reads += 1
                if reads % 2 == 0:
                    return data + b" "
            return data
        with mock.patch.object(m, "read_bytes", side_effect=source_changes):
            campaign.checkpoint()
        self.assertFalse(self.imports)
        self.assertFalse(campaign.state["errors"])
        self.assertEqual("CampaignSourceChangedDuringRead", campaign.state["warnings"][-1]["code"])

    def test_previous_run_actor_before_first_tick_is_only_a_warning(self):
        self.api.write_session()
        actor_path = self.repo / "artifacts/local/dispatch-training-r58/actors.json"
        actor_path.write_text(json.dumps({"runId": "previous-run", "orders": []}), encoding="utf-8")
        campaign = self.campaign(start=True)
        self.assertEqual(0, campaign.execute())
        self.assertTrue(self.imports)
        self.assertFalse(campaign.state["errors"])
        self.assertTrue(any(x.get("code") == "ActorRunMismatch" for x in campaign.state["warnings"]))

    def test_api_whitelist_rejects_unrelated_or_resume_mutations(self):
        api = m.TrainingApi(self.repo)
        for path in ("api/v1/driver/transports/1/complete", m.API + "resume", "https://other.example"):
            with self.assertRaises(m.Failure):
                api.request("POST", path, {})

    def test_cli_import_is_earlybranch_only_and_bounded(self):
        dll = self.repo / "eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll"
        dll.parent.mkdir(parents=True)
        dll.write_bytes(b"offline-stub-never-executed")
        source = self.repo / "artifacts/local/snapshot/session.json"
        corpus = self.repo / "artifacts/local/corpus"
        with mock.patch.object(m.subprocess, "run", return_value=SimpleNamespace(returncode=0)) as run:
            m.import_snapshot(self.repo, source, corpus)
        argv = run.call_args.args[0]
        self.assertEqual(["dotnet", str(dll), "--experience-import", "--session", str(source), "--experience-root", str(corpus)], argv)
        self.assertNotIn("--serve", argv)
        self.assertEqual(120, run.call_args.kwargs["timeout"])
        self.assertTrue(run.call_args.kwargs["capture_output"])


if __name__ == "__main__":
    unittest.main()
