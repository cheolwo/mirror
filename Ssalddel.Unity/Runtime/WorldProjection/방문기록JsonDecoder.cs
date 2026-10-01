using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Data.WorldProjection
{
    /// <summary>
    /// 기존 camelCase 방문 v2 JSON을 명시적으로 읽습니다. 위치 근거의 승인이나 저장·전송은 수행하지 않습니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E3,
        "방문 v2 JSON의 문법·입력 상한·필수 값·비권위 경계를 검사하고 별도 사본으로 변환한다.",
        Boundary = "좌표·지역 근거 승인, DB/API 연결, 운영 완료 또는 Unity 실제 화면 검증이 아니다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3계약회귀)]
    public sealed class 방문기록JsonDecoder
    {
        public const int MaxInputUtf8Bytes = 8 * 1024 * 1024;
        public const int MaxDepth = 32;
        public const int MaxVisits = 10000;
        private const int 최대값개수 = 400000;

        /// <summary>
        /// 필수 값의 누락과 null을 기본 좌표·권위 값으로 바꾸지 않습니다.
        /// 실패 메시지와 내부 예외에 원본 JSON·상호·주소를 보관하지 않습니다.
        /// </summary>
        public RegionalPickupRecord Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw 거부("VisitRecordJsonRequired");
            if (json.Length > MaxInputUtf8Bytes) throw 거부("VisitRecordJsonTooLarge");
            var 인코딩 = new UTF8Encoding(false, true);
            int 바이트수;
            try { 바이트수 = 인코딩.GetByteCount(json); }
            catch (EncoderFallbackException) { throw 거부("VisitRecordJsonMalformed"); }
            if (바이트수 > MaxInputUtf8Bytes) throw 거부("VisitRecordJsonTooLarge");

            // 표준 reader가 허용하는 trailing comma·연속 문서·NaN 등을 먼저 거절합니다.
            // 문자열 해제와 JSON 타입 변환은 아래 표준 라이브러리가 담당합니다.
            var 위치 = 0;
            var 값개수 = 0;
            문법검사(json, ref 위치, 0, ref 값개수);
            공백건너뛰기(json, ref 위치);
            if (위치 != json.Length) throw 거부("VisitRecordJsonMalformed");

            XElement 문서;
            try
            {
                var 상한 = new XmlDictionaryReaderQuotas
                {
                    MaxDepth = MaxDepth + 1,
                    MaxArrayLength = 최대값개수,
                    MaxStringContentLength = MaxInputUtf8Bytes,
                    MaxNameTableCharCount = MaxInputUtf8Bytes,
                    MaxBytesPerRead = 4096
                };
                using var reader = JsonReaderWriterFactory.CreateJsonReader(인코딩.GetBytes(json), 상한);
                문서 = XDocument.Load(reader).Root!;
            }
            catch (Exception 오류) when (오류 is XmlException || 오류 is SerializationException ||
                오류 is ArgumentException || 오류 is InvalidOperationException || 오류 is FormatException)
            {
                // 표준 파서의 메시지는 입력 일부를 포함할 수 있으므로 inner exception도 전달하지 않습니다.
                throw 거부("VisitRecordJsonMalformed");
            }

            foreach (var 항목 in 문서.DescendantsAndSelf())
            {
                // __type은 표준 reader의 다형성 메타데이터이며 방문 v2의 필드가 아닙니다.
                if (항목.Attribute("__type") != null) throw 거부("VisitRecordJsonMetadataRejected");
                if (종류(항목) != "object") continue;
                var 이름들 = new HashSet<string>(StringComparer.Ordinal);
                foreach (var 필드 in 항목.Elements())
                    if (!이름들.Add(필드이름(필드))) throw 거부("VisitRecordDuplicateField");
            }

            var 기록 = 객체필드(문서);
            var 스키마 = 필수문자열(기록, "schemaVersion");
            if (스키마 != "delivery-visit-record.v2") throw 거부("VisitRecordSchemaUnsupported");
            var 기록식별자 = 필수문자열(기록, "recordId");
            var 비공개검토 = 필수참거짓(기록, "localReviewOnly");
            var 운영상태 = 필수참거짓(기록, "isOperationalState");
            if (!비공개검토 || 운영상태) throw 거부("VisitRecordAuthorityRejected");

            var 방문배열 = 필수필드(기록, "visits", "array").Elements().ToArray();
            if (방문배열.Length > MaxVisits) throw 거부("VisitRecordVisitLimitExceeded");
            var 방문들 = new RegionalPickupVisit[방문배열.Length];
            var 방문식별자들 = new HashSet<string>(StringComparer.Ordinal);
            for (var 번호 = 0; 번호 < 방문배열.Length; 번호++)
            {
                var 방문 = 객체필드(방문배열[번호]);
                var 방문식별자 = 필수문자열(방문, "visitId");
                if (!방문식별자들.Add(방문식별자)) throw 거부("VisitRecordDuplicateVisitId");
                var 사건종류 = 필수문자열(방문, "eventKind");
                var 완료확정 = 필수참거짓(방문, "pickupCompletionConfirmed");
                if (사건종류 != "RecordedVisit" || 완료확정) throw 거부("VisitRecordAuthorityRejected");
                var 위도 = 필수좌표(방문, "latitude", -90, 90);
                var 경도 = 필수좌표(방문, "longitude", -180, 180);
                방문들[번호] = new RegionalPickupVisit
                {
                    VisitId = 방문식별자,
                    StoreId = 필수문자열(방문, "storeId"),
                    Sequence = 필수정수(방문, "sequence"),
                    Latitude = 위도,
                    Longitude = 경도,
                    Name = 선택문자열(방문, "name") ?? string.Empty,
                    Menu = 선택문자열(방문, "menu") ?? string.Empty,
                    Address = 선택문자열(방문, "address") ?? string.Empty,
                    AddressStatus = 선택문자열(방문, "addressStatus") ?? string.Empty,
                    CoordinateStatus = 선택문자열(방문, "coordinateStatus") ?? string.Empty,
                    MenuStatus = 선택문자열(방문, "menuStatus") ?? string.Empty,
                    VerificationNote = 선택문자열(방문, "verificationNote") ?? string.Empty,
                    SourceUrls = 출처목록(방문),
                    RegionHint = 선택문자열(방문, "regionHint") ?? string.Empty,
                    AdministrativeAreaStableId = 선택문자열(방문, "administrativeAreaStableId", true),
                    StationModuleStableId = 선택문자열(방문, "stationModuleStableId", true),
                    AreaBindingStatus = 선택문자열(방문, "areaBindingStatus") ?? "Unresolved",
                    AreaBindingEvidence = 선택문자열(방문, "areaBindingEvidence", true),
                    RecordedAt = 선택문자열(방문, "recordedAt", true),
                    EventKind = 사건종류,
                    PickupCompletionConfirmed = 완료확정
                };
            }

            return new RegionalPickupRecord
            {
                SchemaVersion = 스키마,
                RecordId = 기록식별자,
                LocalReviewOnly = 비공개검토,
                IsOperationalState = 운영상태,
                Visits = 방문들
            };
        }

        private static ArgumentException 거부(string code) => new ArgumentException(code, "json");
        private static string? 종류(XElement 항목) => (string?)항목.Attribute("type");
        private static string 필드이름(XElement 항목) => (string?)항목.Attribute("item") ?? 항목.Name.LocalName;

        private static Dictionary<string, XElement> 객체필드(XElement 항목)
        {
            if (종류(항목) != "object") throw 거부("VisitRecordFieldTypeInvalid");
            return 항목.Elements().ToDictionary(필드이름, item => item, StringComparer.Ordinal);
        }

        private static XElement 필수필드(Dictionary<string, XElement> 필드들, string 이름, string 타입)
        {
            if (!필드들.TryGetValue(이름, out var 필드)) throw 거부("VisitRecordFieldRequired");
            if (종류(필드) != 타입) throw 거부("VisitRecordFieldTypeInvalid");
            return 필드;
        }

        private static string 필수문자열(Dictionary<string, XElement> 필드들, string 이름)
        {
            var 값 = 필수필드(필드들, 이름, "string").Value;
            if (string.IsNullOrWhiteSpace(값)) throw 거부("VisitRecordFieldRequired");
            return 값;
        }

        private static string? 선택문자열(Dictionary<string, XElement> 필드들, string 이름, bool null허용 = false)
        {
            if (!필드들.TryGetValue(이름, out var 필드)) return null;
            if (null허용 && 종류(필드) == "null") return null;
            if (종류(필드) != "string") throw 거부("VisitRecordFieldTypeInvalid");
            return 필드.Value;
        }

        private static bool 필수참거짓(Dictionary<string, XElement> 필드들, string 이름) =>
            필수필드(필드들, 이름, "boolean").Value == "true";

        private static int 필수정수(Dictionary<string, XElement> 필드들, string 이름)
        {
            var 원문 = 필수필드(필드들, 이름, "number").Value;
            if (!int.TryParse(원문, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var 값))
                throw 거부("VisitRecordFieldTypeInvalid");
            // 순번의 중복·공백·정렬은 재생 준비의 책임으로 남깁니다.
            return 값;
        }

        private static double 필수좌표(Dictionary<string, XElement> 필드들, string 이름, double 최소, double 최대)
        {
            var 원문 = 필수필드(필드들, 이름, "number").Value;
            if (!double.TryParse(원문, NumberStyles.Float, CultureInfo.InvariantCulture, out var 값) ||
                double.IsNaN(값) || double.IsInfinity(값) || 값 < 최소 || 값 > 최대)
                throw 거부("VisitRecordCoordinateInvalid");
            return 값;
        }

        private static string[] 출처목록(Dictionary<string, XElement> 필드들)
        {
            if (!필드들.TryGetValue("sourceUrls", out var 필드)) return Array.Empty<string>();
            if (종류(필드) != "array") throw 거부("VisitRecordFieldTypeInvalid");
            return 필드.Elements().Select(항목 => 종류(항목) == "string" ? 항목.Value :
                throw 거부("VisitRecordFieldTypeInvalid")).ToArray();
        }

        private static void 문법검사(string json, ref int 위치, int 깊이, ref int 값개수)
        {
            공백건너뛰기(json, ref 위치);
            if (++값개수 > 최대값개수) throw 거부("VisitRecordJsonNodeBudgetExceeded");
            if (위치 >= json.Length) throw 거부("VisitRecordJsonMalformed");
            var 문자 = json[위치];
            if (문자 == '{' || 문자 == '[')
            {
                if (깊이 >= MaxDepth) throw 거부("VisitRecordJsonDepthExceeded");
                위치++;
                var 객체 = 문자 == '{';
                var 닫기 = 객체 ? '}' : ']';
                공백건너뛰기(json, ref 위치);
                if (소비(json, ref 위치, 닫기)) return;
                while (true)
                {
                    if (객체)
                    {
                        문자열검사(json, ref 위치);
                        공백건너뛰기(json, ref 위치);
                        if (!소비(json, ref 위치, ':')) throw 거부("VisitRecordJsonMalformed");
                    }
                    문법검사(json, ref 위치, 깊이 + 1, ref 값개수);
                    공백건너뛰기(json, ref 위치);
                    if (소비(json, ref 위치, 닫기)) return;
                    if (!소비(json, ref 위치, ',')) throw 거부("VisitRecordJsonMalformed");
                    공백건너뛰기(json, ref 위치);
                    // 쉼표 뒤에는 반드시 새 값/필드가 와야 하므로 닫기를 여기서 허용하지 않습니다.
                }
            }
            if (문자 == '"') { 문자열검사(json, ref 위치); return; }
            if (문자 == 't') { 상수검사(json, ref 위치, "true"); return; }
            if (문자 == 'f') { 상수검사(json, ref 위치, "false"); return; }
            if (문자 == 'n') { 상수검사(json, ref 위치, "null"); return; }
            숫자검사(json, ref 위치);
        }

        private static void 문자열검사(string json, ref int 위치)
        {
            if (!소비(json, ref 위치, '"')) throw 거부("VisitRecordJsonMalformed");
            while (위치 < json.Length)
            {
                var 문자 = json[위치++];
                if (문자 == '"') return;
                if (문자 < 0x20) throw 거부("VisitRecordJsonMalformed");
                if (문자 != '\\') continue;
                if (위치 >= json.Length) throw 거부("VisitRecordJsonMalformed");
                문자 = json[위치++];
                if (문자 == 'u')
                {
                    for (var 자리 = 0; 자리 < 4; 자리++)
                    {
                        if (위치 >= json.Length || !Uri.IsHexDigit(json[위치++]))
                            throw 거부("VisitRecordJsonMalformed");
                    }
                }
                else if (문자 != '"' && 문자 != '\\' && 문자 != '/' && 문자 != 'b' && 문자 != 'f' &&
                    문자 != 'n' && 문자 != 'r' && 문자 != 't') throw 거부("VisitRecordJsonMalformed");
            }
            throw 거부("VisitRecordJsonMalformed");
        }

        private static void 숫자검사(string json, ref int 위치)
        {
            소비(json, ref 위치, '-');
            if (!소비(json, ref 위치, '0'))
            {
                if (위치 >= json.Length || json[위치] < '1' || json[위치] > '9')
                    throw 거부("VisitRecordJsonMalformed");
                while (위치 < json.Length && 숫자(json[위치])) 위치++;
            }
            if (소비(json, ref 위치, '.')) 숫자묶음검사(json, ref 위치);
            if (소비(json, ref 위치, 'e') || 소비(json, ref 위치, 'E'))
            {
                if (!소비(json, ref 위치, '+')) 소비(json, ref 위치, '-');
                숫자묶음검사(json, ref 위치);
            }
        }

        private static void 숫자묶음검사(string json, ref int 위치)
        {
            if (위치 >= json.Length || !숫자(json[위치])) throw 거부("VisitRecordJsonMalformed");
            while (위치 < json.Length && 숫자(json[위치])) 위치++;
        }

        private static bool 숫자(char 문자) => 문자 >= '0' && 문자 <= '9';
        private static bool 소비(string json, ref int 위치, char 문자)
        {
            if (위치 >= json.Length || json[위치] != 문자) return false;
            위치++;
            return true;
        }

        private static void 상수검사(string json, ref int 위치, string 상수)
        {
            if (json.Length - 위치 < 상수.Length ||
                string.CompareOrdinal(json, 위치, 상수, 0, 상수.Length) != 0)
                throw 거부("VisitRecordJsonMalformed");
            위치 += 상수.Length;
        }

        private static void 공백건너뛰기(string json, ref int 위치)
        {
            while (위치 < json.Length &&
                (json[위치] == ' ' || json[위치] == '\t' || json[위치] == '\n' || json[위치] == '\r')) 위치++;
        }
    }
}
