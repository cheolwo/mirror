using System.Text.Json;
using DriverApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class DriverCargoAlertPublicationHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SuccessfulReservationSurvivesRestartAndAllowsANewRoundOrRequest()
    {
        var store = new Storage(); var history = new DriverCargoAlertPublicationHistory(store);
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history));
        Assert.Equal(DriverCargoAlertReservationResult.Duplicate, Reserve(new(store)));
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history, round: 2));
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history, request: "request-2"));
        Assert.Equal(3, store.Writes);
    }

    [Fact]
    public void OwnerRoundTripKeepsBothHistoriesWithoutPersistingRawOwnerIds()
    {
        var store = new Storage(); var history = new DriverCargoAlertPublicationHistory(store);
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history, owner: "private-owner-a"));
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history, owner: "private-owner-b"));
        Assert.Equal(DriverCargoAlertReservationResult.Duplicate, Reserve(new(store), owner: "private-owner-a"));
        Assert.Equal(DriverCargoAlertReservationResult.Duplicate, Reserve(new(store), owner: "private-owner-b"));
        Assert.DoesNotContain("private-owner", store.Value!);
        using var document = JsonDocument.Parse(store.Value!);
        Assert.Equal(2, document.RootElement.GetProperty("owners").EnumerateObject().Count());
    }

    [Fact]
    public void LegacyOwnerSnapshotIsPreservedWhenAnotherOwnerCreatesTheNewFormat()
    {
        var store = new Storage { Value = JsonSerializer.Serialize(new
        {
            ownerId = "owner-a",
            alerts = new[] { new { requestId = "request-1", round = 1, expiresAtUtc = Now.AddMinutes(2) } }
        }) };
        var history = new DriverCargoAlertPublicationHistory(store);
        Assert.Equal(DriverCargoAlertReservationResult.Duplicate, Reserve(history, owner: "owner-a"));
        Assert.Equal(0, store.Writes);
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history, owner: "owner-b"));
        Assert.Equal(DriverCargoAlertReservationResult.Duplicate, Reserve(new(store), owner: "owner-a"));
        Assert.Contains("\"version\":2", store.Value!);
        Assert.DoesNotContain("owner-a", store.Value!);
    }

    [Fact]
    public void ReadOrWriteFailureDoesNotGrantPermissionToPostAndCanRetryAnUncommittedReservation()
    {
        var store = new Storage { FailRead = true }; var history = new DriverCargoAlertPublicationHistory(store);
        Assert.Equal(DriverCargoAlertReservationResult.Unavailable, Reserve(history)); Assert.Equal(0, store.Writes);
        store.FailRead = false; store.FailWriteBeforeSave = true;
        Assert.Equal(DriverCargoAlertReservationResult.Unavailable, Reserve(history)); Assert.Null(store.Value);
        store.FailWriteBeforeSave = false;
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history));
    }

    [Fact]
    public void CommittedWriteThenFailureRemainsConsumedInsteadOfReplayingAfterRestart()
    {
        var store = new Storage { FailWriteAfterSave = true };
        Assert.Equal(DriverCargoAlertReservationResult.Unavailable, Reserve(new(store)));
        store.FailWriteAfterSave = false;
        Assert.Equal(DriverCargoAlertReservationResult.Duplicate, Reserve(new(store)));
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void UnconfirmedReadBackDoesNotPermitPostingAndPreservesTheCommittedReservation()
    {
        var store = new Storage { FailReadAfterWrite = true };
        Assert.Equal(DriverCargoAlertReservationResult.Unavailable, Reserve(new(store)));
        store.FailReadAfterWrite = false;
        Assert.Equal(DriverCargoAlertReservationResult.Duplicate, Reserve(new(store)));
    }

    [Fact]
    public void MismatchedReadBackCannotBeTreatedAsSuccessfulStorage()
    {
        var store = new Storage { IgnoreWrites = true };
        Assert.Equal(DriverCargoAlertReservationResult.Unavailable, Reserve(new(store)));
        Assert.Null(store.Value);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"version\":3,\"owners\":{}}")]
    [InlineData("{\"version\":2,\"owners\":null}")]
    [InlineData("{\"ownerId\":\"owner-a\",\"alerts\":null}")]
    [InlineData("{\"version\":2,\"owners\":{\"unhashed-owner\":[]}}")]
    public void CorruptOrUnknownHistoryIsPreservedAndCannotSilentlyResetDeduplication(string value)
    {
        var store = new Storage { Value = value };
        Assert.Equal(DriverCargoAlertReservationResult.Unavailable, Reserve(new(store)));
        Assert.Equal(value, store.Value); Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void ExpiredOwnersArePrunedButMoreThanOneHundredLiveReservationsRemainProtected()
    {
        var store = new Storage(); var history = new DriverCargoAlertPublicationHistory(store);
        for (var index = 0; index < 110; index++)
            Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history, request: "request-" + index));
        Assert.Equal(DriverCargoAlertReservationResult.Duplicate, Reserve(new(store), request: "request-0"));
        Assert.Equal(DriverCargoAlertReservationResult.Reserved,
            history.TryReserve("owner-b", "later", 1, Now.AddMinutes(4), Now.AddMinutes(3)));
        using var document = JsonDocument.Parse(store.Value!);
        Assert.Single(document.RootElement.GetProperty("owners").EnumerateObject());
    }

    [Fact]
    public async Task ConcurrentPublishersReserveTheSameRecommendationOnlyOnce()
    {
        var store = new Storage();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => Reserve(new(store)))));
        Assert.Single(outcomes, x => x == DriverCargoAlertReservationResult.Reserved);
        Assert.Equal(9, outcomes.Count(x => x == DriverCargoAlertReservationResult.Duplicate));
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void InvalidOrExpiredRecommendationCannotReserveAndNewInstallationHasNoPreviousGuarantee()
    {
        var store = new Storage(); var history = new DriverCargoAlertPublicationHistory(store);
        Assert.Equal(DriverCargoAlertReservationResult.Unavailable, history.TryReserve("owner", "request", 1, Now, Now));
        Assert.Equal(DriverCargoAlertReservationResult.Unavailable, Reserve(history, round: 0));
        Assert.Equal(0, store.Writes);
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(history));
        Assert.Equal(DriverCargoAlertReservationResult.Reserved, Reserve(new(new Storage())));
    }

    private static DriverCargoAlertReservationResult Reserve(DriverCargoAlertPublicationHistory history,
        string owner = "owner-a", string request = "request-1", int round = 1)
        => history.TryReserve(owner, request, round, Now.AddMinutes(2), Now);

    private sealed class Storage : IDriverCargoAlertPublicationStorage
    {
        public string? Value; public int Writes;
        public bool FailRead, FailWriteBeforeSave, FailWriteAfterSave, FailReadAfterWrite, IgnoreWrites;
        public string? Read()
        {
            if (FailRead || (FailReadAfterWrite && Writes > 0)) throw new IOException("fixture read unavailable");
            return Value;
        }
        public void Write(string value)
        {
            Writes++;
            if (FailWriteBeforeSave) throw new IOException("fixture write unavailable");
            if (!IgnoreWrites) Value = value;
            if (FailWriteAfterSave) throw new IOException("fixture committed but unconfirmed");
        }
    }
}
