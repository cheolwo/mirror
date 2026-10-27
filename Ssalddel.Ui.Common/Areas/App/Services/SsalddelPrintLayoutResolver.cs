using Ssalddel.Contracts.Common.Documents;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>기존 용지·밀도·방향 별칭과 CSS 규격을 정규화합니다.</summary>
internal static class SsalddelPrintLayoutResolver
{
    internal static SsalddelPrintLayoutProfile ResolvePrintLayout(SsalddelDocumentPrintLayoutOptions? options)
    {
        var paperSize = NormalizePaperSize(options?.PaperSize);
        var density = NormalizeDensity(options?.Density);
        var printStyle = NormalizePrintStyle(options?.PrintStyle, paperSize);
        var orientation = IsThermalPaperSize(paperSize)
            ? SsalddelDocumentOrientationCode.Portrait
            : NormalizeOrientation(options?.Orientation);

        var pageSizeCss = ResolvePageSizeCss(paperSize, orientation);
        var (sheetWidth, sheetMinHeight, metaColumns) = ResolveSheetSize(paperSize, orientation);
        var densityCss = ResolveDensityCss(density, paperSize);

        return new SsalddelPrintLayoutProfile(
            paperSize,
            density,
            orientation,
            printStyle,
            pageSizeCss,
            densityCss.PageMargin,
            sheetWidth,
            sheetMinHeight,
            densityCss.SheetPadding,
            densityCss.Gap,
            densityCss.HeadGap,
            densityCss.TitleFont,
            densityCss.SubtitleFont,
            Math.Min(metaColumns, densityCss.MetaColumns),
            densityCss.BoxPadding,
            densityCss.ValueFont,
            densityCss.TableFont,
            densityCss.TableHeadFont,
            densityCss.CellPadding,
            densityCss.QuantityFont,
            densityCss.DocumentCodeWidth,
            densityCss.DocumentQrColumn,
            densityCss.BarcodeCellWidth,
            densityCss.LineCodeWidth,
            densityCss.FootFont,
            densityCss.DocumentQrSizePx,
            densityCss.DocumentBarcodeWidthPx,
            densityCss.DocumentBarcodeHeightPx,
            densityCss.LineBarcodeWidthPx,
            densityCss.LineBarcodeHeightPx,
            options?.IncludeDocumentQrCode ?? true,
            options?.IncludeLineBarcodes ?? true,
            printStyle == SsalddelDocumentPrintStyleCode.ReceiptSlip);
    }

    private static string NormalizePaperSize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "a5" => SsalddelDocumentPaperSizeCode.A5,
            "a6" => SsalddelDocumentPaperSizeCode.A6,
            "thermal-80" or "receipt-80" or "80mm" => SsalddelDocumentPaperSizeCode.Thermal80,
            "thermal-58" or "receipt-58" or "58mm" => SsalddelDocumentPaperSizeCode.Thermal58,
            _ => SsalddelDocumentPaperSizeCode.A4
        };
    }

    private static string NormalizeDensity(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "comfortable" or "normal" => SsalddelDocumentDensityCode.Comfortable,
            "dense" => SsalddelDocumentDensityCode.Dense,
            _ => SsalddelDocumentDensityCode.Compact
        };
    }

    private static string NormalizeOrientation(string? value)
    {
        return string.Equals(value, SsalddelDocumentOrientationCode.Landscape, StringComparison.OrdinalIgnoreCase)
            ? SsalddelDocumentOrientationCode.Landscape
            : SsalddelDocumentOrientationCode.Portrait;
    }

    private static string NormalizePrintStyle(string? value, string paperSize)
    {
        if (IsThermalPaperSize(paperSize))
        {
            return SsalddelDocumentPrintStyleCode.ReceiptSlip;
        }

        return string.Equals(value, SsalddelDocumentPrintStyleCode.ReceiptSlip, StringComparison.OrdinalIgnoreCase)
            ? SsalddelDocumentPrintStyleCode.ReceiptSlip
            : SsalddelDocumentPrintStyleCode.Sheet;
    }

    private static bool IsThermalPaperSize(string paperSize)
    {
        return paperSize is SsalddelDocumentPaperSizeCode.Thermal80 or SsalddelDocumentPaperSizeCode.Thermal58;
    }

    private static string ResolvePageSizeCss(string paperSize, string orientation)
    {
        return paperSize switch
        {
            SsalddelDocumentPaperSizeCode.A5 => $"A5 {orientation}",
            SsalddelDocumentPaperSizeCode.A6 => $"A6 {orientation}",
            SsalddelDocumentPaperSizeCode.Thermal80 => "80mm 200mm",
            SsalddelDocumentPaperSizeCode.Thermal58 => "58mm 200mm",
            _ => $"A4 {orientation}"
        };
    }

    private static (string SheetWidth, string SheetMinHeight, int MetaColumns) ResolveSheetSize(string paperSize, string orientation)
    {
        return (paperSize, orientation) switch
        {
            (SsalddelDocumentPaperSizeCode.A4, SsalddelDocumentOrientationCode.Landscape) => ("277mm", "180mm", 6),
            (SsalddelDocumentPaperSizeCode.A5, SsalddelDocumentOrientationCode.Landscape) => ("190mm", "128mm", 4),
            (SsalddelDocumentPaperSizeCode.A5, _) => ("128mm", "190mm", 3),
            (SsalddelDocumentPaperSizeCode.A6, SsalddelDocumentOrientationCode.Landscape) => ("128mm", "88mm", 3),
            (SsalddelDocumentPaperSizeCode.A6, _) => ("88mm", "128mm", 2),
            (SsalddelDocumentPaperSizeCode.Thermal80, _) => ("72mm", "auto", 1),
            (SsalddelDocumentPaperSizeCode.Thermal58, _) => ("50mm", "auto", 1),
            _ => ("190mm", "267mm", 4)
        };
    }

    private static SsalddelDensityCss ResolveDensityCss(string density, string paperSize)
    {
        if (paperSize == SsalddelDocumentPaperSizeCode.Thermal58)
        {
            return new(
                "3mm",
                "2mm",
                "3mm",
                "3mm",
                "13px",
                "7px",
                1,
                "2px",
                "8px",
                "7.5px",
                "7px",
                "2px",
                "12px",
                "100%",
                "0",
                "24mm",
                "24mm",
                "7px",
                0,
                128,
                36,
                116,
                32);
        }

        if (paperSize == SsalddelDocumentPaperSizeCode.Thermal80)
        {
            return new(
                "4mm",
                "2mm",
                "3mm",
                "3mm",
                "15px",
                "8px",
                1,
                "3px",
                "9px",
                "8px",
                "7px",
                "2px",
                "13px",
                "100%",
                "0",
                "28mm",
                "26mm",
                "7px",
                0,
                160,
                42,
                130,
                36);
        }

        return density switch
        {
            SsalddelDocumentDensityCode.Comfortable => new(
                "10mm",
                "12mm",
                "8px",
                "14px",
                "25px",
                "12px",
                4,
                "9px",
                "13px",
                "11px",
                "10px",
                "6px",
                "16px",
                "70mm",
                "24mm",
                "44mm",
                "42mm",
                "10px",
                120,
                260,
                80,
                240,
                70),
            SsalddelDocumentDensityCode.Dense => new(
                "5mm",
                "5mm",
                "4px",
                "6px",
                "16px",
                "9px",
                6,
                "4px",
                "10px",
                "8px",
                "7.5px",
                "3px",
                "13px",
                "48mm",
                "14mm",
                "30mm",
                "28mm",
                "7.5px",
                72,
                180,
                48,
                160,
                42),
            _ => new(
                "8mm",
                "8mm",
                "6px",
                "10px",
                "20px",
                "10px",
                5,
                "6px",
                "11px",
                "9.5px",
                "9px",
                "4px",
                "14px",
                "58mm",
                "18mm",
                "36mm",
                "34mm",
                "8px",
                96,
                220,
                62,
                200,
                54)
        };
    }

}

internal sealed record SsalddelPrintLayoutProfile(
    string PaperSize,
    string Density,
    string Orientation,
    string PrintStyle,
    string PageSizeCss,
    string PageMarginCss,
    string SheetWidthCss,
    string SheetMinHeightCss,
    string SheetPaddingCss,
    string GapCss,
    string HeadGapCss,
    string TitleFontCss,
    string SubtitleFontCss,
    int MetaColumns,
    string BoxPaddingCss,
    string ValueFontCss,
    string TableFontCss,
    string TableHeadFontCss,
    string CellPaddingCss,
    string QuantityFontCss,
    string DocumentCodeWidthCss,
    string DocumentQrColumnCss,
    string BarcodeCellWidthCss,
    string LineCodeWidthCss,
    string FootFontCss,
    int DocumentQrSizePx,
    int DocumentBarcodeWidthPx,
    int DocumentBarcodeHeightPx,
    int LineBarcodeWidthPx,
    int LineBarcodeHeightPx,
    bool IncludeDocumentQrCode,
    bool IncludeLineBarcodes,
    bool UseReceiptSlipLayout);

internal sealed record SsalddelDensityCss(
    string PageMargin,
    string SheetPadding,
    string Gap,
    string HeadGap,
    string TitleFont,
    string SubtitleFont,
    int MetaColumns,
    string BoxPadding,
    string ValueFont,
    string TableFont,
    string TableHeadFont,
    string CellPadding,
    string QuantityFont,
    string DocumentCodeWidth,
    string DocumentQrColumn,
    string BarcodeCellWidth,
    string LineCodeWidth,
    string FootFont,
    int DocumentQrSizePx,
    int DocumentBarcodeWidthPx,
    int DocumentBarcodeHeightPx,
    int LineBarcodeWidthPx,
    int LineBarcodeHeightPx);
