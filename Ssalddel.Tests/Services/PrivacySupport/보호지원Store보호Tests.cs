using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.PrivacySupport;

public sealed class 보호지원Store보호Tests
{
    [Fact]
    public void Mongo문서는원문개인정보당사자ID없이암호문과조회Hash만가진다()
    {
        var store = new Mongo보호지원Store(new MongoClient("mongodb://127.0.0.1:27017"),
            Options.Create(new MongoDbOptions { Database = "never-connected-unit-test" }), new EphemeralDataProtectionProvider());
        var original = new 보호지원Record { CaseId = "case:1", Kind = "transaction-dispute", Revision = 1,
            OwnerUserId = "private-user-id", PartyUserIds = ["private-user-id"], Summary = "010-1234-5678 사가정로101호" };
        var document = Write(store, original);
        Assert.DoesNotContain(original.Summary, document.ProtectedPayload);
        Assert.DoesNotContain(original.OwnerUserId, document.ProtectedPayload);
        Assert.DoesNotContain(original.OwnerUserId, document.PartyHashes);
        var roundtrip = Read(store, document);
        Assert.Equal(original.Summary, roundtrip.Summary);
        Assert.Equal(original.OwnerUserId, roundtrip.OwnerUserId);
        document.PartyHashes = ["forged-user-hash"];
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Read(store, document)).InnerException);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("revision")]
    [InlineData("kind")]
    public void 암호문과문서ID판본종류결속이달라지면복호화결과를사용하지않는다(string field)
    {
        var store = new Mongo보호지원Store(new MongoClient("mongodb://127.0.0.1:27017"),
            Options.Create(new MongoDbOptions { Database = "never-connected-unit-test" }), new EphemeralDataProtectionProvider());
        var doc = Write(store, new() { CaseId = "case:1", Kind = "transaction-dispute", PartyUserIds = ["owner"], Revision = 2 });
        if (field == "id") doc.CaseId = "forged";
        if (field == "revision") doc.Revision++;
        if (field == "kind") doc.Kind = "privacy-incident";
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Read(store, doc)).InnerException);
    }

    [Fact]
    public void 원천조회Hash는원천쌍에결속되고원문ID를노출하거나다른원천의Hash를재사용하지않는다()
    {
        var store = new Mongo보호지원Store(new MongoClient("mongodb://127.0.0.1:27017"),
            Options.Create(new MongoDbOptions { Database = "never-connected-unit-test" }), new EphemeralDataProtectionProvider());
        var original = new 보호지원Record { CaseId = "case", Kind = 보호지원종류Codes.분쟁,
            SourceKind = 보호지원출처Codes.분산배송, SourceId = "private-order-id", PartyUserIds = ["customer"], Revision = 1 };
        var document = Write(store, original);
        Assert.Equal(64, document.SourceHash!.Length);
        Assert.DoesNotContain(original.SourceId, document.SourceHash);
        Assert.Equal(original.SourceId, Read(store, document).SourceId);
        document.SourceHash = Write(store, new() { CaseId = "other", Kind = 보호지원종류Codes.분쟁,
            SourceKind = 보호지원출처Codes.분산입고, SourceId = original.SourceId, PartyUserIds = ["customer"], Revision = 1 }).SourceHash;
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Read(store, document)).InnerException);
        // Existing rows remain readable, while HasOpenCaseAsync treats a missing index as unknown.
        document.SourceHash = null;
        Assert.Equal(original.SourceId, Read(store, document).SourceId);
    }

    [Theory]
    [InlineData("received", false, false, true)]
    [InlineData("reviewing", false, false, true)]
    [InlineData("reopened", false, false, true)]
    [InlineData("result-prepared", false, false, true)]
    [InlineData("unknown", false, false, true)]
    [InlineData("closed", false, false, false)]
    [InlineData("denied", false, false, false)]
    [InlineData("closed", true, false, true)]
    [InlineData("closed", false, true, true)]
    public void 미종결사건과보존의도는증빙삭제를차단하며명시적종결만해제후보가된다(string status, bool hold, bool pending, bool expected)
    {
        var row = new 보호지원Record { StatusCode = status, SourceHoldActive = hold,
            RetentionIntent = pending ? new(Guid.NewGuid(), "digest", "admin", "hold-record", "basis", DateTime.UtcNow.AddDays(1), DateTime.UtcNow) : null };
        Assert.Equal(expected, (bool)typeof(Mongo보호지원Store).GetMethod("RequiresSourcePreservation", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [row])!);
    }

    private static Mongo보호지원Store.보호지원Document Write(Mongo보호지원Store store, 보호지원Record value)
        => (Mongo보호지원Store.보호지원Document)typeof(Mongo보호지원Store).GetMethod("Write", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, [value])!;
    private static 보호지원Record Read(Mongo보호지원Store store, Mongo보호지원Store.보호지원Document value)
        => (보호지원Record)typeof(Mongo보호지원Store).GetMethod("Read", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, [value])!;
}
