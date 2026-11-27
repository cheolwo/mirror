"""주택 실거래 표본과 입력한 연소득을 비교한다."""

from datetime import date
from decimal import Decimal, ROUND_HALF_UP
import math
import re


HOUSING_TYPES = ("apartment", "villa")
DEAL_KINDS = ("sale", "jeonse", "monthly")


def median_krw(values):
    ordered = sorted(value for value in values if value is not None)
    middle = len(ordered) // 2
    if not ordered:
        return None
    if len(ordered) % 2:
        return ordered[middle]
    return (ordered[middle - 1] + ordered[middle] + 1) // 2


def rounded_ratio(value, income, multiplier=1):
    if value is None or income is None:
        return None
    ratio = Decimal(value) * multiplier / income
    return float(ratio.quantize(Decimal("0.01"), rounding=ROUND_HALF_UP))


def representative_monthly(rows, monthly, deposit):
    paired = [row for row in rows if row[1] is not None and row[2] is not None]
    if not paired:
        return None
    _, actual_deposit, actual_monthly, contract_date, area = min(
        paired, key=lambda row: (
            abs(row[2] - monthly), abs(row[1] - deposit), row[2], row[1], row[3],
            row[4] is None, row[4],
        ),
    )
    return {"depositKrw": actual_deposit, "monthlyKrw": actual_monthly,
            "contractDate": contract_date, "areaM2": area}


def summarize(housing_type, deal_kind, rows, income, savings):
    sale = median_krw(row[0] for row in rows) if deal_kind == "sale" else None
    deposit = median_krw(row[1] for row in rows) if deal_kind in ("jeonse", "monthly") else None
    monthly = median_krw(row[2] for row in rows) if deal_kind == "monthly" else None
    upfront = sale if deal_kind == "sale" else deposit
    return {
        "housingType": housing_type,
        "dealKind": deal_kind,
        "count": len(rows),
        "saleAmountCount": sum(row[0] is not None for row in rows) if deal_kind == "sale" else 0,
        "depositAmountCount": sum(row[1] is not None for row in rows) if deal_kind != "sale" else 0,
        "monthlyAmountCount": sum(row[2] is not None for row in rows) if deal_kind == "monthly" else 0,
        "medianSaleKrw": sale,
        "medianDepositKrw": deposit,
        "medianMonthlyKrw": monthly,
        "saleToAnnualIncome": rounded_ratio(sale, income),
        "depositToAnnualIncome": rounded_ratio(deposit, income) if deal_kind == "jeonse" else None,
        "annualRentToIncomePct": rounded_ratio(monthly, income, 1200),
        "representativeMonthlyContract": representative_monthly(rows, monthly, deposit) if deal_kind == "monthly" else None,
        "savingsGapBeforeCostsKrw": max(0, upfront - savings) if upfront is not None and savings is not None else None,
    }


def compare(db, district_code: str, start: date, end: date, income_krw: int | None = None,
            *, legal_dong_code: str | None = None, area_min: float | None = None,
            area_max: float | None = None, savings_krw: int | None = None) -> dict:
    """계약일과 전용면적을 양끝 포함으로 비교한다. 소득과 보유 현금은 DB에 쓰지 않는다."""
    if not isinstance(district_code, str) or not re.fullmatch(r"[0-9]{5}", district_code):
        raise ValueError("district_code_invalid")
    if type(start) is not date or type(end) is not date or start > end:
        raise ValueError("contract_date_range_invalid")
    if income_krw is not None and (type(income_krw) is not int or income_krw <= 0):
        raise ValueError("annual_income_invalid")
    if savings_krw is not None and (type(savings_krw) is not int or savings_krw < 0):
        raise ValueError("savings_invalid")
    for area in (area_min, area_max):
        if area is not None and (type(area) not in (int, float) or not math.isfinite(area) or area < 0):
            raise ValueError("exclusive_area_invalid")
    if area_min is not None and area_max is not None and area_min > area_max:
        raise ValueError("exclusive_area_range_invalid")

    district = db.execute("SELECT name, as_of FROM housing_region WHERE kind='sigungu' AND code=?",
                          (district_code,)).fetchone()
    legal_dong = None
    filters = ["district_code=?", "contract_date BETWEEN ? AND ?", "canceled_on IS NULL"]
    values = [district_code, start.isoformat(), end.isoformat()]
    if legal_dong_code is not None:
        if (not isinstance(legal_dong_code, str) or not re.fullmatch(r"[0-9]{10}", legal_dong_code)
                or not legal_dong_code.startswith(district_code)):
            raise ValueError("legal_dong_code_invalid")
        legal_dong = db.execute(
            "SELECT name, as_of FROM housing_region WHERE kind='legal' AND code=? AND parent_code=?",
            (legal_dong_code, district_code),
        ).fetchone()
        filters.append("legal_dong_code=?")
        values.append(legal_dong_code)
    if area_min is not None:
        filters.append("area_m2 >= ?")
        values.append(area_min)
    if area_max is not None:
        filters.append("area_m2 <= ?")
        values.append(area_max)

    groups = {(housing_type, deal_kind): []
              for housing_type in HOUSING_TYPES for deal_kind in DEAL_KINDS}
    rows = db.execute(
        "SELECT housing_type, deal_kind, sale_krw, deposit_krw, monthly_krw, contract_date, area_m2 "
        "FROM housing_transaction WHERE " + " AND ".join(filters),
        values,
    )
    for housing_type, deal_kind, *row in rows:
        if (housing_type, deal_kind) in groups:
            groups[housing_type, deal_kind].append(row)
    return {
        "districtCode": district_code,
        "districtName": district[0] if district else None,
        "legalDongCode": legal_dong_code,
        "legalDongName": legal_dong[0] if legal_dong else None,
        "regionAsOf": district[1] if district else None,
        "start": start.isoformat(),
        "end": end.isoformat(),
        "areaMinM2": area_min,
        "areaMaxM2": area_max,
        "annualGrossIncomeKrw": income_krw,
        "savingsKrw": savings_krw,
        "groups": [summarize(housing_type, deal_kind, rows, income_krw, savings_krw)
                   for (housing_type, deal_kind), rows in groups.items()],
    }
