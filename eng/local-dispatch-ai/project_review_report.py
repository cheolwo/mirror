"""모델 초안과 별도로 실제 계산값을 보여 주는 읽기 전용 검토 보고서."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re

import project_advisor as advisor


def render(run_id, output=advisor.OUTPUT, root=advisor.ROOT):
    if not re.fullmatch(r"\d{8}T\d{6}Z-[a-f0-9]{12}", run_id):
        raise ValueError("invalid_run_id")
    folder = advisor.inside(root, output)
    if not folder.is_relative_to(advisor.inside(root, "artifacts/local")):
        raise ValueError("output_must_be_local_artifact")
    run_folder = advisor.inside(folder, Path("runs") / run_id)
    raw_input = advisor.read(run_folder / "input.json")
    raw_run = advisor.read(run_folder / "run.json")
    raw_results = advisor.read(run_folder / "results.json")
    metadata, results = advisor.decode(raw_run), advisor.decode(raw_results)
    # 생성 당시 정본이 현재도 유효한지 검사한다. 모델의 자유 문장을 수치 근거로 쓰지 않는다.
    evidence, canonical_input = advisor.load_evidence(root)
    if (metadata.get("runId") != run_id or metadata.get("syntheticOnly") is not True
            or metadata.get("inputSha256") != advisor.sha(raw_input)
            or raw_input != canonical_input
            or metadata.get("campaignEvidenceId") != evidence["campaignEvidenceId"]
            or metadata.get("toolSourcesStable") is not True
            or metadata.get("stateChanges") != 0 or metadata.get("operationalApiCalls") != 0):
        raise ValueError("review_report_evidence_mismatch")
    if not isinstance(results, list) or len(results) != metadata.get("caseCount"):
        raise ValueError("review_report_result_count_mismatch")
    cases = {x["caseId"]: x for x in evidence["cases"]}
    seen = set()
    lines = ["# 계산값 대조용 배차 검토표", "",
             "합성 시나리오의 실제 정책 계산값입니다. 실배차·실제 경로·소득의 증거가 아닙니다.",
             "대안 수치는 같은 시나리오 전체 기간의 사후 비교이며 선택 시점의 미래 예측이 아닙니다.",
             "배정 대기 p95는 배정된 주문, 완료 소요 p95는 완료된 주문 중 95%가 그 이내였던 시간입니다. 미배정·미완료는 별도 열로 확인합니다.",
             "AI 초안은 별도 report.md에서 확인합니다. 형식 통과는 설명의 정확성이나 사람의 승인을 뜻하지 않습니다.", "",
             "| 비교 이름 | 의미 |", "|---|---|",
             "| observed | 관측 기반 정책 기준 |", "| policy_off | 합성 정책 OFF 비교 |",
             "| over | 과대 예측 가정 비교 |", "| oracle | 알려진 미래 도착량을 사용하는 비교 상한, 실제 AI 예측 아님 |", ""]
    for result in results:
        case_id = result.get("caseId")
        if case_id not in cases or case_id in seen:
            raise ValueError("review_report_case_mismatch")
        seen.add(case_id)
        case, facts = cases[case_id], advisor.facts_for(cases[case_id])
        if json.dumps(result.get("baseline"), sort_keys=True) != json.dumps(facts, sort_keys=True):
            raise ValueError("review_report_baseline_changed")
        if result.get("status") not in {"not_requested", "rule_reference_retained", "validated_advisory_only"}:
            raise ValueError("review_report_status_invalid")
        def display(value):
            return "미확인" if value is None else str(value)
        lines += ["## " + case["title"] + " · " + str(case["minute"]) + "분", "", "사례: `" + case_id + "`", "",
                  "| 자료 상태 | 대기 주문(건) | 목표 활동(명) | 보호 활동(명) | 추가 권고(명) | 참여 대기(명) |",
                  "|---|---:|---:|---:|---:|---:|",
                  f"| {facts['dataStatus']} | {display(facts['pendingOrders'])} | {display(facts['targetActiveDrivers'])} | {display(facts['protectedDrivers'])} | {display(facts['additionalDrivers'])} | {display(facts['waitingDrivers'])} |", "",
                  "| 비교 | 배정 대기 p95(분) | 완료 소요 p95(분) | 미배정(건) | 미완료(건) |",
                  "|---|---:|---:|---:|---:|"]
        for alternative in case["alternatives"]:
            lines.append(f"| {alternative['alternativeId']} | {alternative['assignedWaitP95Minutes']} | {alternative['completedLeadP95Minutes']} | {alternative['unassignedOrders']} | {alternative['unfinishedOrders']} |")
        status = {"not_requested": "AI 호출 없음", "rule_reference_retained": "AI 설명 거절·기준 계산 유지",
                  "validated_advisory_only": "AI 형식 검사 통과·의미 정확성과 사람 승인 미확인"}[result["status"]]
        lines += ["", status + ".", ""]
    document = "\n".join(lines)
    raw_document = document.encode("utf-8")
    report = run_folder / "verified-comparison.md"
    companion = run_folder / "verified-comparison.meta.json"
    report_metadata = {"schemaVersion": 1, "runId": run_id, "caseCount": len(results),
        "inputSha256": advisor.sha(raw_input), "runSha256": advisor.sha(raw_run),
        "resultsSha256": advisor.sha(raw_results), "reportSha256": advisor.sha(raw_document),
        "reporterSha256": advisor.sha(advisor.read(Path(__file__))),
        "modelCalls": 0, "operationalWrites": 0, "humanApproval": False,
        "semanticAccuracyMeasured": False}
    # 같은 입력의 재실행은 재사용하고 다른 내용으로 기존 보고서를 덮어쓰지 않는다.
    if report.exists() or companion.exists():
        if (not report.exists() or not companion.exists() or advisor.read(report) != raw_document
                or advisor.decode(advisor.read(companion)) != report_metadata):
            raise ValueError("review_report_existing_content_mismatch")
        return report_metadata | {"report": str(report), "reused": True}
    with report.open("xb") as stream:
        stream.write(raw_document)
    try:
        advisor.write_json(companion, report_metadata)
    except OSError:
        # 이번 호출이 만든 동일한 파일만 정리한다. 기존·변경된 자료는 지우지 않는다.
        if report.exists() and advisor.read(report) == raw_document:
            report.unlink()
        raise
    return report_metadata | {"report": str(report), "reused": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--output", default=str(advisor.OUTPUT))
    args = parser.parse_args()
    try:
        print(json.dumps(render(args.run, args.output), ensure_ascii=False, indent=2))
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(2, "대조 보고서 중단: " + type(error).__name__ + "\n")


if __name__ == "__main__":
    main()
