"""서울 집계 CSV의 출처 설정·기간·원본 판독을 확인하는 오프라인 시험."""
import copy
import unittest

from seoul_activity import validate_seoul_csv


class ValidationError(Exception):
    def __init__(self, code):
        self.code = code
        super().__init__(code)


SOURCE = {"requiredColumns": ["STDR_YYQU_CD", "AREA_CD", "인구수"],
          "periodColumn": "STDR_YYQU_CD", "idColumns": ["STDR_YYQU_CD", "AREA_CD"],
          "numericColumns": ["인구수"], "unit": "명", "spatialUnit": "상권"}
HEADER = "STDR_YYQU_CD,AREA_CD,인구수\n"


class SeoulCsvValidationTests(unittest.TestCase):
    def validate(self, text, source=None, limit=10, encoding="utf-8"):
        body = text if isinstance(text, bytes) else text.encode(encoding)
        return validate_seoul_csv(body, SOURCE if source is None else source,
                                  limit, error_type=ValidationError)

    def assert_code(self, code, text, **kwargs):
        with self.assertRaises(ValidationError) as context:
            self.validate(text, **kwargs)
        self.assertEqual(code, context.exception.code)

    def test_strict_utf8_bom_and_cp949_keep_headers_and_units(self):
        for encoding in ["utf-8", "utf-8-sig", "cp949"]:
            with self.subTest(encoding=encoding):
                body = (HEADER + "20263,3110001,100\n").encode(encoding)
                before = bytes(body)
                result = self.validate(body)
                self.assertEqual((1, "20263", ["20263"]),
                                 (result["recordCount"], result["evidenceAsOf"], result["observationPeriods"]))
                self.assertEqual(("명", "상권", encoding),
                                 (result["unit"], result["spatialUnit"], result["encoding"]))
                self.assertEqual(["STDR_YYQU_CD", "AREA_CD", "인구수"], result["columns"])
                self.assertEqual(before, body)

    def test_blank_html_xml_json_errors_and_bad_encoding_are_rejected(self):
        for body in [b"", b" \r\n", b"\xef\xbb\xbf"]:
            self.assert_code("seoul_csv_empty", body)
        for body in ["<!DOCTYPE html><html>오류</html>", "<error>denied</error>",
                     '<?xml version="1.0"?><error/>', '{"error":"no data"}', '[{"error":"failed"}]',
                     HEADER + "20263,3110001,1\x00"]:
            self.assert_code("seoul_csv_not_csv", body)
        self.assert_code("seoul_csv_encoding_invalid", b"\xff\xfe\x00")
        self.assert_code("seoul_csv_encoding_invalid", b"\xef\xbb\xbf\xff")

    def test_schema_drift_duplicate_headers_and_row_shape_are_rejected(self):
        for text in ["STDR_YYQU_CD,OTHER,인구수\n20263,3110001,1\n",
                     "STDR_YYQU_CD,AREA_CD,AREA_CD,인구수\n20263,a,a,1\n",
                     "STDR_YYQU_CD,AREA_CD,인구수,\n20263,a,1,\n"]:
            self.assert_code("seoul_csv_headers_invalid", text)
        self.assert_code("seoul_csv_headers_invalid", "공급자 오류가 발생했습니다")
        for row in ["20263,a,1,extra\n", "20263,a\n"]:
            self.assert_code("seoul_csv_row_shape_invalid", HEADER + row)

    def test_quarter_period_format_and_expected_period_are_bound(self):
        for value in ["20260", "20265", "202611", "2026Q3", "", "00001"]:
            self.assert_code("seoul_csv_period_invalid", HEADER + f"{value},a,1\n")
        source = {**SOURCE, "expectedPeriod": "20263"}
        self.assertEqual(1, self.validate(HEADER + "20263,a,1\n", source)["recordCount"])
        self.assert_code("seoul_csv_period_mismatch", HEADER + "20262,a,1\n", source=source)
        self.assert_code("seoul_csv_expected_period_invalid", HEADER + "20263,a,1\n",
                         source={**SOURCE, "expectedPeriod": "20265"})

    def test_month_periods_are_opt_in_and_sorted_without_changing_precision(self):
        source = {**SOURCE, "periodFormat": "YYYYMM"}
        result = self.validate(HEADER + "202611,a,1\n202609,a,2\n", source)
        self.assertEqual(["202609", "202611"], result["observationPeriods"])
        self.assertEqual("202611", result["evidenceAsOf"])
        for period in ["202600", "202613", "20263"]:
            self.assert_code("seoul_csv_period_invalid", HEADER + f"{period},a,1\n", source=source)

    def test_spatial_area_csv_without_period_preserves_unknown_observation_time(self):
        source = {"requiredColumns": ["AREA_CD", "AREA", "X", "Y"],
                  "idColumns": ["AREA_CD"], "numericColumns": ["AREA", "X", "Y"],
                  "unit": "면적·중심좌표", "spatialUnit": "상권영역"}
        text = "AREA_CD,AREA,X,Y\na,42.5,203000,551000\n"
        for patch in [{}, {"periodColumn": None}, {"periodFormat": "YYYYMM"}]:
            result = self.validate(text, {**source, **patch})
            self.assertEqual((1, None, []),
                             (result["recordCount"], result["evidenceAsOf"], result["observationPeriods"]))
        self.assert_code("seoul_csv_config_invalid", text,
                         source={**source, "expectedPeriod": "20263"})
        self.assert_code("seoul_csv_config_invalid", text,
                         source={**source, "periodFormat": "invalid"})
        self.assert_code("seoul_csv_numeric_invalid", text.replace("203000", "-1"), source=source)

    def test_composite_ids_are_nonempty_unique_and_optional(self):
        self.assert_code("seoul_csv_id_empty", HEADER + "20263, ,1\n")
        self.assert_code("seoul_csv_id_duplicate", HEADER + "20263,a,1\n20263,a,2\n")
        self.assertEqual(2, self.validate(HEADER + "20262,a,1\n20263,a,2\n")["recordCount"])
        source = {key: copy.deepcopy(value) for key, value in SOURCE.items() if key != "idColumns"}
        self.assertEqual(2, self.validate(HEADER + "20263,a,1\n20263,a,2\n", source)["recordCount"])

    def test_numeric_cells_are_finite_nonnegative_and_missing_is_not_zero(self):
        for value in ["-1", "NaN", "inf", "-Infinity", "1e999", "missing", "1,000"]:
            escaped = '"' + value + '"' if "," in value else value
            self.assert_code("seoul_csv_numeric_invalid", HEADER + f"20263,a,{escaped}\n")
        result = self.validate(HEADER + "20263,a,\n20263,b,0\n20263,c,12.5\n")
        self.assertEqual((3, 1), (result["recordCount"], result["missingNumericCells"]))
        self.assertEqual(1, self.validate(HEADER + "20263,a,  \n")["missingNumericCells"])

    def test_record_limit_empty_rows_header_only_and_quoted_csv(self):
        self.assert_code("record_limit_exceeded", HEADER + "20263,a,1\n20263,b,2\n", limit=1)
        self.assert_code("seoul_csv_no_records", HEADER + "\n,,\n")
        self.assertEqual(1, self.validate(HEADER + '20263,"area,one",1\n')["recordCount"])
        self.assert_code("seoul_csv_syntax_invalid", HEADER + '20263,"unterminated,1\n')

    def test_invalid_configuration_does_not_silently_weaken_validation(self):
        for patch in [{"requiredColumns": []}, {"requiredColumns": "AREA_CD"},
                      {"idColumns": ["AREA_CD", "AREA_CD"]}, {"periodColumn": ""},
                      {"numericColumns": [""]}, {"periodFormat": "other"}]:
            self.assert_code("seoul_csv_config_invalid", HEADER + "20263,a,1\n", source={**SOURCE, **patch})
        for limit in [0, -1, True, 1.5]:
            self.assert_code("invalid_record_limit", HEADER + "20263,a,1\n", limit=limit)
        for column in ["idColumns", "numericColumns"]:
            self.assert_code("seoul_csv_headers_invalid", HEADER + "20263,a,1\n",
                             source={**SOURCE, column: ["unknown"]})


if __name__ == "__main__":
    unittest.main()
