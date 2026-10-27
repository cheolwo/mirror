using Ssalddel.Contracts.Common.Documents;
using Ssalddel.Contracts.Common.Identifiers;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>HTML과 평문에서 같은 품목 식별값의 우선순위를 재사용합니다.</summary>
internal static class SsalddelDocumentIdentifierResolver
{
    internal static SsalddelIdentifierCodePayload ResolveLineBarcodePayload(SsalddelExpectedItemDocumentLine line)
    {
        if (line.BarcodePayload is not null)
        {
            return line.BarcodePayload;
        }

        if (!string.IsNullOrWhiteSpace(line.ProductBarcode))
        {
            return line.ProductBarcode.Contains(':', StringComparison.Ordinal)
                ? SsalddelIdentifierCodePayloads.Parse(line.ProductBarcode)
                : SsalddelIdentifierCodePayloads.Create(SsalddelIdentifierKindCode.Product, line.ProductBarcode);
        }

        if (!string.IsNullOrWhiteSpace(line.BundleBarcode))
        {
            return line.BundleBarcode.Contains(':', StringComparison.Ordinal)
                ? SsalddelIdentifierCodePayloads.Parse(line.BundleBarcode)
                : SsalddelIdentifierCodePayloads.Create(SsalddelIdentifierKindCode.Bundle, line.BundleBarcode);
        }

        return SsalddelIdentifierCodePayloads.Create(
            SsalddelIdentifierKindCode.Product,
            string.IsNullOrWhiteSpace(line.Sku) ? line.LineNo : line.Sku);
    }

}
