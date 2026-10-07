using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
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

    private static Mongo보호지원Store.보호지원Document Write(Mongo보호지원Store store, 보호지원Record value)
        => (Mongo보호지원Store.보호지원Document)typeof(Mongo보호지원Store).GetMethod("Write", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, [value])!;
    private static 보호지원Record Read(Mongo보호지원Store store, Mongo보호지원Store.보호지원Document value)
        => (보호지원Record)typeof(Mongo보호지원Store).GetMethod("Read", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, [value])!;
}
