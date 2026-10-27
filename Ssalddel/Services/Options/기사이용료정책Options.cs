namespace 살뜰.Services.Options
{
    public sealed class 기사이용료정책Options
    {
        public const string SectionName = "DriverUsagePolicy";

        public bool 무료배차 { get; set; } = true;
        public decimal 기본이용료 { get; set; } = 0m;
        public decimal 월상한이용료 { get; set; } = 0m;

        public decimal 추가이용료
        {
            get => 월상한이용료;
            set => 월상한이용료 = value;
        }

        public decimal 적용월상한이용료 => 무료배차 ? 0m : 월상한이용료;

        public decimal 월누적이용료계산(int 배차건수)
        {
            if (무료배차 || 배차건수 <= 0)
            {
                return 0m;
            }

            return Math.Min(배차건수 * 기본이용료, 월상한이용료);
        }
    }
}
