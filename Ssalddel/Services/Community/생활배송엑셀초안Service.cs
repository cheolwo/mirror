using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Privacy;

namespace Ssalddel.Services.Community;

public interface I생활배송엑셀초안Service
{
    NeighborhoodDeliveryInputTemplateResponse 양식조회();
    Task<Result<NeighborhoodDeliveryInputPreviewResponse>> 미리보기Async(
        NeighborhoodDeliveryInputPreviewRequest request, CancellationToken cancellationToken = default);
}

/// <summary>동의를 확인한 입력을 메모리에서만 읽습니다. 견적·접수·외부 조회·원장 변경을 수행하지 않습니다.</summary>
public sealed class 생활배송엑셀초안Service(ICurrentUserAccessor currentUser, I신청개인정보동의증적Service consent)
    : I생활배송엑셀초안Service
{
    private const int MaximumExpandedBytes = 20 * 1024 * 1024;
    private const int MaximumPartBytes = 2 * 1024 * 1024;
    private const int MaximumEntries = 256;
    private const int MaximumCells = 4096;
    private const int MaximumSharedStrings = 2048;
    private const int MaximumCellCharacters = 4096;
    private static readonly XNamespace SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace OfficeRelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace ContentTypeNs = "http://schemas.openxmlformats.org/package/2006/content-types";

    public NeighborhoodDeliveryInputTemplateResponse 양식조회()
    {
        using var stream = typeof(생활배송엑셀초안Service).Assembly.GetManifestResourceStream(NeighborhoodDeliveryInputSchema.TemplateResourceName)
            ?? throw new InvalidOperationException("생활 배송 입력 양식이 준비되지 않았습니다.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return new NeighborhoodDeliveryInputTemplateResponse { ContentBase64 = Convert.ToBase64String(buffer.ToArray()) };
    }

    public async Task<Result<NeighborhoodDeliveryInputPreviewResponse>> 미리보기Async(
        NeighborhoodDeliveryInputPreviewRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var actor = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(actor)) return Failure("AuthenticationRequired", "로그인 후 입력 자료를 확인해 주세요.", 401);
        if (request?.PrivacyConsentEvidenceId is null || request.PrivacyConsentEvidenceId == Guid.Empty)
            return Failure("PrivacyConsentRequired", "입력 자료를 올리기 전에 개인정보 수집·이용 동의를 확인해 주세요.");
        try
        {
            await consent.유효한동의요구Async(request.PrivacyConsentEvidenceId, 신청개인정보업무Codes.운송대행,
                NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, actor, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Failure("PrivacyConsentRequired", "현재 사용자와 신청에 유효한 개인정보 동의가 필요합니다.");
        }
        // 읽는 동안 인증 주체가 바뀌었으면 이전 주체의 자료를 반환하지 않습니다.
        if (!string.Equals(actor, currentUser.UserId, StringComparison.Ordinal))
            return Failure("AuthenticationRequired", "로그인 상태가 바뀌었습니다. 다시 확인해 주세요.", 401);
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 200 || !request.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Result.Ok(Rejected("UnsupportedFile", "판본이 있는 .xlsx 입력 양식을 사용해 주세요."));
        if (string.IsNullOrWhiteSpace(request.ContentBase64)
            || request.ContentBase64.Length > ((NeighborhoodDeliveryInputSchema.MaximumFileBytes + 2) / 3) * 4)
            return Result.Ok(Rejected("FileTooLarge", "빈 파일이거나 파일 크기 제한을 넘었습니다."));
        try
        {
            var bytes = Convert.FromBase64String(request.ContentBase64);
            var parsed = Parse(bytes, cancellationToken);
            if (!string.Equals(actor, currentUser.UserId, StringComparison.Ordinal))
                return Failure("AuthenticationRequired", "로그인 상태가 바뀌었습니다. 다시 확인해 주세요.", 401);
            return Result.Ok(parsed);
        }
        catch (FormatException)
        {
            return Result.Ok(Rejected("InvalidFile", "입력 파일을 읽을 수 없습니다. 양식을 다시 저장해 주세요."));
        }
    }

    /// <summary>순수 파싱 경계. 반환 값은 불완전한 초안이며 접수 승인이나 정본을 뜻하지 않습니다.</summary>
    public static NeighborhoodDeliveryInputPreviewResponse Parse(byte[] bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length < 4 || bytes.Length > NeighborhoodDeliveryInputSchema.MaximumFileBytes)
            return Rejected("FileTooLarge", "빈 파일이거나 파일 크기 제한을 넘었습니다.");
        if (bytes[0] != (byte)'P' || bytes[1] != (byte)'K' || bytes[2] != 3 || bytes[3] != 4)
            return Rejected("InvalidFile", "암호화되지 않은 표준 .xlsx 파일을 사용해 주세요.");
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
            var documents = ReadParts(archive, cancellationToken);
            return ReadRows(documents, cancellationToken);
        }
        catch (InputRejectedException exception) { return Rejected(exception.Code, exception.SafeMessage); }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or ArgumentException or IOException or FormatException or OverflowException or InvalidOperationException)
        {
            // 예외·파일 이름·셀·연락처 원문을 오류나 로그에 포함하지 않습니다.
            return Rejected("InvalidFile", "입력 파일을 읽을 수 없습니다. 표준 양식을 다시 저장해 주세요.");
        }
    }

    private static Dictionary<string, XDocument> ReadParts(ZipArchive archive, CancellationToken ct)
    {
        Require(archive.Entries.Count is > 0 and <= MaximumEntries, "ArchiveLimit", "파일 구성 항목 수가 제한을 넘었습니다.");
        var documents = new Dictionary<string, XDocument>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        var nodes = 0;
        var cells = 0;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var path = entry.FullName;
            Require(path.Length is > 0 and <= 200 && !path.StartsWith('/') && !path.Contains('\\')
                && !path.Contains(':') && !path.Split('/').Any(p => p is ".." or "." or ""),
                "UnsafeArchivePath", "안전하지 않은 파일 구성 경로입니다.");
            Require(names.Add(path), "DuplicatePart", "중복된 파일 구성 항목이 있습니다.");
            Require(!path.Contains("vba", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("externalLink", StringComparison.OrdinalIgnoreCase)
                && (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)),
                "UnsupportedPart", "매크로·외부 연결·지원하지 않는 파일 구성은 사용할 수 없습니다.");
            Require(entry.Length <= MaximumPartBytes && entry.Length >= 0,
                "PartTooLarge", "파일 구성 항목의 크기가 제한을 넘었습니다.");
            expanded += entry.Length;
            Require(expanded <= MaximumExpandedBytes, "ExpandedFileTooLarge", "압축 해제 크기가 제한을 넘었습니다.");
            using var source = entry.Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                Require(buffer.Length + count <= MaximumPartBytes, "PartTooLarge", "파일 구성 항목의 크기가 제한을 넘었습니다.");
                buffer.Write(chunk, 0, count);
            }
            Require(buffer.Length == entry.Length, "InvalidFile", "파일 구성 크기를 확인할 수 없습니다.");
            buffer.Position = 0;
            using var reader = XmlReader.Create(buffer, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumPartBytes,
                MaxCharactersFromEntities = 0, IgnoreComments = true, IgnoreProcessingInstructions = true
            });
            var document = XDocument.Load(reader);
            foreach (var element in document.Descendants())
            {
                ct.ThrowIfCancellationRequested();
                Require(++nodes <= 60000, "XmlLimit", "파일 정보량이 제한을 넘었습니다.");
                Require(element.Name != SheetNs + "f", "FormulaNotAllowed", "수식은 읽지 않습니다. 값만 남긴 양식으로 다시 올려 주세요.");
                if (element.Name == SheetNs + "c") Require(++cells <= MaximumCells, "CellLimit", "셀 수가 제한을 넘었습니다.");
                Require(!element.Attributes().Any(a => a.Name.LocalName == "TargetMode" && a.Value.Equals("External", StringComparison.OrdinalIgnoreCase)),
                    "ExternalLinkNotAllowed", "외부 연결이 있는 파일은 사용할 수 없습니다.");
                if (element.Name == RelationshipNs + "Relationship")
                    Require(!Uri.TryCreate((string?)element.Attribute("Target"), UriKind.Absolute, out var absolute) || absolute.IsFile && ((string?)element.Attribute("Target"))?.StartsWith('/') == true,
                        "ExternalLinkNotAllowed", "외부 연결이 있는 파일은 사용할 수 없습니다.");
            }
            documents.Add(path, document);
        }
        var types = Part(documents, "[Content_Types].xml");
        Require(types.Root?.Name == ContentTypeNs + "Types", "UnsupportedWorkbook", "표준 .xlsx 통합 문서만 사용할 수 있습니다.");
        var defaultTypes = types.Root!.Elements(ContentTypeNs + "Default").ToArray();
        // OPC는 해당 part의 Override를 우선하고 없으면 확장자 Default를 사용합니다.
        Require(!defaultTypes.GroupBy(e => (string?)e.Attribute("Extension") ?? string.Empty, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1),
            "DuplicateContentType", "중복된 파일 형식 정의가 있습니다.");
        var xmlType = defaultTypes.SingleOrDefault(e => string.Equals((string?)e.Attribute("Extension"), "xml", StringComparison.OrdinalIgnoreCase));
        var workbookType = types.Root!.Elements(ContentTypeNs + "Override").SingleOrDefault(e => (string?)e.Attribute("PartName") == "/xl/workbook.xml");
        Require((string?)(workbookType ?? xmlType)?.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
            "UnsupportedWorkbook", "표준 .xlsx 통합 문서만 사용할 수 있습니다.");
        Require(!types.Descendants().Attributes("ContentType").Any(a => a.Value.Contains("macro", StringComparison.OrdinalIgnoreCase)),
            "MacroNotAllowed", "매크로 파일은 사용할 수 없습니다.");
        return documents;
    }

    private static NeighborhoodDeliveryInputPreviewResponse ReadRows(Dictionary<string, XDocument> documents, CancellationToken ct)
    {
        var workbook = Part(documents, "xl/workbook.xml");
        Require(workbook.Root?.Name == SheetNs + "workbook", "InvalidWorkbookStructure", "통합 문서의 표준 구조를 확인해 주세요.");
        var sheetsElement = workbook.Root!.Elements(SheetNs + "sheets").ToArray();
        Require(sheetsElement.Length == 1 && workbook.Descendants(SheetNs + "sheet").All(s => ReferenceEquals(s.Parent, sheetsElement[0])),
            "InvalidWorkbookStructure", "통합 문서의 시트 목록 구조를 확인해 주세요.");
        var sheets = sheetsElement[0].Elements(SheetNs + "sheet").Where(s => (string?)s.Attribute("name") == NeighborhoodDeliveryInputSchema.SheetName).ToArray();
        Require(sheets.Length == 1 && ((string?)sheets[0].Attribute("state") is null or "visible"),
            "InputSheetRequired", "보이는 배송입력 시트가 하나 필요합니다.");
        var sheetId = (string?)sheets[0].Attribute(OfficeRelationshipNs + "id");
        var relationships = Part(documents, "xl/_rels/workbook.xml.rels");
        Require(relationships.Root?.Name == RelationshipNs + "Relationships"
            && relationships.Descendants(RelationshipNs + "Relationship").All(r => ReferenceEquals(r.Parent, relationships.Root)),
            "InvalidWorkbookRelationship", "통합 문서의 시트 연결 구조를 확인해 주세요.");
        var relation = relationships.Root!.Elements(RelationshipNs + "Relationship")
            .SingleOrDefault(r => (string?)r.Attribute("Id") == sheetId);
        var target = (string?)relation?.Attribute("Target");
        Require(!string.IsNullOrWhiteSpace(target) && (string?)relation?.Attribute("Type") == OfficeRelationshipNs.NamespaceName + "/worksheet",
            "InputSheetRequired", "배송입력 시트의 연결을 확인할 수 없습니다.");
        var sheetPath = target!.StartsWith("/xl/", StringComparison.Ordinal) ? target[1..] : "xl/" + target;
        Require(sheetPath.StartsWith("xl/worksheets/", StringComparison.Ordinal) && !sheetPath.Split('/').Any(p => p is ".." or "."),
            "UnsafeArchivePath", "배송입력 시트의 연결 경로를 확인해 주세요.");
        var shared = documents.TryGetValue("xl/sharedStrings.xml", out var strings)
            ? strings.Descendants(SheetNs + "si").Select(ReadText).ToArray() : [];
        Require(shared.Length <= MaximumSharedStrings, "SharedStringLimit", "공유 문자열 수가 제한을 넘었습니다.");
        var date1904Value = (string?)workbook.Root?.Element(SheetNs + "workbookPr")?.Attribute("date1904");
        Require(date1904Value is null or "0" or "1" or "false" or "true", "UnsupportedDateSystem", "통합 문서의 날짜 기준을 확인해 주세요.");
        var date1904 = date1904Value is "1" or "true";
        var dateStyles = DateStyles(documents);
        var cells = new Dictionary<string, string>(StringComparer.Ordinal);
        var dateCells = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var numericCells = new HashSet<string>(StringComparer.Ordinal);
        var worksheet = Part(documents, sheetPath);
        Require(worksheet.Root?.Name == SheetNs + "worksheet", "InvalidSheetStructure", "배송입력 시트의 구조를 확인해 주세요.");
        var sheetData = worksheet.Root!.Elements(SheetNs + "sheetData").SingleOrDefault();
        Require(sheetData is not null, "InvalidSheetStructure", "배송입력 시트의 업무 행을 확인할 수 없습니다.");
        var sheetRows = sheetData!.Elements(SheetNs + "row").ToArray();
        var rowNumbers = new HashSet<int>();
        foreach (var row in sheetRows)
        {
            ct.ThrowIfCancellationRequested();
            Require(int.TryParse((string?)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var rowNumber)
                && rowNumber is >= 1 and <= 1048576 && rowNumbers.Add(rowNumber),
                "InvalidRowReference", "시트의 행 출처가 잘못되었거나 중복되었습니다.");
        }
        var inputCells = worksheet.Descendants(SheetNs + "c").ToArray();
        Require(inputCells.All(cell => cell.Parent?.Name == SheetNs + "row" && ReferenceEquals(cell.Parent.Parent, sheetData)),
            "InvalidSheetStructure", "업무 행 바깥의 셀은 사용할 수 없습니다.");
        foreach (var cell in inputCells)
        {
            ct.ThrowIfCancellationRequested();
            var reference = (string?)cell.Attribute("r") ?? string.Empty;
            Require(ValidReference(reference), "InvalidCellReference", "셀 출처를 확인할 수 없습니다.");
            Require(RowNumber(reference) == int.Parse((string)cell.Parent!.Attribute("r")!, CultureInfo.InvariantCulture),
                "InvalidCellReference", "셀과 행의 출처가 일치하지 않습니다.");
            var type = (string?)cell.Attribute("t");
            var raw = (string?)cell.Element(SheetNs + "v") ?? string.Empty;
            var value = type switch
            {
                "inlineStr" => ReadText(cell.Element(SheetNs + "is") ?? new XElement(SheetNs + "is")),
                "s" => int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < shared.Length
                    ? shared[index] : throw new InputRejectedException("InvalidSharedString", "공유 문자열 연결을 확인할 수 없습니다."),
                null or "n" or "str" or "d" => raw,
                _ => throw new InputRejectedException("UnsupportedCell", "지원하지 않는 셀 값입니다. 텍스트나 숫자로 다시 입력해 주세요.")
            };
            Require(value.Length <= MaximumCellCharacters && cells.TryAdd(reference, value), "InvalidCell", "셀 값이나 중복 출처를 확인해 주세요.");
            if (type is null or "n") numericCells.Add(reference);
            var style = (string?)cell.Attribute("s");
            if (style is not null)
            {
                Require(int.TryParse(style, NumberStyles.None, CultureInfo.InvariantCulture, out var styleId) && styleId < 1000,
                    "InvalidCellStyle", "셀 날짜 서식을 확인해 주세요.");
                if ((type is null or "n") && dateStyles.Contains(styleId)
                    && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
                    && double.IsFinite(serial) && serial is >= 0 and < 73000)
                {
                    var local = date1904
                        ? new DateTime(1904, 1, 1).AddDays(Math.Floor(serial)).AddMilliseconds(Math.Round((serial - Math.Floor(serial)) * 86400000))
                        : DateTime.FromOADate(serial);
                    if (local.Year is >= 2000 and <= 2100)
                        dateCells.Add(reference, new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime);
                }
            }
        }
        Require(cells.GetValueOrDefault("A1") == NeighborhoodDeliveryInputSchema.Version, "SchemaMismatch", "입력 양식의 판본을 확인해 주세요.");
        for (var column = 0; column < NeighborhoodDeliveryInputSchema.Headers.Count; column++)
            Require(cells.GetValueOrDefault(Column(column) + "3") == NeighborhoodDeliveryInputSchema.Headers[column],
                "HeaderMismatch", "필수 열의 이름과 순서가 다릅니다. 현재 양식을 사용해 주세요.");
        Require(!cells.Any(c => RowNumber(c.Key) == 3 && ColumnIndex(c.Key) >= NeighborhoodDeliveryInputSchema.Headers.Count && !string.IsNullOrWhiteSpace(c.Value)),
            "HeaderMismatch", "양식에 없는 열은 사용할 수 없습니다.");
        var dataRows = cells.Where(c => RowNumber(c.Key) >= 4 && !string.IsNullOrWhiteSpace(c.Value)).GroupBy(c => RowNumber(c.Key)).OrderBy(g => g.Key).ToArray();
        Require(dataRows.Length <= NeighborhoodDeliveryInputSchema.MaximumRows, "RowLimit", "최대 100개 업무 행만 확인할 수 있습니다.");
        var rows = new List<NeighborhoodDeliveryInputRow>();
        foreach (var row in dataRows)
        {
            ct.ThrowIfCancellationRequested();
            Require(row.All(c => ColumnIndex(c.Key) < NeighborhoodDeliveryInputSchema.Headers.Count), "UnknownColumn", "양식에 없는 열은 사용할 수 없습니다.");
            var raw = new Dictionary<string, string>(StringComparer.Ordinal);
            var source = new Dictionary<string, string>(StringComparer.Ordinal);
            var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            var numericFields = new HashSet<string>(StringComparer.Ordinal);
            for (var column = 0; column < NeighborhoodDeliveryInputSchema.FieldKeys.Count; column++)
            {
                var field = NeighborhoodDeliveryInputSchema.FieldKeys[column];
                var reference = Column(column) + row.Key.ToString(CultureInfo.InvariantCulture);
                raw[field] = cells.GetValueOrDefault(reference) ?? string.Empty;
                source[field] = NeighborhoodDeliveryInputSchema.SheetName + "!" + reference;
                if (dateCells.TryGetValue(reference, out var date)) dates[field] = date;
                if (numericCells.Contains(reference)) numericFields.Add(field);
            }
            rows.Add(MapRow(row.Key, raw, source, dates, numericFields));
        }
        foreach (var duplicate in rows.Where(r => r.SourceRowId.HasValue).GroupBy(r => r.SourceRowId).Where(g => g.Count() > 1))
            foreach (var row in duplicate) row.Issues = row.Issues.Append(Issue("SourceRowId", "DuplicateRowId", "다른 행과 요청 번호가 겹칩니다. 별도 배송이면 다른 행 ID를 사용해 주세요.")).ToArray();
        return new NeighborhoodDeliveryInputPreviewResponse
        {
            Rows = rows, Errors = rows.Count == 0 ? [Issue("File", "NoDataRows", "입력된 업무 행이 없습니다.")] : []
        };
    }

    private static HashSet<int> DateStyles(Dictionary<string, XDocument> documents)
    {
        if (!documents.TryGetValue("xl/styles.xml", out var styles)) return [];
        var formats = new Dictionary<int, string>();
        foreach (var format in styles.Descendants(SheetNs + "numFmt"))
        {
            Require(int.TryParse((string?)format.Attribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && formats.TryAdd(id, (string?)format.Attribute("formatCode") ?? string.Empty), "InvalidCellStyle", "셀 날짜 서식을 확인해 주세요.");
        }
        var definitions = styles.Root?.Element(SheetNs + "cellXfs")?.Elements(SheetNs + "xf").ToArray() ?? [];
        Require(definitions.Length <= 1000, "StyleLimit", "셀 서식 수가 제한을 넘었습니다.");
        var dates = new HashSet<int>();
        for (var index = 0; index < definitions.Length; index++)
        {
            Require(int.TryParse((string?)definitions[index].Attribute("numFmtId") ?? "0", NumberStyles.None, CultureInfo.InvariantCulture, out var id),
                "InvalidCellStyle", "셀 날짜 서식을 확인해 주세요.");
            // 시간만 있는 서식이나 숫자 서식을 날짜로 추측하지 않습니다.
            if (id is >= 14 and <= 17 or 22 || formats.TryGetValue(id, out var code) && IsDateFormat(code)) dates.Add(index);
        }
        return dates;
    }

    private static bool IsDateFormat(string format)
    {
        var visible = new StringBuilder();
        var quoted = false;
        var bracket = false;
        for (var index = 0; index < format.Length; index++)
        {
            var letter = format[index];
            if (letter == '"') { quoted = !quoted; continue; }
            if (!quoted && letter == '[') { bracket = true; continue; }
            if (!quoted && letter == ']') { bracket = false; continue; }
            if (!quoted && !bracket && (letter is '\\' or '_' or '*')) { index++; continue; }
            if (!quoted && !bracket) visible.Append(char.ToLowerInvariant(letter));
        }
        var text = visible.ToString();
        return text.Contains('y') && text.Contains('m') && text.Contains('d');
    }

    private static NeighborhoodDeliveryInputRow MapRow(int rowNumber, Dictionary<string, string> raw, Dictionary<string, string> source,
        Dictionary<string, DateTime> dates, HashSet<string> numericFields)
    {
        var issues = new List<NeighborhoodDeliveryInputIssue>();
        string Value(string key) => raw[key].Trim();
        string Text(string key, int maximum, bool required = false)
        {
            var value = Value(key);
            if (required && value.Length == 0) issues.Add(Issue(key, "Required", "이 항목을 입력해 주세요."));
            // 잘못된 선택값을 미입력처럼 지우지 않습니다. 기존 작성/접수 검증이 수정을 요구하도록 보존합니다.
            if (value.Length > maximum) issues.Add(Issue(key, "ValueTooLong", "입력 길이가 제한을 넘었습니다."));
            return value;
        }
        string Phone(string key)
        {
            var text = Text(key, 40, required: true);
            if (text.Length == 0) return string.Empty;
            var compact = text.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
            if (numericFields.Contains(key) || compact.Length is < 9 or > 11 || compact[0] != '0' || compact.Any(c => c is < '0' or > '9'))
            { issues.Add(Issue(key, "InvalidPhone", "앞자리 0을 포함한 전화번호를 텍스트로 확인해 주세요.")); return string.Empty; }
            return text;
        }
        DateTime? Date(string key, bool required)
        {
            if (dates.TryGetValue(key, out var formattedDate)) return formattedDate;
            var value = Value(key);
            if (value.Length == 0) { if (required) issues.Add(Issue(key, "Required", "시각을 입력해 주세요.")); return null; }
            if (DateTime.TryParseExact(value, ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                if (local.Year is >= 2000 and <= 2100) return new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime;
            }
            else if ((value.EndsWith('Z') || value.Length >= 6 && (value[^6] is '+' or '-') && value[^3] == ':')
                && DateTimeOffset.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mmK"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset) && offset.Year is >= 2000 and <= 2100)
                return offset.UtcDateTime;
            issues.Add(Issue(key, "InvalidDateTime", "한국 시각 yyyy-MM-dd HH:mm 또는 시간대가 있는 ISO 시각을 텍스트로 입력해 주세요."));
            return null;
        }
        TimeWindowDTO? Window(string prefix, bool required)
        {
            var start = Date(prefix + "Start", required);
            var end = Date(prefix + "End", required || start.HasValue);
            if (start is null && end is not null) issues.Add(Issue(prefix + "Start", "Required", "시간창 시작을 입력해 주세요."));
            if (start is null || end is null) return null;
            if (start >= end) { issues.Add(Issue(prefix + "End", "InvalidTimeWindow", "종료는 시작 시각보다 뒤여야 합니다.")); return null; }
            return new TimeWindowDTO { 시작일시 = start.Value, 종료일시 = end.Value };
        }
        Guid? id = null;
        if (Value("SourceRowId").Length > 0)
        {
            if (Guid.TryParseExact(Value("SourceRowId"), "D", out var parsed) && parsed != Guid.Empty) id = parsed;
            else issues.Add(Issue("SourceRowId", "InvalidRowId", "행 ID는 비어 있거나 유효한 GUID여야 합니다."));
        }
        var quantity = 0;
        if (!int.TryParse(Value("Quantity"), NumberStyles.None, CultureInfo.InvariantCulture, out quantity) || quantity is < 1 or > 1000)
        { quantity = 0; issues.Add(Issue("Quantity", "InvalidQuantity", "수량을 1~1000의 정수로 입력해 주세요.")); }
        decimal weight = 0;
        if (!decimal.TryParse(Value("WeightKg"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out weight) || weight is <= 0 or > 10000)
        { weight = 0; issues.Add(Issue("WeightKg", "InvalidWeight", "중량을 kg 단위의 양수로 입력해 주세요.")); }
        var size = Value("PackageSizeCode");
        if (size.Length > 0 && !NeighborhoodPackageSizes.IsKnown(size))
            issues.Add(Issue("PackageSizeCode", "InvalidPackageSize", "크기는 Small·Medium·Large 또는 미입력으로 지정해 주세요."));
        LocationContactDTO Location(string prefix, bool pickup) => new()
        {
            주소 = new AddressDTO { 도로명주소 = Text(prefix + "Address", 300, required: true), 상세주소 = Text(prefix + "DetailedAddress", 300) },
            연락처 = new ContactDTO { 이름 = Text(prefix + "ContactName", 100, required: true), 전화번호 = Phone(prefix + "Phone") },
            시간창 = Window(prefix, pickup)
        };
        var draft = new NeighborhoodDeliveryRequest
        {
            ClientRequestId = id ?? Guid.Empty, CargoName = Text("CargoName", 120, required: true), Quantity = quantity, WeightKg = weight,
            VehicleType = Text("VehicleType", 60), PackageSizeCode = size.Length == 0 ? null : size,
            Pickup = Location("Pickup", true), Dropoff = Location("Dropoff", false), Notes = Text("Notes", 1000)
        };
        if (draft.VehicleType.Length == 0) issues.Add(Issue("VehicleType", "ReviewRequired", "접수 전에 운송 수단을 선택해 주세요."));
        return new NeighborhoodDeliveryInputRow { RowNumber = rowNumber, SourceRowId = id, RawValues = raw, SourceCells = source, Draft = draft, Issues = issues };
    }

    private static string ReadText(XElement element)
    {
        var text = string.Concat(element.Descendants(SheetNs + "t").Select(t => t.Value));
        Require(text.Length <= MaximumCellCharacters, "StringTooLong", "셀 문자열 길이가 제한을 넘었습니다.");
        return text;
    }
    private static XDocument Part(Dictionary<string, XDocument> documents, string name)
        => documents.TryGetValue(name, out var document) ? document : throw new InputRejectedException("MissingPart", "필수 파일 구성 항목이 없습니다.");
    private static bool ValidReference(string value)
    {
        var letters = value.TakeWhile(c => c is >= 'A' and <= 'Z').Count();
        return letters is >= 1 and <= 3 && value.Length - letters is >= 1 and <= 7
            && value[letters] != '0' && value.Skip(letters).All(c => c is >= '0' and <= '9')
            && int.TryParse(value[letters..], out var row) && row is >= 1 and <= 1048576 && ColumnIndex(value) < 16384;
    }
    private static int RowNumber(string reference) => int.Parse(new string(reference.SkipWhile(char.IsLetter).ToArray()), CultureInfo.InvariantCulture);
    private static int ColumnIndex(string reference)
    {
        var column = 0;
        foreach (var letter in reference.TakeWhile(char.IsLetter)) column = column * 26 + letter - 'A' + 1;
        return column - 1;
    }
    private static string Column(int index)
    {
        var value = new StringBuilder();
        for (index++; index > 0; index = (index - 1) / 26) value.Insert(0, (char)('A' + (index - 1) % 26));
        return value.ToString();
    }
    private static NeighborhoodDeliveryInputIssue Issue(string field, string code, string message) => new(field, code, message);
    private static NeighborhoodDeliveryInputPreviewResponse Rejected(string code, string message) => new() { Errors = [Issue("File", code, message)] };
    private static Result<NeighborhoodDeliveryInputPreviewResponse> Failure(string code, string message, int status = 400)
        => Result.Fail<NeighborhoodDeliveryInputPreviewResponse>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));
    private static void Require(bool condition, string code, string message)
    {
        if (!condition) throw new InputRejectedException(code, message);
    }
    private sealed class InputRejectedException(string code, string safeMessage) : Exception
    {
        public string Code { get; } = code;
        public string SafeMessage { get; } = safeMessage;
    }
}
