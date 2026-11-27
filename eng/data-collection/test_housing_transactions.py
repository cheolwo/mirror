"""주거 자료의 원표 해석, 해제·이상값, 반복 적재와 소득 비교를 검사한다."""

import csv
from datetime import date
import io
import sqlite3
import unittest

from collect_public_snapshots import CollectionError
from housing_price_queries import compare
from housing_transactions import MOLIT_FILE_SOURCE, SCHEMA, Scope, install_snapshot, parse_rows, period_is_covered

START, END = date(2026, 10, 1), date(2026, 10, 31)
SALE = Scope("11260", "중랑구", "서울특별시", "apartment", "sale", START, END)
RENT = Scope("11260", "중랑구", "서울특별시", "apartment", "rent", START, END)
LEGAL = {"면목동": "1126010100"}
COMMON = ["NO", "시군구", "단지명", "전용면적(㎡)", "계약년월", "계약일", "층", "건축년도"]
ROW = [1, "서울특별시 중랑구 면목동", "표본 아파트", "59.9", "202610", "01", "12", "1999"]
FIRST = "2026-11-25T02:01:00Z"
SECOND = "2026-11-25T02:02:00Z"


def csv_body(group, *rows):
    stream = io.StringIO(newline="")
    writer = csv.writer(stream)
    for line in ("□ 검색조건", f"계약일자 : {START} ~ {END}", "시도 : 서울특별시", "시군구 : 중랑구",
                 f"실거래구분 : 아파트({'매매' if group == 'sale' else '전월세'})"):
        writer.writerow([line])
    extra = ["거래금액(만원)", "해제사유발생일"] if group == "sale" else ["전월세구분", "보증금(만원)", "월세금(만원)"]
    writer.writerow(COMMON + extra)
    writer.writerows(rows)
    return stream.getvalue().encode("cp949")


class HousingTransactions(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        self.db.executescript(SCHEMA.read_text(encoding="utf-8"))
        self.db.executemany("INSERT INTO housing_region VALUES (?,?,?,?,?,?)", [
            ("11260", "중랑구", None, "sigungu", "2026-10-30", "hash"),
            ("1126010100", "면목동", "11260", "legal", "2026-10-30", "hash"),
        ])
        self.db.execute("INSERT INTO housing_source VALUES (?,?,?)", (MOLIT_FILE_SOURCE, "공식", "https://rt.molit.go.kr"))
        self.db.commit()

    def test_scope_refresh_preserves_real_duplicate_rows_and_excludes_cancelled(self):
        data = csv_body("sale", ROW + ["45,000", "-"], ROW + ["45,000", "-"], ROW + ["90,000", "20261113"])
        rows = parse_rows(data, 3, SALE, LEGAL)
        install_snapshot(self.db, SALE, rows, "first", FIRST)
        install_snapshot(self.db, SALE, rows, "second", SECOND)
        self.assertEqual(3, self.db.execute("SELECT count(*) FROM housing_transaction").fetchone()[0])
        self.assertEqual(2, self.db.execute("SELECT count(*) FROM v_housing_effective_transaction").fetchone()[0])
        self.assertTrue(period_is_covered(self.db, "11260", "apartment", "sale", START, END))
        self.assertFalse(period_is_covered(self.db, "11260", "apartment", "rent", START, END))
        changes = self.db.total_changes
        result = compare(self.db, "11260", START, END, 36_000_000, legal_dong_code="1126010100",
                         area_min=50, area_max=60, savings_krw=50_000_000)
        sale = result["groups"][0]
        self.assertEqual((2, 450_000_000, 12.5, 400_000_000), (
            sale["count"], sale["medianSaleKrw"], sale["saleToAnnualIncome"], sale["savingsGapBeforeCostsKrw"]))
        self.assertEqual(changes, self.db.total_changes)

    def test_rent_conflict_stays_out_of_price_comparison_without_reclassifying(self):
        data = csv_body("rent", ROW + ["전세", "15,000", 0], ROW + ["월세", "2,000", 40],
                        ROW + ["월세", "8,000", 0])
        rows = parse_rows(data, 3, RENT, LEGAL)
        _, unpriced = install_snapshot(self.db, RENT, rows, "raw", FIRST)
        self.assertEqual(1, unpriced)
        self.assertEqual(("monthly", 0), self.db.execute(
            "SELECT deal_kind,monthly_krw FROM housing_unpriced_record").fetchone())
        _, jeonse, monthly = compare(self.db, "11260", START, END, 36_000_000)["groups"][:3]
        self.assertEqual(150_000_000, jeonse["medianDepositKrw"])
        self.assertEqual((1, 13.33, 20_000_000), (monthly["count"], monthly["annualRentToIncomePct"],
                         monthly["representativeMonthlyContract"]["depositKrw"]))

    def test_amounts_never_replace_existing_rows_for_wrong_or_incomplete_csv(self):
        data = csv_body("sale", ROW + ["45,000", "-"])
        with self.assertRaisesRegex(CollectionError, "housing_csv_count_mismatch"):
            parse_rows(data, 2, SALE, LEGAL)
        wrong_region = ROW.copy()
        wrong_region[1] = "서울특별시 광진구 중곡동"
        with self.assertRaisesRegex(CollectionError, "housing_row_region_mismatch"):
            parse_rows(csv_body("sale", wrong_region + ["45,000", 0]), 1, SALE, LEGAL)
        with self.assertRaisesRegex(CollectionError, "housing_csv_product_mismatch"):
            parse_rows(data, 1, RENT, LEGAL)
        self.assertEqual(0, self.db.execute("SELECT count(*) FROM housing_snapshot").fetchone()[0])


if __name__ == "__main__":
    unittest.main()
