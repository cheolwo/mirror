using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Domain.Notifications;
using Ssalddel.Services.OperationsAlerts;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using Xunit;

namespace Ssalddel.Tests.Services.OperationsAlerts;

public sealed class OperationsAlertHandlingR21Tests
{
    private static readonly DateTime Now = new(2080, 1, 2, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(25)]
    [InlineData(73)]
    public void Service_goal_does_not_invent_a_deadline_or_overdue_result_from_elapsed_alert_time(int elapsedHours)
    {
        var row = Alert("dispute", "Urgent", "Open", Now.AddHours(-elapsedHours));
        var handling = OperationsAlertHandlingPolicy.Describe(row, Now);

        Assert.Equal("WorkingHoursNotConfigured", handling.ServiceClockCode);
        Assert.Equal("응답 6시간 · 처리 24시간 · 추가 처리 누적 72시간", handling.ServiceTargetSummary);
        Assert.Null(handling.NextOperatorReviewAtUtc);
        Assert.Equal("OpenUnresolved", handling.StateCode);
        Assert.Equal("Open", row.Status);
        Assert.Null(row.AcknowledgedAtUtc);
    }

    [Theory]
    [InlineData("PrivacyIncident")]
    [InlineData("SupportDue")]
    [InlineData("CargoIncidentReviewRequired")]
    [InlineData("unknown private input")]
    public void Other_business_clocks_are_not_replaced_by_the_transaction_dispute_service_goal(string code)
    {
        var row = Alert("other", "Urgent", "Open", Now.AddHours(-73));
        row.ProblemCode = code;
        var handling = OperationsAlertHandlingPolicy.Describe(row, Now);

        Assert.Equal("NotApplicable", handling.ServiceClockCode);
        Assert.Null(handling.ServiceTargetSummary);
        Assert.Null(handling.NextOperatorReviewAtUtc);
        Assert.DoesNotContain("private", handling.NextAction);
    }

    [Theory]
    [InlineData("Open", "OpenUnresolved")]
    [InlineData("Acknowledged", "AcknowledgedUnresolved")]
    [InlineData("Resolved", "Resolved")]
    [InlineData("unknown-status", "SourceRevalidationRequired")]
    public void Acknowledgement_is_not_a_customer_response_or_business_resolution(string status, string stateCode)
    {
        var row = Alert("state", "Urgent", status, Now.AddHours(-1));
        row.AcknowledgedBy = status == "Acknowledged" ? "operator-private-id" : null;
        var handling = OperationsAlertHandlingPolicy.Describe(row, Now);

        Assert.Equal(stateCode, handling.StateCode);
        Assert.Equal("운영 담당자", handling.ResponsibleParty);
        Assert.DoesNotContain("operator-private-id", handling.ResponsibleParty);
        Assert.Equal(status, row.Status);
        Assert.Null(handling.NextOperatorReviewAtUtc);
        if (status == "Resolved") Assert.Null(handling.NextRevalidationAtUtc);
    }

    [Theory]
    [InlineData(-121, false)]
    [InlineData(-120, true)]
    [InlineData(1, false)]
    public void Source_freshness_rejects_stale_and_future_observations_without_creating_a_processing_time(int seconds, bool current)
    {
        var row = Alert("freshness", "Urgent", "Acknowledged", Now.AddHours(-1));
        row.LastVerifiedAtUtc = DateTime.SpecifyKind(Now.AddSeconds(seconds), DateTimeKind.Unspecified);
        var handling = OperationsAlertHandlingPolicy.Describe(row, Now);

        Assert.Equal(current, OperationsAlertHandlingPolicy.IsSourceCurrent(row, Now));
        Assert.Equal(current ? "AcknowledgedUnresolved" : "SourceRevalidationRequired", handling.StateCode);
        Assert.Null(handling.NextOperatorReviewAtUtc);
        if (seconds > 0) Assert.Null(handling.NextRevalidationAtUtc);
        else Assert.Equal(Now.AddSeconds(seconds).AddMinutes(2), handling.NextRevalidationAtUtc);
    }

    [Fact]
    public void Revalidation_is_a_separate_utc_timestamp_and_missing_observation_does_not_invent_one()
    {
        var row = Alert("revalidation", "Warning", "Open", Now.AddHours(-1));
        row.LastVerifiedAtUtc = Now.AddSeconds(-30);
        var handling = OperationsAlertHandlingPolicy.Describe(row, Now);

        Assert.Equal(Now.AddSeconds(90), handling.NextRevalidationAtUtc);
        Assert.Equal(DateTimeKind.Utc, handling.NextRevalidationAtUtc!.Value.Kind);
        Assert.Null(handling.NextOperatorReviewAtUtc);
        row.LastVerifiedAtUtc = default;
        handling = OperationsAlertHandlingPolicy.Describe(row, Now);
        Assert.Null(handling.NextRevalidationAtUtc);
        Assert.Equal("SourceRevalidationRequired", handling.StateCode);
    }

    [Fact]
    public void Legacy_response_and_positional_constructor_do_not_require_handling_data()
    {
        const string legacy = """
            {"id":"legacy","episode":1,"revision":2,"severity":"Warning","status":"Open",
             "title":"확인","action":"업무 확인","sourceHref":"/operations",
             "createdAtUtc":"2080-01-02T09:00:00Z","lastVerifiedAtUtc":"2080-01-02T09:00:00Z",
             "sourceCurrent":true,"deliveryStates":[]}
            """;
        var dto = JsonSerializer.Deserialize<OperationsAlertDto>(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(dto);
        Assert.Null(dto.Handling);
        Assert.Equal("legacy", dto.Id);
        Assert.Null(new OperationsAlertDto("old", 1, 1, "Warning", "Open", "확인", "업무 확인", "/operations", Now, Now, true, []).Handling);
    }

    [Fact]
    public async Task Unresolved_queue_includes_acknowledged_and_orders_globally_before_pagination()
    {
        await using var fixture = await Fixture.Create();
        var warnings = Enumerable.Range(0, 24).Select(i => Alert($"w-{i:00}", "Warning", "Open", Now.AddHours(-24 + i)));
        fixture.Db.AddRange(warnings);
        fixture.Db.AddRange(Alert("ack-urgent", "Urgent", "Acknowledged", Now.AddMinutes(-30)),
            Alert("open-urgent", "Urgent", "Open", Now.AddMinutes(-15)),
            Alert("resolved", "Urgent", "Resolved", Now.AddDays(-10)));
        await fixture.Db.SaveChangesAsync();

        var first = await fixture.Query.ListAsync("Unresolved", 1);
        Assert.Equal(20, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal(new[] { "ack-urgent", "open-urgent", "w-00" }, first.Items.Take(3).Select(x => x.Id));
        Assert.Equal("AcknowledgedUnresolved", first.Items[0].Handling!.StateCode);
        var second = await fixture.Query.ListAsync("Unresolved", 2);
        Assert.Equal(Enumerable.Range(18, 6).Select(i => $"w-{i:00}"), second.Items.Select(x => x.Id));
        Assert.False(second.HasMore);
        Assert.DoesNotContain(first.Items.Concat(second.Items), x => x.Status == "Resolved");

        var unfilteredSecond = await fixture.Query.ListAsync(null, 2);
        Assert.Equal("resolved", unfilteredSecond.Items.Last().Id);
        var legacyOpen = await fixture.Query.ListAsync("Open", 1);
        Assert.All(legacyOpen.Items, x => Assert.Equal("Open", x.Status));
        Assert.Equal("open-urgent", legacyOpen.Items[0].Id);
    }

    [Fact]
    public async Task Read_query_preserves_flags_revision_acknowledgement_and_delivery_records()
    {
        await using var fixture = await Fixture.Create();
        var row = Alert("read-only", "Urgent", "Acknowledged", Now.AddHours(-7));
        row.Revision = 7;
        row.AcknowledgedAtUtc = Now.AddHours(-6);
        fixture.Db.Add(row);
        fixture.Db.Add(new OperationsAlertOutbox { Id = "outbox", AlertId = row.Id, Episode = row.Episode,
            Channel = "Email", Status = "ConfigurationRequired", CreatedAtUtc = Now, NextAttemptUtc = Now });
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var page = await fixture.Query.ListAsync("Unresolved", 1);
        var detail = await fixture.Query.DetailAsync(row.Id);
        Assert.False(page.Enabled);
        Assert.False(page.ExternalSendEnabled);
        Assert.Equal("Acknowledged", detail!.Status);
        Assert.Equal(new[] { "Email:ConfigurationRequired" }, detail.DeliveryStates);
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
        Assert.Equal(7, (await fixture.Db.Set<OperationsAlert>().AsNoTracking().SingleAsync()).Revision);
        Assert.Equal(Now.AddHours(-6), (await fixture.Db.Set<OperationsAlert>().AsNoTracking().SingleAsync()).AcknowledgedAtUtc);
        Assert.Equal(1, await fixture.Db.Set<OperationsAlertOutbox>().CountAsync());
        Assert.Equal("/privacy-support/source-read-only", detail.SourceHref);
        Assert.Equal("WorkingHoursNotConfigured", detail.Handling!.ServiceClockCode);
    }

    [Fact]
    public async Task Existing_coordinator_constructor_keeps_acknowledgement_and_query_separate()
    {
        await using var fixture = await Fixture.Create();
        var row = Alert("compatible", "Urgent", "Open", Now.AddHours(-1));
        fixture.Db.Add(row);
        await fixture.Db.SaveChangesAsync();
        var coordinator = new OperationsAlertCoordinator(fixture.Db, fixture.Options, new Clock());

        await coordinator.AcknowledgeAsync(row.Id, "operator", new(Guid.NewGuid(), row.Revision));
        var page = await coordinator.ListAsync("Unresolved", 1);
        var dto = Assert.Single(page.Items);
        Assert.Equal("Acknowledged", dto.Status);
        Assert.Equal("AcknowledgedUnresolved", dto.Handling!.StateCode);
        Assert.Null(dto.Handling.NextOperatorReviewAtUtc);
        Assert.Empty(await fixture.Db.Set<OperationsAlertOutbox>().ToListAsync());
    }

    private static OperationsAlert Alert(string id, string severity, string status, DateTime created)
        => new() { Id = id, SourceKind = "support", SourceId = "source-" + id, ProblemCode = "TransactionDispute",
            Severity = severity, Status = status, CreatedAtUtc = created, LastVerifiedAtUtc = Now };

    private sealed class Fixture : IAsyncDisposable
    {
        public required SqliteConnection Connection;
        public required SsalddelContext Db;
        public OptionsMonitor Options { get; } = new();
        public OperationsAlertQueryService Query => new(Db, Options, new Clock());

        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            return new() { Connection = connection, Db = db };
        }

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class OptionsMonitor : IOptionsMonitor<OperationsAlertsOptions>
    {
        public OperationsAlertsOptions CurrentValue { get; } = new();
        public OperationsAlertsOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OperationsAlertsOptions, string?> listener) => null;
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }
}
