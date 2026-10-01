namespace Ssalddel.Simulation.Contracts
{
    public static class 프랜차이즈공급망SimulationCodes
    {
        public const string 검증표본 = "VerificationSample";
        public const string SessionAggregate권위 = "SessionAggregate";

        public const string 본부재판매 = "HeadquartersResale";
        public const string 공급사매장직납 = "SupplierDirectToStore";

        public const string 본부자체배송 = "HeadquartersFleet";
        public const string 공급사배송 = "Supplier";
        public const string 제삼자운송 = "ThirdPartyCarrier";
        public const string 위탁이행 = "EntrustedFulfillment";

        public const string 활성 = "Active";
        public const string 일시중지 = "Suspended";
        public const string 종료 = "Ended";

        public const string 계약초안 = "Draft";
        public const string 계약확정 = "Confirmed";
        public const string 계약활성 = "Active";
        public const string 계약일시중지 = "Suspended";
        public const string 계약종료 = "Ended";

        public const string 발주초안 = "Draft";
        public const string 발주제출 = "Submitted";
        public const string 발주수락 = "Accepted";
        public const string 발주부분수락 = "PartiallyAccepted";
        public const string 발주거절 = "Rejected";
        public const string 발주철회 = "Withdrawn";
    }
}
