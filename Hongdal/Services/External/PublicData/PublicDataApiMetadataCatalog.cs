using Hongdal.Contracts.Common.PublicData;

namespace 홍달.Services.External.PublicData;

public sealed class PublicDataApiMetadataCatalog : IPublicDataApiMetadataCatalog
{
    private static readonly IReadOnlyList<PublicDataApiMetadataItem> Catalog =
    [
        new()
        {
            Key = "juso-road-address-search",
            Provider = "행정안전부",
            DisplayName = "실시간 주소정보 조회 API",
            Purpose = "사용자가 입력한 주소를 도로명/지번/우편번호 기준으로 표준화합니다.",
            Domain = "Address",
            VersionScope = "2.5",
            ApiType = "REST",
            DataFormat = "JSON/XML",
            BaseUrl = "https://business.juso.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15057017/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = true,
            ContainsPersonalData = false,
            MainParameters = ["confmKey", "currentPage", "countPerPage", "keyword", "resultType"],
            MainResponseFields = ["roadAddr", "jibunAddr", "zipNo", "admCd", "rnMgtSn", "bdMgtSn"],
            UsageNotes =
            [
                "공동주택 단지 후보 조회 전에 주소 문자열을 표준화하는 1차 관문으로 사용합니다.",
                "상세주소, 동/호 정보는 직접 공개하지 않고 내부 단지 소속 확인 흐름에서만 사용합니다."
            ]
        },
        new()
        {
            Key = "kapt-apartment-complex-list",
            Provider = "국토교통부",
            DisplayName = "공동주택 단지 목록제공 서비스",
            Purpose = "시도/시군구/읍면동/도로명주소 기반으로 공동주택 단지 후보를 조회합니다.",
            Domain = "ApartmentComplex",
            VersionScope = "2.5",
            ApiType = "REST",
            DataFormat = "JSON/XML",
            BaseUrl = "https://apis.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15057332/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = true,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "sidoCode", "sigunguCode", "dongCode", "roadName"],
            MainResponseFields = ["kaptCode", "kaptName", "as1", "as2", "as3", "as4"],
            UsageNotes =
            [
                "외부 단지 코드는 내부 공동주택 식별자로 바로 노출하지 않고 매핑 테이블을 둡니다.",
                "동명이거나 주소가 유사한 단지가 있을 수 있으므로 주소 표준화 결과와 함께 후보 상태로 보관합니다."
            ]
        },
        new()
        {
            Key = "kapt-apartment-complex-basic",
            Provider = "국토교통부",
            DisplayName = "공동주택 기본 정보제공 서비스",
            Purpose = "단지의 동수, 세대수, 관리 방식, 설비 등 기본 정보를 확인합니다.",
            Domain = "ApartmentComplex",
            VersionScope = "2.5",
            ApiType = "REST",
            DataFormat = "JSON/XML",
            BaseUrl = "https://apis.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15058453/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = true,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "kaptCode"],
            MainResponseFields = ["kaptCode", "kaptName", "hoCnt", "dongCnt", "kaptdWtimebus", "kaptdPcnt"],
            UsageNotes =
            [
                "공동 주문 목표 수량과 단지 내 분류 규모를 추정할 때 참고 정보로 사용합니다.",
                "관리사무소 승인 또는 공식 협약을 대체하는 데이터로 사용하지 않습니다."
            ]
        },
        new()
        {
            Key = "standard-apartment-complex-data",
            Provider = "국토교통부",
            DisplayName = "전국공동주택표준데이터",
            Purpose = "공동주택 표준 데이터셋을 보강 데이터로 활용합니다.",
            Domain = "ApartmentComplex",
            VersionScope = "2.5",
            ApiType = "REST/File",
            DataFormat = "JSON/CSV",
            BaseUrl = "https://www.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15096285/standard.do",
            RequiresServiceKey = true,
            ContainsResidentialData = true,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "page", "perPage", "cond"],
            MainResponseFields = ["단지명", "법정동주소", "도로명주소", "세대수", "동수"],
            UsageNotes =
            [
                "K-apt 단지 목록과 주소 검색 API 결과를 보강하는 용도로 사용합니다.",
                "갱신 주기와 제공 필드가 API별로 다를 수 있으므로 동기화 시점을 기록합니다."
            ]
        },
        new()
        {
            Key = "semas-traditional-market-status",
            Provider = "소상공인시장진흥공단",
            DisplayName = "전통시장 현황",
            Purpose = "시장코드와 지역, 편의·안전·물류시설 현황을 동기화해 소상공인 중심의 지역 기준정보로 사용합니다.",
            Domain = "TraditionalMarket",
            VersionScope = "1.0",
            ApiType = "REST/File",
            DataFormat = "JSON/CSV",
            BaseUrl = "https://api.odcloud.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15052837/fileData.do?recommendDataYn=Y",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "page", "perPage", "returnType"],
            MainResponseFields = ["시장코드", "시장명", "시장 유형", "도로명주소", "시도", "시군구", "공동물류창고_보유여부", "시장전용 고객주차장_보유여부"],
            UsageNotes =
            [
                "시장코드를 내부 안정 식별자와 시장 단위 커뮤니티 범위 키의 기반으로 사용합니다.",
                "연간 기준 데이터이므로 시설의 현재 운영 여부나 입점 사업자의 권한을 증명하는 자료로 사용하지 않습니다."
            ]
        },
        new()
        {
            Key = "mfds-imported-food-product-db",
            Provider = "식품의약품안전처",
            DisplayName = "수입식품 제품DB 정보",
            Purpose = "영상·게시글·수입 검토에서 발견된 제품명을 식약처 수입식품 관리번호와 공식 품목 후보로 연결합니다.",
            Domain = "ImportedFood",
            VersionScope = "2.0",
            ApiType = "REST",
            DataFormat = "JSON/XML",
            BaseUrl = "https://apis.data.go.kr/1471000/IprtFoodPrdtDBService02",
            DocumentationUrl = "https://www.data.go.kr/data/15073949/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "pageNo", "numOfRows", "type", "DCLR_PRDT_DIVS_NM", "MNFT_NATN_NM", "PRDT_NM", "PRDLST_NM"],
            MainResponseFields = ["IPRT_FOOD_MNG_NO", "PRDT_NM", "PRDLST_CD", "PRDLST_NM", "MNFT_NATN_CD", "MNFT_NATN_NM"],
            UsageNotes =
            [
                "수입식품 관리번호를 공식 제품 후보의 기준 식별자로 보관합니다.",
                "식약처 품목코드는 관세청 HSK 코드가 아니므로 자동 변환하거나 같은 코드로 취급하지 않습니다."
            ]
        },
        new()
        {
            Key = "mfds-imported-food-overseas-manufacturer",
            Provider = "식품의약품안전처",
            DisplayName = "수입식품 해외제조업소 정보",
            Purpose = "해외제조업소의 공식 코드, 소재 국가, 업종, 안전관리 인증과 취소·중단 상태를 확인합니다.",
            Domain = "ImportedFood",
            VersionScope = "2.0",
            ApiType = "REST",
            DataFormat = "JSON/XML",
            BaseUrl = "https://apis.data.go.kr/1471000/IprtFoodOvseaMnftBsshInfoService02",
            DocumentationUrl = "https://www.data.go.kr/data/15073967/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "pageNo", "numOfRows", "type", "OCTR_MNFT_BSSH_NM", "FOOD_SE_NM", "NATN_NM"],
            MainResponseFields = ["OCTR_MNFT_BSSH_CD", "OCTR_MNFT_BSSH_NM", "OCTR_MNFT_BSSH_ADDR", "NATN_NM", "FOOD_SAFE_MNG_SYS_CERT_YN", "RTRCN_SUSP_NM", "IPRT_SUSP_NO"],
            UsageNotes =
            [
                "제조업소명보다 식약처 해외제조업소 코드를 우선 식별자로 사용합니다.",
                "인증·취소·수입중단 상태는 바뀔 수 있으므로 조회시각을 기록하고 실제 수입 전에 다시 확인합니다."
            ]
        },
        new()
        {
            Key = "mfds-imported-food-korean-label",
            Provider = "식품의약품안전처",
            DisplayName = "수입식품 제품별 한글표시사항",
            Purpose = "수입제품의 한글·영문명, 수입업체, 해외제조업소, 원재료와 변환된 한글표시사항을 조회합니다.",
            Domain = "ImportedFood",
            VersionScope = "2.0",
            ApiType = "REST",
            DataFormat = "JSON/XML",
            BaseUrl = "https://apis.data.go.kr/1471000/IprtFoodPrdtKoreanLabelingItem",
            DocumentationUrl = "https://www.data.go.kr/data/15110214/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "pageNo", "numOfRows", "type", "prductKoreanNm", "prductNm", "ovsmnfstNm", "itmNm", "mnfNtncdNm", "irdntNm", "procsDtmStart", "procsDtmEnd"],
            MainResponseFields = ["PRDUCT_KOREAN_NM", "PRDUCT_NM", "BSN_OFC_NAME", "OVSMNFST_NM", "ITM_NM", "MNF_NTNCD_NM", "KORLABEL", "IRDNT_NM", "PROCS_DTM"],
            UsageNotes =
            [
                "이 데이터에는 수입식품 관리번호와 해외제조업소 코드가 없으므로 이름·국가·품목 기반 연결은 확정값이 아니라 후보로 보관합니다.",
                "원재료 텍스트는 제품 표시 정보이며 원료 사용 가능 여부 판정을 대신하지 않습니다. 별도 원료정보 API와 규격정보 확인이 필요합니다."
            ]
        },
        new()
        {
            Key = "customs-cargo-tracking",
            Provider = "관세청/공공데이터포털",
            DisplayName = "화물 통관 진행 정보 조회",
            Purpose = "수입 화물의 통관 진행 상태를 조회해 FCL/LCL 운송 계획에 참고합니다.",
            Domain = "Customs",
            VersionScope = "2.0",
            ApiType = "REST",
            DataFormat = "XML",
            BaseUrl = "https://apis.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "cargMtNo", "mblNo", "hblNo"],
            MainResponseFields = ["csclPrgsStts", "shedNm", "prcsDttm"],
            UsageNotes =
            [
                "통관 상태는 운송 가능성 판단의 참고값이며, 기본 운송 요청을 무조건 차단하는 게이트로 사용하지 않습니다.",
                "조회 결과 원문과 해석 결과를 분리해 저장합니다."
            ]
        },
        new()
        {
            Key = "customs-hs-country-import-statistics",
            Provider = "관세청",
            DisplayName = "품목별 국가별 수출입실적",
            Purpose = "HS 코드와 수출국별 수입금액·순중량을 조회해 기간 가중평균 CIF 단가를 계산합니다.",
            Domain = "Customs",
            VersionScope = "2.5",
            ApiType = "REST",
            DataFormat = "XML",
            BaseUrl = "https://apis.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15100475/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "strtYymm", "endYymm", "hsSgn", "cntyCd"],
            MainResponseFields = ["year", "hsCd", "statCd", "impWgt", "impDlr"],
            UsageNotes =
            [
                "수입금액 합계를 순중량 합계로 나눈 가중평균을 사용하고 월별 단가의 단순평균은 사용하지 않습니다.",
                "CIF 통계단가는 관세·부가세·검역·통관·국내 물류비와 판매마진을 포함한 소비자가격이 아닙니다."
            ]
        },
        new()
        {
            Key = "customs-confirmation-requirements",
            Provider = "관세청",
            DisplayName = "세관장확인대상물품",
            Purpose = "10자리 HSK 코드로 수입 시 확인해야 할 법령, 승인기관과 구비요건을 조회합니다.",
            Domain = "Customs",
            VersionScope = "2.5",
            ApiType = "REST",
            DataFormat = "XML",
            BaseUrl = "https://apis.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15101589/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "hsSgn", "imexTpcd"],
            MainResponseFields = ["hsSgn", "dcerCfrmLworNm", "reqApreIttNm", "reqCfrmIstmNm", "aplyStrtDt"],
            UsageNotes =
            [
                "조회 결과가 없다는 사실만으로 수입요건이 없거나 통관이 가능하다고 확정하지 않습니다.",
                "법령·승인기관·구비서류가 반환되면 관세사 또는 해당 승인기관의 확인이 필요한 정보로 표시합니다."
            ]
        },
        new()
        {
            Key = "customs-weekly-exchange-rate",
            Provider = "관세청",
            DisplayName = "관세환율정보",
            Purpose = "수입신고 과세가격을 원화로 환산할 때 적용되는 국가·통화별 관세환율을 조회합니다.",
            Domain = "Customs",
            VersionScope = "2.5",
            ApiType = "REST",
            DataFormat = "XML",
            BaseUrl = "https://apis.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15101230/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "aplyBgnDt", "weekFxrtTpcd"],
            MainResponseFields = ["cntySgn", "mtryUtNm", "fxrt", "currSgn", "aplyBgnDt", "imexTp"],
            UsageNotes =
            [
                "일반 환전 시세가 아니라 관세 과세가격 계산에 사용하는 주간 환율입니다.",
                "요청 국가부호와 일치하는 결과만 HS 공공데이터 묶음에 포함합니다."
            ]
        },
        new()
        {
            Key = "customs-hs-code-annual-file",
            Provider = "관세청",
            DisplayName = "연례 HS 부호 기준정보",
            Purpose = "HSK 코드의 한글·영문 품명, 수량·중량 단위와 성질 분류를 내부 HS 기준정보 갱신에 사용합니다.",
            Domain = "Customs",
            VersionScope = "2.5",
            ApiType = "File",
            DataFormat = "XLSX",
            BaseUrl = "https://www.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15049722/fileData.do",
            RequiresServiceKey = false,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["catalogYear"],
            MainResponseFields = ["HSK 코드", "한글품목명", "영문품목명", "수량단위코드", "중량단위코드", "성질통합분류코드"],
            UsageNotes =
            [
                "연간 파일은 실시간 묶음 조회 대상이 아니라 내부 HS 코드 카탈로그 갱신 기준으로 관리합니다.",
                "연도별 코드 신설·폐지·품명 변경을 검토한 뒤 버전 단위로 반영합니다."
            ]
        },
        new()
        {
            Key = "at-daily-wholesale-retail-food-price",
            Provider = "한국농수산식품유통공사(aT)",
            DisplayName = "일별 도·소매 가격정보",
            Purpose = "국내 농축수산물의 최근 중도매·소매가격을 kg 기준으로 정규화해 HS 수입 기준가격과 비교합니다.",
            Domain = "FoodPrice",
            VersionScope = "2.5",
            ApiType = "REST",
            DataFormat = "JSON/XML",
            BaseUrl = "https://apis.data.go.kr",
            DocumentationUrl = "https://www.data.go.kr/data/15156057/openapi.do",
            RequiresServiceKey = true,
            ContainsResidentialData = false,
            ContainsPersonalData = false,
            MainParameters = ["serviceKey", "cond[exmn_ymd::GTE]", "cond[exmn_ymd::LTE]", "cond[ctgry_cd::EQ]", "cond[item_cd::EQ]"],
            MainResponseFields = ["exmn_ymd", "se_cd", "item_cd", "vrty_cd", "grd_cd", "exmn_dd_cnvs_prc"],
            UsageNotes =
            [
                "HS 코드와 aT 품목코드는 직접 호환되지 않으므로 검토된 교차 연결표와 매칭 품질을 함께 반환합니다.",
                "국산 품종을 구분할 수 없는 품목은 국내시장 조사값으로 표시하고 국산 확정 가격으로 표현하지 않습니다."
            ]
        }
    ];

    public PublicDataApiMetadataResponse GetCatalog(PublicDataApiMetadataQuery query)
    {
        IEnumerable<PublicDataApiMetadataItem> items = Catalog;

        if (!string.IsNullOrWhiteSpace(query.Domain))
        {
            items = items.Where(item => string.Equals(item.Domain, query.Domain, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.VersionScope))
        {
            items = items.Where(item => string.Equals(item.VersionScope, query.VersionScope, StringComparison.OrdinalIgnoreCase));
        }

        if (query.ContainsResidentialData.HasValue)
        {
            items = items.Where(item => item.ContainsResidentialData == query.ContainsResidentialData.Value);
        }

        return new PublicDataApiMetadataResponse
        {
            Items = items
                .OrderBy(item => item.VersionScope, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }
}
