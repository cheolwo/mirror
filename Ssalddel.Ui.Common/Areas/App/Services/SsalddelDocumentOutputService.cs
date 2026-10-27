using static Ssalddel.Ui.Common.Areas.App.Services.SsalddelDocumentIdentifierResolver;
using System.Net;
using System.Text;
using Ssalddel.Contracts.Common.Documents;
using Ssalddel.Contracts.Common.Identifiers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public sealed class SsalddelDocumentOutputService(ISsalddelIdentifierCodeGenerator identifierCodeGenerator) : ISsalddelDocumentOutputService
{
    private readonly SsalddelDocumentHtmlRenderer htmlRenderer = new(identifierCodeGenerator);

    public SsalddelDocumentOutput CreateWaybill(SsalddelWaybillDocumentDraft draft)
    {
        var title = $"운송장 {draft.DocumentNo}";
        var html = SsalddelDocumentHtmlRenderer.BuildWaybillHtml(draft, title);
        var plainText = BuildPlainText(draft);

        return new SsalddelDocumentOutput(
            $"ssalddel-waybill-{SanitizeFileName(draft.DocumentNo)}.html",
            title,
            "text/html;charset=utf-8",
            html,
            plainText);
    }

    public SsalddelDocumentOutput CreateInboundExpectedItems(SsalddelExpectedItemDocumentDraft draft)
    {
        return CreateExpectedItems(
            draft,
            SsalddelExpectedItemDocumentKindCode.InboundExpectedItems,
            "입고예정 품목표",
            "inbound-expected-items",
            SsalddelIdentifierKindCode.InboundRequest);
    }

    public SsalddelDocumentOutput CreateOutboundExpectedItems(SsalddelExpectedItemDocumentDraft draft)
    {
        return CreateExpectedItems(
            draft,
            SsalddelExpectedItemDocumentKindCode.OutboundExpectedItems,
            "출고예정 품목표",
            "outbound-expected-items",
            SsalddelIdentifierKindCode.OutboundPlan);
    }

    private SsalddelDocumentOutput CreateExpectedItems(
        SsalddelExpectedItemDocumentDraft draft,
        string documentKind,
        string fallbackTitle,
        string fileKind,
        string fallbackBarcodeKind)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.DocumentNo);

        var title = string.IsNullOrWhiteSpace(draft.Title)
            ? $"{fallbackTitle} {draft.DocumentNo}"
            : draft.Title;
        var normalizedDraft = new SsalddelExpectedItemDocumentDraft
        {
            DocumentNo = draft.DocumentNo,
            DocumentKind = documentKind,
            Title = title,
            Status = draft.Status,
            WarehouseName = draft.WarehouseName,
            OwnerName = draft.OwnerName,
            CounterpartyName = draft.CounterpartyName,
            OrderNo = draft.OrderNo,
            LedgerNo = draft.LedgerNo,
            ExpectedDateText = draft.ExpectedDateText,
            WorkMemo = draft.WorkMemo,
            CreatedAt = draft.CreatedAt,
            PrintLayout = draft.PrintLayout,
            DocumentBarcodePayload = draft.DocumentBarcodePayload
                ?? SsalddelIdentifierCodePayloads.Create(fallbackBarcodeKind, draft.DocumentNo),
            Lines = draft.Lines
        };

        var html = htmlRenderer.BuildExpectedItemsHtml(normalizedDraft, fallbackTitle);
        var plainText = BuildExpectedItemsPlainText(normalizedDraft);

        return new SsalddelDocumentOutput(
            $"ssalddel-{fileKind}-{SanitizeFileName(draft.DocumentNo)}.html",
            title,
            "text/html;charset=utf-8",
            html,
            plainText);
    }

    private static string BuildPlainText(SsalddelWaybillDocumentDraft draft)
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"운송장: {draft.DocumentNo}",
            $"화물: {draft.CargoName}",
            $"상차: {draft.PickupPlace} / {draft.PickupAddress} / {draft.PickupTime}",
            $"하차: {draft.DropoffPlace} / {draft.DropoffAddress} / {draft.DropoffTime}",
            $"정산: {draft.PaymentMethod} / {(draft.ReceiptRequired ? "인수증 필요" : "인수증 없음")}",
            $"예상 운임: {draft.ExpectedFare:N0}원",
            $"메모: {draft.Memo}"
        });
    }

    private static string BuildExpectedItemsPlainText(SsalddelExpectedItemDocumentDraft draft)
    {
        var lines = draft.Lines.Select(line =>
        {
            var payload = ResolveLineBarcodePayload(line);
            return $"- {line.LineNo} / {line.ProductName} / {line.Quantity:N0}{line.Unit} / {payload.RawCode}";
        });

        return string.Join(Environment.NewLine, new[]
        {
            $"{draft.Title}: {draft.DocumentNo}",
            $"상태: {draft.Status}",
            $"창고: {draft.WarehouseName}",
            $"소유/화주: {draft.OwnerName}",
            $"상대/공급·수령: {draft.CounterpartyName}",
            $"주문 번호: {draft.OrderNo}",
            $"원장 번호: {draft.LedgerNo}",
            $"예정일: {draft.ExpectedDateText}",
            "품목:",
            string.Join(Environment.NewLine, lines),
            $"메모: {draft.WorkMemo}"
        });
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "document" : safe;
    }
}

public static class SsalddelDocumentOutputServiceCollectionExtensions
{
    public static IServiceCollection AddSsalddelDocumentOutputServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ISsalddelIdentifierCodeGenerator, ZxingSsalddelIdentifierCodeGenerator>();
        services.AddSingleton<ISsalddelDocumentOutputService, SsalddelDocumentOutputService>();
        return services;
    }
}
