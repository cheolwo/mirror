using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Data.WorldProjection
{
    public enum 방문재생상태 { Empty, Ready, Playing, Paused, Ended }

    /// <summary>서버 업무 상태가 아닌 기록 재생의 한 순간입니다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "방문 순서 재생의 현재 위치·방문·연출 시간을 불변 값으로 반환한다.",
        Boundary = "실제 기사 GPS, 주행선, 픽업/배송 완료 또는 Unity 객체가 아니다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3Unity소비자회귀)]
    public sealed class 방문재생Frame
    {
        internal 방문재생Frame(방문재생상태 state, string visitId, string nextVisitId,
            int sequence, bool moving, double x, double z, double progress, double elapsed)
        {
            State = state; VisitId = visitId; NextVisitId = nextVisitId;
            Sequence = sequence; IsMoving = moving; X = x; Z = z;
            SegmentProgress = progress; PresentationSeconds = elapsed;
        }

        public 방문재생상태 State { get; }
        public string VisitId { get; }
        public string NextVisitId { get; }
        public int Sequence { get; }
        public bool IsMoving { get; }
        public double X { get; }
        public double Z { get; }
        public double SegmentProgress { get; }
        public double PresentationSeconds { get; }
    }

    /// <summary>
    /// 검증된 방문 사본을 순서대로 읽는 메모리 전용 연출입니다.
    /// 경과 시간은 프레임별 누적 대신 기준 시계에서 계산하며, 연결선은 실제 주행선이 아닙니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "준비된 방문의 재생·정지·번호 선택·배속·종료를 결정적 연출 시계로 관리한다.",
        Boundary = "DB·전송·Simulation Save/Replay·업무 Event·Unity Play Mode 검증은 포함하지 않는다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
    public sealed class 방문순서재생Session
    {
        private readonly double 정차연출초;
        private readonly double 이동연출초;
        private 준비된방문[] 방문들 = Array.Empty<준비된방문>();
        private double? 마지막시계;
        private double 기준시계;
        private double 기준재생초;
        private double 현재재생초;

        public 방문순서재생Session(double dwellSeconds = 2, double travelSeconds = 4)
        {
            if (!유한(dwellSeconds) || dwellSeconds < 0 || !유한(travelSeconds)
                || travelSeconds <= 0 || !유한(dwellSeconds + travelSeconds))
                throw new ArgumentException("VisitPlaybackDurationInvalid");
            정차연출초 = dwellSeconds;
            이동연출초 = travelSeconds;
        }

        public 방문재생상태 State { get; private set; } = 방문재생상태.Empty;
        public string RecordId { get; private set; } = string.Empty;
        public string RecordRevision { get; private set; } = string.Empty;
        public string InputFingerprint { get; private set; } = string.Empty;
        public string DiagnosticCode { get; private set; } = string.Empty;
        public double PlaybackRate { get; private set; } = 1;
        public double DurationSeconds { get; private set; }
        public IReadOnlyList<준비된방문> Visits => Array.AsReadOnly(방문들);

        /// <summary>같은 판본 재전달은 진행을 초기화하지 않습니다. 실패는 기존 표시 사본을 보존합니다.</summary>
        public bool Load(방문재생준비Result prepared, double monotonicSeconds)
        {
            if (prepared == null || !prepared.CanReplay || prepared.Visits.Count == 0
                || string.IsNullOrWhiteSpace(prepared.InputFingerprint))
                return 거절("VisitPlaybackPreparationRequired");
            if (!시계검사(monotonicSeconds)) return false;
            var duration = prepared.Visits.Count * 정차연출초
                + (prepared.Visits.Count - 1) * 이동연출초;
            if (!유한(duration)) return 거절("VisitPlaybackDurationOverflow");
            if (prepared.RecordId == RecordId && prepared.RecordRevision == RecordRevision
                && prepared.InputFingerprint != InputFingerprint)
                return 거절("VisitPlaybackRevisionConflict");
            if (prepared.InputFingerprint == InputFingerprint
                && prepared.RecordId == RecordId && prepared.RecordRevision == RecordRevision)
                return Advance(monotonicSeconds);

            방문들 = prepared.Visits.ToArray();
            RecordId = prepared.RecordId;
            RecordRevision = prepared.RecordRevision;
            InputFingerprint = prepared.InputFingerprint;
            DurationSeconds = duration;
            현재재생초 = 기준재생초 = 0;
            기준시계 = monotonicSeconds;
            마지막시계 = monotonicSeconds;
            State = 방문재생상태.Ready;
            DiagnosticCode = string.Empty;
            return true;
        }

        public bool Play(double monotonicSeconds)
        {
            if (State == 방문재생상태.Empty) return 거절("VisitPlaybackNotLoaded");
            if (!Advance(monotonicSeconds)) return false;
            if (State == 방문재생상태.Playing || State == 방문재생상태.Ended) return true;
            기준재생초 = 현재재생초;
            기준시계 = monotonicSeconds;
            State = 방문재생상태.Playing;
            return true;
        }

        public bool Pause(double monotonicSeconds)
        {
            if (State == 방문재생상태.Empty) return 거절("VisitPlaybackNotLoaded");
            if (!Advance(monotonicSeconds)) return false;
            if (State != 방문재생상태.Ended) State = 방문재생상태.Paused;
            기준재생초 = 현재재생초;
            기준시계 = monotonicSeconds;
            return true;
        }

        /// <summary>선택한 방문에 멈춥니다. 실제 방문/픽업 사건을 다시 발행하지 않습니다.</summary>
        public bool SeekVisit(string visitId, double monotonicSeconds)
        {
            var index = Array.FindIndex(방문들, v => v.VisitId == visitId);
            if (index < 0) return 거절("VisitPlaybackVisitNotFound");
            if (!시계검사(monotonicSeconds)) return false;
            현재재생초 = 기준재생초 = index * (정차연출초 + 이동연출초);
            기준시계 = monotonicSeconds;
            마지막시계 = monotonicSeconds;
            State = 방문재생상태.Paused;
            DiagnosticCode = string.Empty;
            return true;
        }

        public bool SetPlaybackRate(double rate, double monotonicSeconds)
        {
            if (!유한(rate) || rate <= 0 || rate > 16) return 거절("VisitPlaybackRateInvalid");
            if (State == 방문재생상태.Empty) return 거절("VisitPlaybackNotLoaded");
            if (!Advance(monotonicSeconds)) return false;
            기준재생초 = 현재재생초;
            기준시계 = monotonicSeconds;
            PlaybackRate = rate;
            return true;
        }

        public bool Advance(double monotonicSeconds)
        {
            if (State == 방문재생상태.Empty) return 거절("VisitPlaybackNotLoaded");
            if (!시계검사(monotonicSeconds)) return false;
            if (State == 방문재생상태.Playing)
            {
                var elapsed = (monotonicSeconds - 기준시계) * PlaybackRate;
                // 오랜 정지 뒤 큰 경과 시간도 끝으로 닫되 Infinity를 위치 계산에 전달하지 않습니다.
                if (elapsed >= DurationSeconds - 기준재생초)
                {
                    현재재생초 = DurationSeconds;
                    State = 방문재생상태.Ended;
                }
                else 현재재생초 = 기준재생초 + elapsed;
            }
            마지막시계 = monotonicSeconds;
            DiagnosticCode = string.Empty;
            return true;
        }

        public 방문재생Frame CurrentFrame
        {
            get
            {
                if (방문들.Length == 0)
                    return new 방문재생Frame(State, string.Empty, string.Empty, 0, false, 0, 0, 0, 0);
                var cycle = 정차연출초 + 이동연출초;
                var index = State == 방문재생상태.Ended ? 방문들.Length - 1
                    : Math.Min(방문들.Length - 1, (int)Math.Floor(현재재생초 / cycle));
                // 나눗셈 반올림으로 정확한 방문 시작점이 직전 구간으로 내려가지 않게 합니다.
                while (index < 방문들.Length - 1 && 현재재생초 >= (index + 1) * cycle) index++;
                while (index > 0 && State != 방문재생상태.Ended && 현재재생초 < index * cycle) index--;
                var visit = 방문들[index];
                var within = 현재재생초 - index * cycle;
                var betweenVisits = index < 방문들.Length - 1 && within >= 정차연출초;
                var progress = betweenVisits ? Math.Max(0, Math.Min(1, (within - 정차연출초) / 이동연출초)) : 0;
                var next = betweenVisits ? 방문들[index + 1] : visit;
                return new 방문재생Frame(State, visit.VisitId, betweenVisits ? next.VisitId : string.Empty,
                    visit.Sequence, betweenVisits && State == 방문재생상태.Playing,
                    (1 - progress) * visit.X + progress * next.X,
                    (1 - progress) * visit.Z + progress * next.Z, progress, 현재재생초);
            }
        }

        public void Clear()
        {
            방문들 = Array.Empty<준비된방문>();
            RecordId = RecordRevision = InputFingerprint = DiagnosticCode = string.Empty;
            마지막시계 = null;
            기준시계 = 기준재생초 = 현재재생초 = DurationSeconds = 0;
            PlaybackRate = 1;
            State = 방문재생상태.Empty;
        }

        private bool 시계검사(double value)
        {
            if (!유한(value) || value < 0) return 거절("VisitPlaybackClockInvalid");
            if (마지막시계.HasValue && value < 마지막시계.Value)
                return 거절("VisitPlaybackClockReversed");
            return true;
        }

        private static bool 유한(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private bool 거절(string code) { DiagnosticCode = code; return false; }
    }
}
