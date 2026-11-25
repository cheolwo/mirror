using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Explanation;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Explanation;

/// <summary>
/// 실제 Recorder/Service와 격리 메모리 DB의 공통 근거 연결 회귀입니다.
/// HTTP 시험은 기존 X-Fixture-Actor 인증을 사용하며 운영 JWT·메일·기기 검증이 아닙니다.
/// </summary>
public sealed partial class CargoDispatchExplanationR24Tests
{
    [Fact]
    public async Task 새추천사건은보호payload에공통판본을결속하고_접수5항목과판정4항목을구분한다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.Recorder.ReserveAsync(f.Queue, Driver, Now, "coordination/private-r25", null,
            decisionFacts: new(true, true, 2, 4));
        Assert.NotNull(row);
        Assert.Equal(EntityState.Added, f.Db.Entry(row!).State);
        Assert.Equal(0, await f.Db.운송이벤트.AsNoTracking().CountAsync());
        using var saved = JsonDocument.Parse(new FakeProtection().Unprotect(row.메타데이터)!);
        Assert.Equal(1, saved.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(JsonValueKind.Null, saved.RootElement.GetProperty("inputEvidence").ValueKind);
        var reference = saved.RootElement.GetProperty("basis");
        Assert.Equal(9, reference.GetProperty("recordedCodes").GetArrayLength());
        Assert.Equal(0, reference.GetProperty("missingCodes").GetArrayLength());
        Assert.DoesNotContain("intake.weight", row.메타데이터, StringComparison.Ordinal);
        await f.Db.SaveChangesAsync();

        var item = Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);
        var basis = Assert.IsType<CargoDispatchBasisEvidence>(item.Basis);
        Assert.Equal("recorded", basis.Status);
        Assert.Equal(reference.GetProperty("version").GetString(), basis.CatalogVersion);
        Assert.Equal(reference.GetProperty("contentHash").GetString(), basis.CatalogContentHash);
        Assert.Matches("^[A-F0-9]{64}$", basis.CatalogContentHash!);
        Assert.Equal(9, basis.RecordedFactCount);
        Assert.Equal(5, basis.IntakeFactCount);
        Assert.Equal(4, basis.DecisionFactCount);
        Assert.Equal(0, basis.MissingFactCount);
        Assert.Equal(9, basis.Entries.Count);
        Assert.Equal(5, basis.Entries.Count(x => x.Kind == "intake"));
        Assert.Equal(4, basis.Entries.Count(x => x.Kind == "decision"));
        Assert.All(basis.Entries, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.Label));
            Assert.False(string.IsNullOrWhiteSpace(x.Explanation));
            Assert.NotNull(x.Value);
        });
        Assert.Equal("confirmed", Assert.Single(basis.Entries, x => x.Code == "decision.plan").State);
        Assert.Equal("confirmed", Assert.Single(basis.Entries, x => x.Code == "decision.loading").State);
        var exposed = JsonSerializer.Serialize(item);
        foreach (var secret in new[] { Owner, Orderer, Driver, "private-r25", "private-address-r24", "private-phone-r24" })
            Assert.DoesNotContain(secret, exposed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 미확인false는누락null로바뀌지않고_누락은검증된사실수에더하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Recorder.ReserveAsync(f.Queue, Driver, Now, "coordination/private-r25", null,
            decisionFacts: new(false, false));
        await f.Db.SaveChangesAsync();

        var basis = Assert.IsType<CargoDispatchBasisEvidence>(Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items).Basis);

        Assert.Equal("partial", basis.Status);
        Assert.Equal(7, basis.RecordedFactCount);
        Assert.Equal(5, basis.IntakeFactCount);
        Assert.Equal(2, basis.DecisionFactCount);
        Assert.Equal(2, basis.MissingFactCount);
        foreach (var code in new[] { "decision.plan", "decision.loading" })
        {
            var entry = Assert.Single(basis.Entries, x => x.Code == code);
            Assert.Equal("not-confirmed", entry.State);
            Assert.NotNull(entry.Value);
        }
        foreach (var code in new[] { "decision.pickup-order", "decision.dropoff-order" })
        {
            var entry = Assert.Single(basis.Entries, x => x.Code == code);
            Assert.Equal("missing", entry.State);
            Assert.Null(entry.Value);
        }
    }

    [Fact]
    public async Task 관측하지않은접수조건과엔진의미기록판정은누락으로남기며_현재정보로채우지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Request.화물중량Kg = null;
        f.Request.운송방식 = "미확인-r25";
        f.Request.화물온도조건 = "미확인-r25";
        f.Request.픽업_시간창_종료일시 = default;
        f.Request.하차_시간창_종료일시 = null;
        await f.Db.SaveChangesAsync();
        var row = await f.RecordAsync();
        var metadata = row.메타데이터;
        f.Request.화물중량Kg = 1m;
        f.Request.운송방식 = "독차";
        f.Request.화물온도조건 = "냉동";
        f.Request.픽업_시간창_종료일시 = Now.AddHours(10);
        f.Request.하차_시간창_종료일시 = Now.AddHours(11);
        await f.Db.SaveChangesAsync();

        var item = Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);
        var basis = Assert.IsType<CargoDispatchBasisEvidence>(item.Basis);

        Assert.Equal("partial", basis.Status);
        Assert.Equal(0, basis.RecordedFactCount);
        Assert.Equal(0, basis.IntakeFactCount);
        Assert.Equal(0, basis.DecisionFactCount);
        Assert.Equal(9, basis.MissingFactCount);
        Assert.All(basis.Entries, x => { Assert.Equal("missing", x.State); Assert.Null(x.Value); });
        Assert.Empty(item.Facts);
        Assert.Equal(metadata, (await f.Db.운송이벤트.AsNoTracking().SingleAsync()).메타데이터);
    }

    [Fact]
    public async Task 공통근거와개인안내는당시사건을읽으며_현재원장변경이나조회로업무와발송을변경하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var evidenceId = row.Id.ToString(CultureInfo.InvariantCulture);
        var before = await f.Service.GetNoticeContextAsync(Owner, f.Request.의뢰Id, evidenceId);
        var metadata = row.메타데이터;
        var state = f.Queue.상태;
        var fee = f.Request.최종운임;
        f.Request.화물중량Kg = 999m;
        f.Request.운송방식 = "독차";
        f.Request.화물온도조건 = "냉동";
        f.Request.픽업_시간창_종료일시 = Now.AddDays(1);
        f.Queue.추천판정근거Json = "{\"current\":\"private-current-recomputed-r25\"}";
        await f.Db.SaveChangesAsync();
        var after = await f.Service.GetNoticeContextAsync(Owner, f.Request.의뢰Id, evidenceId);

        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.Equal("preparation-only", after.Purpose);
        Assert.Equal("cargo-dispatch-notice.r1", after.TemplateVersion);
        Assert.Equal(evidenceId, after.EvidenceId);
        Assert.Equal(f.Request.의뢰Id, after.RequestId);
        Assert.Equal(1, after.RecommendationRound);
        Assert.Equal(Now, after.OccurredAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(after.Subject));
        Assert.NotEmpty(after.Lines);
        Assert.Equal(5, after.Basis.IntakeFactCount);
        Assert.Equal(0, after.Basis.DecisionFactCount);
        Assert.Equal(4, after.Basis.MissingFactCount);
        Assert.DoesNotContain("private-current-recomputed-r25", JsonSerializer.Serialize(after), StringComparison.Ordinal);
        Assert.Equal(metadata, (await f.Db.운송이벤트.AsNoTracking().SingleAsync()).메타데이터);
        Assert.Equal(state, f.Queue.상태);
        Assert.Equal(fee, f.Request.최종운임);
        Assert.Empty(f.Feedback.Items);
        Assert.Equal(0, await f.Db.Command알림Outbox.CountAsync());
        Assert.Equal(0, await f.Db.CommunityPostEmailNotificationOutbox.CountAsync());
    }

    [Theory]
    [InlineData("legacy", "legacy-unlinked")]
    [InlineData("version", "catalog-unavailable")]
    [InlineData("hash", "mismatch")]
    [InlineData("recorded-codes", "mismatch")]
    [InlineData("missing-codes", "mismatch")]
    public async Task 과거미연결과손상된공통참조는보류하되_개인사실과의견을보존하고소급보충하지않는다(string change, string expectedStatus)
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.ReserveAsync();
        Assert.NotNull(row);
        RewriteR25Uncommitted(row!, payload =>
        {
            if (change == "legacy") { payload.Remove("basis"); return; }
            var reference = payload["basis"]!.AsObject();
            switch (change)
            {
                case "version": reference["version"] = "unknown-catalog-r25"; break;
                case "hash": reference["contentHash"] = new string('0', 64); break;
                case "recorded-codes": reference["recordedCodes"] = new JsonArray("intake.weight"); break;
                case "missing-codes": reference["missingCodes"] = new JsonArray("unknown-code-r25"); break;
            }
        });
        await f.Db.SaveChangesAsync();
        var metadata = row!.메타데이터;
        var feedback = await f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, Feedback(row));
        var item = Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);
        var basis = Assert.IsType<CargoDispatchBasisEvidence>(item.Basis);
        var notice = await f.Service.GetNoticeContextAsync(Owner, f.Request.의뢰Id, item.EvidenceId);

        Assert.Equal(expectedStatus, basis.Status);
        Assert.Equal(expectedStatus, notice.Basis.Status);
        Assert.Empty(basis.Entries);
        Assert.Contains(item.Facts, x => x.Label == "당시 접수 중량" && x.Value == "100kg");
        Assert.Equal(feedback.FeedbackId, Assert.Single(item.MyFeedback).FeedbackId);
        Assert.Equal("preparation-only", notice.Purpose);
        Assert.Equal(metadata, (await f.Db.운송이벤트.AsNoTracking().SingleAsync()).메타데이터);
        Assert.Equal(1, await f.Db.운송이벤트.CountAsync());
        Assert.Equal(0, await f.Db.Command알림Outbox.CountAsync());
        Assert.Equal(0, await f.Db.CommunityPostEmailNotificationOutbox.CountAsync());
    }

    [Fact]
    public async Task 개인안내는추천종료후에도본인과거기록만허용하고_현재새기사에게다른기사설명을허용하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var first = await f.RecordAsync();
        var firstId = first.Id.ToString(CultureInfo.InvariantCulture);
        const string nextDriver = "private-next-driver-r25";
        f.Queue.현재추천대상기사Id = nextDriver;
        f.Queue.추천라운드 = 2;
        f.Queue.추천만료시각 = Now.AddMinutes(20);
        await f.Db.SaveChangesAsync();
        var next = await f.Recorder.ReserveAsync(f.Queue, nextDriver, Now.AddMinutes(1), "manual/private-r25", null);
        Assert.NotNull(next);
        await f.Db.SaveChangesAsync();
        f.Queue.배차노출상태 = 상태값.배차노출상태.추천후보없음;
        f.Queue.현재추천대상기사Id = null;
        f.Queue.추천만료시각 = Now.AddMinutes(-1);
        await f.Db.SaveChangesAsync();

        foreach (var actor in new[] { Owner, Orderer, Driver })
        {
            var notice = await f.Service.GetNoticeContextAsync(actor, f.Request.의뢰Id, firstId);
            Assert.Equal(firstId, notice.EvidenceId);
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetNoticeContextAsync(nextDriver, f.Request.의뢰Id, firstId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetNoticeContextAsync(Driver, f.Request.의뢰Id, next!.Id.ToString(CultureInfo.InvariantCulture)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetNoticeContextAsync("unrelated-r25", f.Request.의뢰Id, firstId));
        var own = Assert.Single((await f.Service.GetAsync(Driver, f.Request.의뢰Id)).Items);
        Assert.Equal(firstId, own.EvidenceId);
        Assert.Equal(5, Assert.IsType<CargoDispatchBasisEvidence>(own.Basis).IntakeFactCount);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("unrelated-r25", HttpStatusCode.Forbidden)]
    [InlineData(Owner, HttpStatusCode.OK)]
    [InlineData(Orderer, HttpStatusCode.OK)]
    [InlineData(Driver, HttpStatusCode.OK)]
    public async Task 실제HTTP개인안내는인증된본인권한만사용하고_조회가발송이나추가저장을하지않는다(string? actor, HttpStatusCode expected)
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var evidenceId = row.Id.ToString(CultureInfo.InvariantCulture);
        var metadata = row.메타데이터;
        var state = f.Queue.상태;
        await using var http = await ExplanationHttpHost.StartAsync(f.Service);
        using var request = http.Request(HttpMethod.Get,
            $"api/v1/cargo-dispatch-explanations/{f.Request.의뢰Id}/notice-context/{evidenceId}?userId={Owner}", actor);
        using var response = await http.Client.SendAsync(request, http.Token);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var notice = await response.Content.ReadFromJsonAsync<CargoDispatchNoticeContext>(cancellationToken: http.Token);
            Assert.NotNull(notice);
            Assert.Equal(evidenceId, notice.EvidenceId);
            Assert.Equal("preparation-only", notice.Purpose);
            Assert.Equal("cargo-dispatch-notice.r1", notice.TemplateVersion);
            Assert.Equal("partial", notice.Basis.Status);
            Assert.Equal(5, notice.Basis.IntakeFactCount);
            var exposed = JsonSerializer.Serialize(notice);
            foreach (var secret in new[] { Owner, Orderer, Driver, "raw-private-algorithm-r24", "private-address-r24", "private-phone-r24" })
                Assert.DoesNotContain(secret, exposed, StringComparison.Ordinal);
        }
        Assert.Equal(metadata, (await f.Db.운송이벤트.AsNoTracking().SingleAsync()).메타데이터);
        Assert.Equal(state, f.Queue.상태);
        Assert.Empty(f.Feedback.Items);
        Assert.Equal(0, await f.Db.Command알림Outbox.CountAsync());
        Assert.Equal(0, await f.Db.CommunityPostEmailNotificationOutbox.CountAsync());
    }

    [Fact]
    public async Task 실제HTTP개인안내는잘못된사건ID와없는사건을구분하고_기존의견과공통판본을함께조회한다()
    {
        await using var f = await Fixture.CreateAsync();
        var row = await f.RecordAsync();
        var feedback = await f.Service.SubmitFeedbackAsync(Owner, f.Request.의뢰Id, Feedback(row));
        var id = row.Id.ToString(CultureInfo.InvariantCulture);
        await using var http = await ExplanationHttpHost.StartAsync(f.Service);
        var endpoint = $"api/v1/cargo-dispatch-explanations/{f.Request.의뢰Id}";
        foreach (var (value, expected) in new[] { ("bad", HttpStatusCode.BadRequest), ("0", HttpStatusCode.BadRequest), ("9999999", HttpStatusCode.NotFound) })
        {
            using var request = http.Request(HttpMethod.Get, $"{endpoint}/notice-context/{value}", Owner);
            using var response = await http.Client.SendAsync(request, http.Token);
            Assert.Equal(expected, response.StatusCode);
        }
        using var get = http.Request(HttpMethod.Get, endpoint, Owner);
        using var read = await http.Client.SendAsync(get, http.Token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var explanation = await read.Content.ReadFromJsonAsync<CargoDispatchExplanationResponse>(cancellationToken: http.Token);
        var item = Assert.Single(explanation!.Items);
        Assert.Equal(id, item.EvidenceId);
        Assert.Equal("partial", Assert.IsType<CargoDispatchBasisEvidence>(item.Basis).Status);
        Assert.Equal(feedback.FeedbackId, Assert.Single(item.MyFeedback).FeedbackId);
        Assert.Equal(1, await f.Db.운송이벤트.CountAsync());
        Assert.Single(f.Feedback.Items);
    }

    [Fact]
    public async Task 입력근거는해당의뢰와기사의지문만보호보존하고_호출뒤원사전변경과공개응답으로전파하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        const string otherRequest = "private-other-request-id-r25";
        const string otherDriver = "private-other-driver-id-r25";
        var requestEvidence = new Dictionary<string, string>
        {
            [f.Request.의뢰Id] = "private-request-fingerprint-r25", [otherRequest] = "private-other-request-fingerprint-r25"
        };
        var driverEvidence = new Dictionary<string, string>
        {
            [Driver] = "private-driver-fingerprint-r25", [otherDriver] = "private-other-driver-fingerprint-r25"
        };
        var source = new 국내화물배차판정근거
        {
            입력지문 = "private-input-fingerprint-r25", 기사기본지문 = "private-driver-base-fingerprint-r25",
            실차제원지문 = "private-vehicle-fingerprint-r25", 기사고정조건지문 = "private-driver-fixed-fingerprint-r25",
            현재수행지문 = "private-current-work-fingerprint-r25", 위치관측지문 = "private-location-fingerprint-r25",
            지문판본 = "private-fingerprint-version-r25", 의뢰근거 = requestEvidence, 기사근거 = driverEvidence,
            기준시각Utc = Now, 조회시작시각Utc = Now.AddSeconds(-2), 조회완료시각Utc = Now.AddSeconds(-1),
            위치기록시각Utc = Now.AddMinutes(-2), 위치수신시각Utc = Now.AddMinutes(-1),
            조회범위 = "private-source-query-scope-r25", 원자적조회여부 = true,
            미확인사유 = ["private-raw-unconfirmed-reason-r25"], 차단사유 = ["private-raw-block-reason-r25"],
            대기순위점수 = 98765.432m
        };

        var capture = f.Recorder.ReserveAsync(f.Queue, Driver, Now, "coordination/private-r25", source);
        requestEvidence[f.Request.의뢰Id] = "private-changed-request-r25";
        driverEvidence[Driver] = "private-changed-driver-r25";
        var row = await capture;
        Assert.NotNull(row);
        Assert.Equal(EntityState.Added, f.Db.Entry(row!).State);
        using var saved = JsonDocument.Parse(new FakeProtection().Unprotect(row.메타데이터)!);
        var evidence = saved.RootElement.GetProperty("inputEvidence");
        var expected = new Dictionary<string, string>
        {
            ["inputFingerprint"] = source.입력지문!, ["driverBaseFingerprint"] = source.기사기본지문!,
            ["vehicleFingerprint"] = source.실차제원지문!, ["driverFixedFingerprint"] = source.기사고정조건지문!,
            ["currentWorkFingerprint"] = source.현재수행지문!, ["locationObservationFingerprint"] = source.위치관측지문!,
            ["fingerprintVersion"] = source.지문판본!, ["requestFingerprint"] = "private-request-fingerprint-r25",
            ["driverFingerprint"] = "private-driver-fingerprint-r25", ["queryScope"] = source.조회범위
        };
        foreach (var pair in expected) Assert.Equal(pair.Value, evidence.GetProperty(pair.Key).GetString());
        Assert.Equal(source.입력지문, saved.RootElement.GetProperty("inputFingerprint").GetString());
        Assert.Equal(source.기준시각Utc, evidence.GetProperty("referenceAtUtc").GetDateTime());
        Assert.Equal(source.조회시작시각Utc, evidence.GetProperty("queryStartedAtUtc").GetDateTime());
        Assert.Equal(source.조회완료시각Utc, evidence.GetProperty("queryCompletedAtUtc").GetDateTime());
        Assert.Equal(source.위치기록시각Utc, evidence.GetProperty("locationRecordedAtUtc").GetDateTime());
        Assert.Equal(source.위치수신시각Utc, evidence.GetProperty("locationReceivedAtUtc").GetDateTime());
        Assert.True(evidence.GetProperty("atomicRead").GetBoolean());
        foreach (var excluded in new[] { otherRequest, otherDriver, "private-other-request-fingerprint-r25", "private-other-driver-fingerprint-r25",
                     "private-changed-request-r25", "private-changed-driver-r25", "private-raw-unconfirmed-reason-r25", "private-raw-block-reason-r25", "98765.432" })
            Assert.DoesNotContain(excluded, evidence.GetRawText(), StringComparison.Ordinal);
        await f.Db.SaveChangesAsync();
        requestEvidence.Clear();
        driverEvidence.Clear();
        var metadata = row.메타데이터;
        var item = Assert.Single((await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items);
        var notice = await f.Service.GetNoticeContextAsync(Owner, f.Request.의뢰Id, item.EvidenceId);
        var exposed = JsonSerializer.Serialize(new { Item = item, Notice = notice });
        foreach (var secret in expected.Values.Append(otherRequest).Append(otherDriver).Append(Driver))
            Assert.DoesNotContain(secret, exposed, StringComparison.Ordinal);
        Assert.Equal(metadata, (await f.Db.운송이벤트.AsNoTracking().SingleAsync()).메타데이터);
        Assert.Empty(f.Feedback.Items);
        Assert.Equal(0, await f.Db.Command알림Outbox.CountAsync());
        Assert.Equal(0, await f.Db.CommunityPostEmailNotificationOutbox.CountAsync());
    }

    private static void RewriteR25Uncommitted(운송이벤트 row, Action<JsonObject> change)
    {
        // Only unsaved synthetic fixture events are amended. Never modify a persisted append-only event.
        var protection = new FakeProtection();
        var payload = JsonNode.Parse(protection.Unprotect(row.메타데이터)!)!.AsObject();
        change(payload);
        row.메타데이터 = protection.Protect(payload.ToJsonString())!;
    }
}
