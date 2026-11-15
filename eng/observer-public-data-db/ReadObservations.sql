-- 읽기 전용 예시. 이번 내부 검토 판본만 조회하며 게임/운영 상태를 변경하지 않는다.
-- EvidenceAsOfUtc는 TemporalPrecisionCode에 따른 기간 대표일이다.
SELECT n.SourceId, n.DatasetId, n.MetricCode, n.UnitCode,
       n.SpatialPrecisionCode, n.TemporalPrecisionCode,
       COUNT(*) AS StoredRows,
       SUM(n.NumericValue IS NULL) AS MissingRows,
       MIN(n.EvidenceAsOfUtc) AS FirstPeriodAnchorUtc,
       MAX(n.EvidenceAsOfUtc) AS LastPeriodAnchorUtc,
       COUNT(DISTINCT n.RegionStableId) AS ProviderRegions
FROM public_data_normalized_records AS n
WHERE n.DataRevision LIKE 'observer-db-preparation.r2:sha256:%'
GROUP BY n.SourceId, n.DatasetId, n.MetricCode, n.UnitCode,
         n.SpatialPrecisionCode, n.TemporalPrecisionCode;

-- 조사 후보 안내다. 실제 외부 관측값이나 26개 자료 취득 성공을 뜻하지 않는다.
SELECT r.DatasetId AS ResearchId, r.SourceVersion, r.ContentHashSha256,
       r.StorageObjectName, r.EvidenceAsOfUtc, i.ErrorSummary
FROM public_data_raw_snapshots AS r
JOIN public_data_ingestion_runs AS i ON i.Id = r.FirstCollectionRunId
WHERE r.SourceId = 'observer-public-data-research';

-- 원본과 수집 계보의 외래키를 확인한다. 출처 조건은 원본/안내 JSON을 함께 읽는다.
SELECT n.SourceId, n.DatasetId, n.StableId, n.RegionStableId,
       n.MetricCode, n.NumericValue, n.UnitCode,
       n.EvidenceAsOfUtc, n.TemporalPrecisionCode, n.QualityCode,
       n.LimitationCode, r.ContentHashSha256, r.StorageObjectName,
       i.RunKey, n.DataRevision
FROM public_data_normalized_records AS n
JOIN public_data_raw_snapshots AS r ON r.Id = n.RawSnapshotId
JOIN public_data_ingestion_runs AS i ON i.Id = r.FirstCollectionRunId
WHERE n.DataRevision LIKE 'observer-db-preparation.r2:sha256:%'
ORDER BY n.Id
LIMIT 100;
