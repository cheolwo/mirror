using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Warehouse;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Observation
{
    /// <summary>상호와 과거 방문 설명은 표현 문맥이다. 기존 가상 주문·메뉴·직원을 실제 사실로 바꾸지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "기존 음식 생활 Core와 별도 저장·관찰 전용 표시 문맥을 결속한다.",
        Boundary = "실제 주문/배차/통행 권위와 실제 직원·고객 정보가 아니다.")]
    public static class 사가정음식생활관찰Profile
    {
        public const string Revision = "sagajeong-food-life.r1";
        public const string SlotId = "sagajeong-food-life-r1-primary";
        public const string StoreName = "샐러리아 샌드위치&샐러드&포케 사가정점";
        public const string Address = "서울 중랑구 용마산로51길 50 1층";
        public const double Latitude = 37.5802023;
        public const double Longitude = 127.0892977;
        public const string HistorySource = "delivery:2026-09-28:visit:1";

        public static 경영SimulationSession생성Request 생성(Guid id)
            => 가상배달관찰표본.CreateNeighborhoodLife(id, dayEnabled: true);

        public static bool 관찰명령허용(string command)
            => command == "play" || command == "step" || command == "save"
                || command == "restart" || command == "overview";

        public static bool 중심주체(string id)
            => id == 가상동네생활기준.RestaurantActorId || id == "participant:synthetic:a"
                || id == "participant:synthetic:b" || id == "actor:synthetic-courier:1"
                || id == "actor:synthetic-courier:2" || id == "actor:synthetic-courier:3";

        public static 동네관찰ScreenModel 조회(경영SimulationSessionSnapshot state)
        {
            if (state == null || state.WaitingFleet?.NeighborhoodLife == null
                || !state.WaitingFleet.NeighborhoodLife.DayEnabled || state.DurationTicks != 1800)
                throw new ArgumentException("SagajeongFoodLifeSnapshotInvalid");
            var model = 동네관찰Presenter.생성(state);
            model.ObservationOnly = true;
            model.PlaceName = "사가정 · 샐러리아 음식 생활";
            model.BoundaryNotice = "실제 사가정 지도 참고 · 가상 인물과 게임용 이동 경로\n실제 직원·고객·배달 경로를 재현한 기록이 아닙니다.";
            model.Actors = model.Actors.Where(row => 중심주체(row.ActorId)).Select(row => new 동네관찰Row
            {
                Id = row.Id, ActorId = row.ActorId, RelatedId = row.RelatedId, FacilityId = row.FacilityId,
                Title = row.ActorId == 가상동네생활기준.RestaurantActorId ? "가상 음식점 담당" : row.Title,
                Body = row.Body,
            }).ToArray();
            var focusedActors = state.WaitingFleet.NeighborhoodLife.Actors.Where(actor => 중심주체(actor.ActorId)).ToArray();
            model.Summary = $"관찰 대상 {focusedActors.Length}명 · 휴식/수면 {focusedActors.Count(actor => actor.Stage == "Resting" || actor.Stage == "HomeRest" || actor.Stage == "Sleeping")}명\n"
                + $"음식 수령 {state.FoodDeliveries.Count(order => order.ReceivedTick.HasValue)}건";
            model.VisitHistory = new[]
            {
                new 동네관찰Row { Id = HistorySource, Title = "2026.09.28 · 실제 방문 기록",
                    Body = StoreName + "\n" + Address + "\n배달 방문 1회 · 동시 픽업 2건", FacilityId = "restaurant" },
                new 동네관찰Row { Id = HistorySource + ":menu", Title = "그날 기록한 메뉴",
                    Body = "훈제오리 포케\n비뷰 음료&토핑\n제로 힐링티\n오리지널 닭가슴살 샌드위치" },
                new 동네관찰Row { Id = HistorySource + ":context", Title = "일상에서 음식이 이어지는 방식",
                    Body = "메뉴를 고르고, 매장에서 준비하고, 기사가 픽업하고, 주민이 받습니다.\n이 화면의 자율 주문은 기존 가상 메뉴를 사용합니다. 과거 방문 메뉴와 현재 판매 여부는 별개입니다." },
                new 동네관찰Row { Id = HistorySource + ":boundary", Title = "실제 장소와 가상 생활",
                    Body = "매장 위치는 건물 수준 자료입니다. 출입구·주차점·주민 집·이동 경로는 게임용 구성입니다.\n실제 외관 사진과 인물 영상은 사용하지 않습니다." },
            };
            return model;
        }
    }
}
