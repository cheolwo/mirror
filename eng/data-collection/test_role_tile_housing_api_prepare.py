"""Exercise offline housing XML validation with invented data."""

from contextlib import closing
from hashlib import sha256
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory
import unittest
from xml.etree import ElementTree

from housing_transactions import MOLIT_FILE_SOURCE, PRIORITY_DISTRICTS
from role_tile_housing_api_prepare import (
    COMMERCIAL, PRODUCTS, CheckError, bridge, fingerprint, load_manifest, load_pages, parse_page, report,
)


def xml_page(rows, number=1, size=50, total=None, code="000"):
    root = ElementTree.Element("response")
    header = ElementTree.SubElement(root, "header")
    ElementTree.SubElement(header, "resultCode").text = code
    body = ElementTree.SubElement(root, "body")
    items = ElementTree.SubElement(body, "items")
    for values in rows:
        row = ElementTree.SubElement(items, "item")
        for key, value in values.items():
            ElementTree.SubElement(row, key).text = value
    for key, value in {"numOfRows": size, "pageNo": number, "totalCount": total if total is not None else len(rows)}.items():
        ElementTree.SubElement(body, key).text = str(value)
    return ElementTree.tostring(root, encoding="utf-8", xml_declaration=True)


def sale(month="202609", day="5", amount="23,400", **extra):
    return {"sggCd": "11260", "dealYear": month[:4], "dealMonth": month[4:], "dealDay": day,
            "aptNm": "시험 건물 가", "excluUseAr": "39.750", "cdealDay": "", "cdealType": "",
            "dealAmount": amount, "floor": "1", "buildYear": "2018", "umdNm": "가상동", "umdCd": "10100", **extra}


def rent(**extra):
    row = sale()
    row.pop("dealAmount")
    row.pop("cdealType")
    row.pop("cdealDay")
    return {**row, "deposit": "12,300", "monthlyRent": "0", "contractType": "갱신", **extra}


class HousingApiPreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.db = self.directory / "baseline.sqlite"
        self.manifest = self.directory / "manifest.json"
        self.raw = self.directory / "xml"
        self.raw.mkdir()
        self.make_baseline()

    def make_baseline(self):
        schema = Path(__file__).with_name("housing_transactions.sql")
        with closing(sqlite3.connect(self.db)) as db, db:
            db.executescript(schema.read_text(encoding="utf-8"))
            db.execute("INSERT INTO housing_source VALUES (?, 'local-test-csv', 'https://example.invalid/fixture')",
                       (MOLIT_FILE_SOURCE,))
            db.execute("INSERT INTO housing_region VALUES('11','서울특별시',NULL,'sido','2026-10-30','fixture')")
            for district in PRIORITY_DISTRICTS:
                db.execute("INSERT INTO housing_region VALUES(?,?,'11','sigungu','2026-10-30','fixture')",
                           (district, district))
                db.execute("INSERT INTO housing_region VALUES(?,'가상동',?,'legal','2026-10-30','fixture')",
                           (district + "10100", district))
                for product in PRODUCTS:
                    reference = db.execute("""INSERT INTO housing_snapshot VALUES
                        (NULL,?,?,?,?,'2026-09-01','2026-11-25','2026-11-25T00:00:00Z',0,NULL)""",
                        (MOLIT_FILE_SOURCE, district, product.housing, product.group)).lastrowid
                    if district == "11260" and product == PRODUCTS[0]:
                        for source_row in (1, 2):
                            db.execute("""INSERT INTO housing_transaction VALUES
                                (?,?,'11260','1126010100','가상동','apartment','sale','sale','2026-09-05',
                                   '시험 건물 가',39.75,1,2018,234000000,NULL,NULL,NULL,NULL)""",
                                (reference, source_row))
                        db.execute("UPDATE housing_snapshot SET row_count=2 WHERE id=?", (reference,))

    def page_file(self, path, content, number):
        archive = self.raw / f"{path}.xml"
        archive.write_bytes(content)
        return {"pageNo": number, "file": archive.relative_to(self.directory).as_posix(),
                "sha256": sha256(content).hexdigest()}

    def scope(self, product=PRODUCTS[0], district="11260", month="202609", size=50, pages=None):
        return {"datasetId": str(product.dataset), "districtCode": district, "contractMonth": month,
                "officialEndpoint": product.endpoint, "collectedAtUtc": "2026-11-29T00:00:00Z", "numOfRows": size,
                "pages": pages or [self.page_file(f"{product.dataset}-{district}-{month}-1", xml_page([]), 1)]}

    def write_manifest(self, scopes):
        self.manifest.write_text(json.dumps({"schemaVersion": 1, "inputKind": "fixture", "cohorts": scopes}),
                                 encoding="utf-8")

    def test_baseline_only_reads_48_groups(self):
        old_hash = fingerprint(self.db)
        result = report(self.db)
        self.assertEqual(result["cohortStates"], {"missing_api_pages": 48})
        self.assertEqual(result["csvRowsWithinOriginalWindow"], 2)
        self.assertTrue(all(c["csvScopeComplete"] for c in result["cohorts"]))
        self.assertTrue(result["baselineUnchanged"])
        self.assertEqual(fingerprint(self.db), old_hash)
        self.assertEqual(result["separateCommercialSource"]["state"], "outside_housing_schema")

    def test_whole_fixture_preserves_duplicate_rows_and_excludes_after_cutoff(self):
        scopes = []
        for product in PRODUCTS:
            for district in PRIORITY_DISTRICTS:
                for month in ("202609", "202610", "202611"):
                    picked = []
                    if (product, district, month) == (PRODUCTS[0], "11260", "202609"):
                        picked = [sale(), sale()]
                    elif (product, district, month) == (PRODUCTS[0], "11260", "202611"):
                        picked = [sale(month="202611", day="26")]
                    archive = self.page_file(f"{product.dataset}-{district}-{month}-1", xml_page(picked), 1)
                    scopes.append(self.scope(product, district, month, pages=[archive]))
        self.write_manifest(scopes)
        result = report(self.db, self.manifest)
        self.assertEqual(result["cohortStates"], {"matched_comparable_scope": 48})
        self.assertEqual(result["candidateState"], "manual_review_only")
        september, _, november = result["cohorts"][:3]
        self.assertEqual(september["apiComparableRowsInsideWindow"], 2)
        self.assertEqual(september["apiRepeatedComparableSignaturesPreserved"], 1)
        self.assertEqual(september["differences"]["equivalentComparableRows"], 2)
        self.assertEqual(november["apiReportedWholeMonthRows"], 1)
        self.assertEqual(november["apiRowsOutsideRetainedCutoff"], 1)
        self.assertEqual(november["apiComparableRowsInsideWindow"], 0)

    def test_xml_auth_error_and_dtd_are_rejected(self):
        with self.assertRaisesRegex(CheckError, "xml_provider_authorization_required"):
            parse_page(xml_page([], code="30"))
        with self.assertRaisesRegex(CheckError, "xml_dtd_not_allowed"):
            parse_page(b"<!DOCTYPE response SYSTEM 'file:///tmp/secrets'><response/>")
        with self.assertRaisesRegex(CheckError, "xml_dtd_not_allowed"):
            parse_page(b'<!DOCTYPE response [<!ENTITY xxx \"bad\">]><response>&xxx;</response>')

    def test_manifest_blocks_other_products_wrong_paths_stale_endpoints_and_dates(self):
        old = f"{PRODUCTS[2].endpoint.removesuffix('2').replace('Trade2/', 'Trade/')}"
        for change, issue in (
            ({"datasetId": COMMERCIAL.dataset}, "manifest_cohort_outside_scope_or_duplicate"),
            ({"districtCode": "11110"}, "manifest_cohort_outside_scope_or_duplicate"),
            ({"officialEndpoint": old}, "manifest_endpoint_mismatch"),
            ({"collectedAtUtc": "2026-09-15T00:00:00Z"}, "manifest_capture_predates_contract_scope_end"),
            ({"pages": [{"pageNo": 1, "file": "../page.xml", "sha256": "0" * 64}]}, "manifest_xml_relative_file_required"),
        ):
            base = self.scope(product=PRODUCTS[2])
            for key, value in change.items():
                base[key] = value
            self.write_manifest([base])
            with self.subTest(change=change), self.assertRaisesRegex(CheckError, issue):
                load_manifest(self.manifest)

    def test_page_totals_missing_middle_and_checksum(self):
        first = self.page_file("first", xml_page([sale()], number=1, size=1, total=3), 1)
        wrong_total = self.page_file("wrong", xml_page([sale(day="6")], number=2, size=1, total=4), 2)
        last = self.page_file("last", xml_page([sale(day="7")], number=3, size=1, total=3), 3)
        with self.assertRaisesRegex(CheckError, "xml_changing_total_count"):
            load_pages(self.scope(size=1, pages=[first, wrong_total]), self.directory)
        with self.assertRaisesRegex(CheckError, "xml_incomplete_page_sequence"):
            load_pages(self.scope(size=1, pages=[first, last]), self.directory)
        with self.assertRaisesRegex(CheckError, "xml_sha256_mismatch"):
            load_pages(self.scope(size=1, pages=[{**first, "sha256": "0" * 64}]), self.directory)
        with self.assertRaisesRegex(CheckError, "xml_page_receipt_mismatch"):
            load_pages(self.scope(size=2, pages=[first]), self.directory)
        short = self.page_file("short", xml_page([sale()], number=1, size=2, total=3), 1)
        with self.assertRaisesRegex(CheckError, "xml_page_row_count_mismatch"):
            load_pages(self.scope(size=2, pages=[short]), self.directory)

    def test_exact_repeated_payload_requires_review_even_reordered(self):
        first = self.page_file("first", xml_page([sale(day="5"), sale(day="6")], number=1, size=2, total=4), 1)
        other = self.page_file("other", xml_page([sale(day="6"), sale(day="5")], number=2, size=2, total=4), 2)
        with self.assertRaisesRegex(CheckError, "xml_repeated_page_items_review_required"):
            load_pages(self.scope(size=2, pages=[first, other]), self.directory)

    def test_wrong_district_month_and_legal_code_are_rejected(self):
        for row, issue in (
            (sale(sggCd="11110"), "xml_row_district_mismatch"),
            (sale(month="202610"), "xml_row_month_mismatch"),
            (sale(umdCd="10200"), "xml_legal_code_name_conflict"),
        ):
            with self.subTest(issue=issue), self.assertRaisesRegex(CheckError, issue):
                bridge(row, PRODUCTS[0], "11260", "202609", {"가상동": "1126010100"})

    def test_prices_and_cancellation_keep_distinct_rules(self):
        legal = {"가상동": "1126010100"}
        for row, product, expected_kind, problem, amount, cancel in (
            (sale(cdealDay="20260922"), PRODUCTS[0], "sale", None, (234000000, None, None), "2026-09-22"),
            (sale(cdealDay="26.09.22"), PRODUCTS[0], "sale", None, (234000000, None, None), "2026-09-22"),
            (sale(cdealType="O"), PRODUCTS[0], None, "cancellation_mark_without_date", None, None),
            (sale(amount="23,00"), PRODUCTS[0], None, "sale_amount_invalid", None, None),
            (sale(excluUseAr=""), PRODUCTS[0], None, "exclusive_area_absent_or_invalid", None, None),
            (rent(), PRODUCTS[1], "jeonse", None, (None, 123000000, 0), None),
            (rent(monthlyRent="100"), PRODUCTS[1], "monthly", None, (None, 123000000, 1000000), None),
            (rent(deposit="0"), PRODUCTS[1], None, "rent_amount_missing_or_zero_without_deposit", None, None),
        ):
            with self.subTest(product=product, problem=problem, kind=expected_kind):
                date, signature, held, canceled = bridge(row, product, "11260", "202609", legal)
                self.assertEqual(held, problem)
                self.assertEqual(date.isoformat(), "2026-09-05")
                self.assertEqual(canceled.isoformat() if canceled else None, cancel)
                if signature:
                    self.assertEqual(signature[6], expected_kind)
                    self.assertEqual(signature[7:10], amount)
                else:
                    self.assertIsNotNone(problem)

    def test_villa_uses_its_own_building_field_and_legal_name(self):
        for row, product in (
            (sale(mhouseNm="가상 연립"), PRODUCTS[2]),
            (rent(mhouseNm="가상 다세대", monthlyRent="17"), PRODUCTS[3]),
        ):
            row.pop("aptNm")
            row.pop("umdCd")
            _, key, hold, _ = bridge(row, product, "11260", "202609", {"가상동": "1126010100"})
            self.assertIsNone(hold)
            self.assertEqual(key[1], "1126010100")
            self.assertEqual(key[2], row["mhouseNm"])

    def test_future_cancellation_and_wrong_placeholder_date_hold_entire_cohort(self):
        for cancellation in ("20261216", "99.09.22"):
            with self.subTest(cancellation=cancellation):
                future = self.page_file("future", xml_page([sale(cdealDay=cancellation)]), 1)
                self.write_manifest([self.scope(pages=[future])])
                result = report(self.db, self.manifest)
                self.assertEqual(result["cohorts"][0]["reason"], "xml_cancellation_after_capture_date")
                self.assertNotIn("differences", result["cohorts"][0])
        for cancellation, reason in (
            ("2026-W39-1", "xml_cancel_date_invalid"),
            ("26.02.30", "xml_cancel_date_invalid"),
            ("00.09.22", "xml_cancel_date_precedes_contract"),
        ):
            with self.subTest(cancellation=cancellation), self.assertRaisesRegex(CheckError, reason):
                bridge(sale(cdealDay=cancellation), PRODUCTS[0], "11260", "202609", {"가상동": "1126010100"})

    def test_an_amount_and_cancellation_delta_are_comparison_hints(self):
        rows = [sale(amount="25,400"), sale(cdealDay="2026-09-22")]
        scope = self.scope(pages=[self.page_file("edits", xml_page(rows), 1)])
        self.write_manifest([scope])
        result = report(self.db, self.manifest)
        details = result["cohorts"][0]
        self.assertEqual(details["state"], "comparison_review_required")
        self.assertEqual(details["apiSaleCanceledRowsInsideWindow"], 1)
        self.assertEqual(details["differences"], {
            "equivalentComparableRows": 0, "apiOnlyComparableRows": 2, "csvOnlyComparableRows": 2,
            "possibleCancellationDateEditsWithoutRowId": 1, "signatureShapeCoincidencesWithoutRowId": 2,
        })
        self.assertEqual(result["cohortStates"], {"comparison_review_required": 1, "missing_api_pages": 47})

    def test_existing_database_sidecar_is_rejected_without_opening(self):
        file = self.db.with_name(self.db.name + "-wal")
        file.write_bytes(b"placeholder")
        with self.assertRaisesRegex(CheckError, "baseline_not_frozen"):
            report(self.db)
        self.assertEqual(file.read_bytes(), b"placeholder")


if __name__ == "__main__":
    unittest.main()
