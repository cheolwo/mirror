using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.PrivacyRetention;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Services.PrivacyRetention;

public interface I배송원장개인정보파기Service
{
    Task<bool> 파기Async(string ledgerId, string sourceCode, string recordId, CancellationToken ct);
}

public sealed class 배송원장개인정보파기Service : I배송원장개인정보파기Service
{
    private readonly IMongoCollection<커뮤니티원장문서> _collection;
    public 배송원장개인정보파기Service(IMongoClient client, IOptions<MongoDbOptions> options)
        => _collection = 커뮤니티원장MongoCollectionFactory.Create(client, options);

    public async Task<bool> 파기Async(string ledgerId, string sourceCode, string recordId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ledgerId)) return false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var doc = await _collection.Find(x => x.원장Id == ledgerId).FirstOrDefaultAsync(ct);
            // Absence is not proof that a previously claimed writer cannot still insert this source.
            // Keep the job blocked until the exact ledger can receive a durable CAS deletion marker.
            if (doc is null) return false;
            if (!원천일치(doc, sourceCode, recordId)) return false;
            if (doc.투영상태 == 커뮤니티원장투영상태.처리중 || doc.투영처리Token is not null) return false;
            var revision = doc.Revision;
            Scrub(doc, sourceCode);
            doc.확장속성[Marker] = sourceCode + ":" + recordId;
            doc.Revision++;
            doc.투영완료Revision = doc.Revision;
            doc.투영상태 = 커뮤니티원장투영상태.완료;
            doc.투영처리Token = null; doc.투영다음시도시각Utc = null; doc.투영마지막오류 = null;
            doc.수정시각Utc = DateTime.UtcNow;
            var result = await _collection.ReplaceOneAsync(x => x.원장Id == ledgerId && x.Revision == revision
                && x.투영상태 != 커뮤니티원장투영상태.처리중 && x.투영처리Token == null, doc, cancellationToken: ct);
            if (result.MatchedCount == 1) return true;
        }
        return false;
    }

    public const string Marker = "privacyOperationalCopyPurged";
    internal static bool 원천일치(커뮤니티원장문서 doc, string sourceCode, string recordId)
    {
        var refs = doc.외부참조;
        return sourceCode switch
        {
            개인정보파기원천Codes.FoodOrder => refs.Any(x => x.Key is "음식주문번호" or "주문번호" or "원천Id" && x.Value == recordId)
                && doc.원장템플릿Key is "food-order" or "food-delivery",
            개인정보파기원천Codes.NeighborhoodDelivery => refs.GetValueOrDefault("화주운송의뢰Id") == recordId
                && doc.원장Id == "transport:" + recordId && doc.원장템플릿Key == "cargo-transport",
            _ => false
        };
    }

    internal static void Scrub(커뮤니티원장문서 doc, string sourceCode)
    {
        foreach (var block in doc.블록목록)
            ScrubDictionary(block.Data, sourceCode == 개인정보파기원천Codes.FoodOrder && block.BlockId == "restaurant");
        ScrubDictionary(doc.확장속성, false);
        // Only these exact business-ledger copies are targeted. State/amount/menu facts survive.
        doc.원함 = null;
        foreach (var p in doc.참여자목록)
            if (p.RoleLabel is "요청자" or "주문자" or "상차 확인자" or "수령 확인자") p.DisplayName = p.RoleLabel;
        foreach (var h in doc.상태이력) h.메모 = null;
        doc.투영변경메모 = null;
        if (doc.다이어그램스냅샷 is { } diagram)
        {
            foreach (var n in diagram.Nodes) { ScrubDictionary(n.Data, false); n.Description = null; }
            foreach (var e in diagram.Edges) ScrubDictionary(e.Data, false);
            ScrubDictionary(diagram.Metadata, false);
        }
    }

    private static readonly HashSet<string> PrivateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "주소", "상세주소", "위도", "경도", "연락처이름", "연락처전화번호", "수령인명", "수령인연락처", "수령지주소", "수령지상세주소",
        "수령요청사항", "요청사항", "수락메모", "메모", "정산메모", "현장지급메모", "화물설명", "첨부_json", "연락처", "전화번호",
        "Address", "DetailedAddress", "Latitude", "Longitude", "Phone", "ContactPhone", "CustomerName", "DeliveryInstructions"
    };
    private static void ScrubDictionary(Dictionary<string, string> data, bool preserveBusinessPlace)
    {
        foreach (var key in data.Keys.ToArray())
            if (PrivateKeys.Contains(key) && !(preserveBusinessPlace && (key is "주소" or "상세주소" or "위도" or "경도")))
                data.Remove(key);
    }
}
