"""Regressions for public source grain, unknown values and role case display."""

import csv
from io import StringIO
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
import zipfile

from role_tile_examples import case_state
from role_tile_housing_api_probe import parse_sample
from role_tile_libraries import FIELDS, hours
from role_tile_stay import source_duration_rows


class RoleTileEvidenceTests(unittest.TestCase):
    def duration_sample(self, records):
        with TemporaryDirectory() as temporary:
            path = Path(temporary) / "stay.zip"
            source = {"sourceId": "OA-22892", "observedDate": "2026-11-20", "attachment": "stay.zip"}
            table = StringIO(newline="")
            write = csv.writer(table)
            write.writerow(["기준년월일", "행정동 코드", "격자 ID", "x", "y", "시각", "구간", "인구수"] + ["age"] * 16)
            for area, count in records:
                write.writerow(["20261120", area, "다사60004000", "960125", "1940125", "12", "060", count] + ["*"] * 16)
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("stay.csv", table.getvalue().encode("cp949"))
            return source_duration_rows(path, source, {"다사60004000": (960125, 1940125)}, {"12"})

    def test_distinct_admin_components_are_related_but_not_duplicates(self):
        components, receipt = self.duration_sample([("11260575", "5.50"), ("11260570", "*")])
        self.assertEqual([5.5, 1, 1], components["12", "060", "다사60004000"])
        self.assertEqual(1, receipt["native_additional_admin_components"])
        self.assertEqual(0, receipt["duplicate_full_provider_keys"])

    def test_repeated_full_provider_key_quarantines_grid(self):
        components, receipt = self.duration_sample([("11260575", "5.50"), ("11260575", "7.00")])
        self.assertEqual({}, components)
        self.assertEqual(1, receipt["duplicate_full_provider_keys"])

    def test_numeric_and_mask_only_stay_have_distinct_states(self):
        self.assertEqual("published_component_allocated", case_state("stay", {
            "rows": [{"mean_known_allocated_per_start_hour": 0}]}))
        self.assertEqual("numeric_component_not_available", case_state("stay", {
            "rows": [{"mean_known_allocated_per_start_hour": None}]}))
        self.assertEqual("published_component_allocated_with_masking", case_state("stay", {
            "rows": [{"mean_known_allocated_per_start_hour": 3,
                      "masked_or_partial_weighted_grid_hours": 10}]}))

    def test_legal_area_context_does_not_produce_tile_values(self):
        result = case_state("landPrice", {"nativeScope": "법정동",
            "rows": [{"legal_dong_code": "1126010100", "median_krw_per_m2": 3305000}],
            "regionContext": [{"adminCode": "1126057000", "referenceKeys": ["1126010100"]}]})
        self.assertEqual("native_region_context", result)

    def test_library_zero_clock_pair_does_not_mean_known_closed(self):
        row = dict.fromkeys(FIELDS)
        row["WEEKDAY_OPER_OPEN_HHMM"] = "09:00"
        row["WEEKDAY_OPER_COLSE_HHMM"] = "18:30"
        row["SAT_OPER_OPER_OPEN_HHMM"] = "00:00"
        row["SAT_OPER_CLOSE_HHMM"] = "00:00"
        weekday, saturday, _holiday = list(hours("case", row))
        self.assertEqual(("09:00", "18:30", "published_hours"), weekday[-3:])
        self.assertEqual((None, None, "schedule_unresolved"), saturday[-3:])

    def test_housing_empty_success_is_distinct_from_service_authorization(self):
        self.assertEqual("service_authorization_required", parse_sample(
            b"<OpenAPI_ServiceResponse><cmmMsgHeader><returnReasonCode>30</returnReasonCode></cmmMsgHeader>"
            b"</OpenAPI_ServiceResponse>")["state"])
        self.assertEqual("sample_returned_zero", parse_sample(
            b"<response><header><resultCode>00</resultCode></header><body><totalCount>0</totalCount></body>"
            b"</response>")["state"])


if __name__ == "__main__":
    unittest.main()
