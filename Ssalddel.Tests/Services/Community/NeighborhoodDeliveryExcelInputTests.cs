using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Controllers.Common;
using Ssalddel.Services.Community;
using Ssalddel.Services.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodDeliveryExcelInputTests
{
    private static readonly XNamespace SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly Guid ConsentId = Guid.Parse("894796b8-dea4-4ffc-8107-d5aa6fe4986b");
    private static readonly Guid RowId = Guid.Parse("28762270-cbb4-48b3-84bb-bddaf3e9d354");

    [Fact]
    public void 정상입력은셀출처와확인전초안만반환한다()
    {
        var result = Parse(Parts());
        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Empty(row.Issues);
        Assert.Equal(4, row.RowNumber);
        Assert.Equal(RowId, row.SourceRowId);
        Assert.Equal(RowId, row.Draft.ClientRequestId);
        Assert.Equal("배송입력!H4", row.SourceCells["PickupPhone"]);
        Assert.Equal("010-1234-5678", row.RawValues["PickupPhone"]);
        Assert.Equal("010-1234-5678", row.Draft.Pickup.연락처.전화번호);
        Assert.Equal(new DateTime(2030, 1, 2, 1, 0, 0, DateTimeKind.Utc), row.Draft.Pickup.시간창!.시작일시);
        Assert.Null(row.Draft.Pickup.주소.위도);
        Assert.Null(row.Draft.AgreedFareKrw);
        Assert.Null(row.Draft.PrivacyConsentEvidenceId);
        Assert.Null(row.Draft.AutoFulfillment);
        Assert.False(row.Draft.DispatchRequested);
        Assert.False(row.Draft.DirectPaymentAgreed);
    }

    [Fact]
    public void 누락은수량중량차량시간연락처를추정하지않는다()
    {
        var result = Parse(Parts(new Dictionary<string, string> { ["A4"] = "", ["C4"] = "", ["D4"] = "", ["H4"] = "", ["M4"] = "", ["N4"] = "", ["S4"] = "" }));
        var row = Assert.Single(result.Rows);
        Assert.Equal(Guid.Empty, row.Draft.ClientRequestId);
        Assert.Null(row.SourceRowId);
        Assert.Equal(0, row.Draft.Quantity);
        Assert.Equal(0, row.Draft.WeightKg);
        Assert.Equal("", row.Draft.VehicleType);
        Assert.Null(row.Draft.Pickup.시간창);
        Assert.Contains(row.Issues, x => x.FieldKey == "PickupPhone" && x.Code == "Required");
        Assert.Contains(row.Issues, x => x.FieldKey == "WeightKg");
    }

    [Theory]
    [InlineData("1012345678")]
    [InlineData("0101234BAD")]
    [InlineData("123")]
    public void 전화번호유실이나잘못된문자를복구하지않는다(string phone)
    {
        var row = Assert.Single(Parse(Parts(new Dictionary<string, string> { ["H4"] = phone })).Rows);
        Assert.Equal(phone, row.RawValues["PickupPhone"]);
        Assert.Equal("", row.Draft.Pickup.연락처.전화번호);
        Assert.Contains(row.Issues, x => x.Code == "InvalidPhone");
        Assert.DoesNotContain(row.Issues, x => x.Message.Contains(phone, StringComparison.Ordinal));
    }

    [Fact]
    public void 숫자형전화셀은텍스트확인을요구한다()
    {
        var parts = Parts();
        ReplaceCell(parts, "H4", new XElement(SheetNs + "c", new XAttribute("r", "H4"), new XElement(SheetNs + "v", "01012345678")));
        var row = Assert.Single(Parse(parts).Rows);
        Assert.Equal("01012345678", row.RawValues["PickupPhone"]);
        Assert.Equal("", row.Draft.Pickup.연락처.전화번호);
        Assert.Contains(row.Issues, x => x.Code == "InvalidPhone");
    }

    [Fact]
    public void 업로드파일과파일이름은기존암호화전송계약으로분류한다()
    {
        var fields = IsmsPProtectedDataAttributeReader.Read<NeighborhoodDeliveryInputPreviewRequest>();
        Assert.Contains(fields, x => x.PropertyName == nameof(NeighborhoodDeliveryInputPreviewRequest.FileName) && x.IsPersonalData);
        Assert.Contains(fields, x => x.PropertyName == nameof(NeighborhoodDeliveryInputPreviewRequest.ContentBase64) && x.FieldKey == PersonalDataFieldKey.PhoneNumber);
        Assert.Contains(fields, x => x.PropertyName == nameof(NeighborhoodDeliveryInputPreviewRequest.ContentBase64) && x.FieldKey == PersonalDataFieldKey.DetailedAddress);
        Assert.True(SsalddelIsmsPClientEncryptionService.RequiresEncryptedTransport<NeighborhoodDeliveryInputPreviewRequest>());
        Assert.True(SsalddelIsmsPClientEncryptionService.RequiresEncryptedTransport<NeighborhoodDeliveryRequest>());
        Assert.False(IsmsPProtectedDataAttributeReader.BuildFieldProtectionPlan(typeof(NeighborhoodDeliveryInputPreviewRequest)).HasUnknownFields);
    }

    [Theory]
    [InlineData("01/02/2030")]
    [InlineData("47500")]
    [InlineData("2030-01-02")]
    [InlineData("2030-01-02 25:00")]
    public void 모호한날짜와서식없는숫자는확정하지않는다(string date)
    {
        var row = Assert.Single(Parse(Parts(new Dictionary<string, string> { ["M4"] = date })).Rows);
        Assert.Null(row.Draft.Pickup.시간창);
        Assert.Contains(row.Issues, x => x.FieldKey == "PickupStart" && x.Code == "InvalidDateTime");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 명시적날짜서식과엑셀날짜기준을확인한다(bool date1904)
    {
        var parts = Parts();
        parts["xl/styles.xml"] = $"<styleSheet xmlns='{SheetNs}'><numFmts><numFmt numFmtId='164' formatCode='yyyy-mm-dd hh:mm'/></numFmts><cellXfs><xf numFmtId='0'/><xf numFmtId='164'/></cellXfs></styleSheet>";
        if (date1904)
        {
            var workbook = XDocument.Parse(parts["xl/workbook.xml"]);
            workbook.Root!.AddFirst(new XElement(SheetNs + "workbookPr", new XAttribute("date1904", "1")));
            parts["xl/workbook.xml"] = workbook.ToString();
        }
        var localStart = new DateTime(2030, 1, 2, 10, 0, 0);
        var serial = date1904 ? (localStart - new DateTime(1904, 1, 1)).TotalDays : localStart.ToOADate();
        ReplaceCell(parts, "M4", new XElement(SheetNs + "c", new XAttribute("r", "M4"), new XAttribute("s", "1"), new XElement(SheetNs + "v", serial.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
        var row = Assert.Single(Parse(parts).Rows);
        Assert.Empty(row.Issues);
        Assert.Equal(new DateTime(2030, 1, 2, 1, 0, 0, DateTimeKind.Utc), row.Draft.Pickup.시간창!.시작일시);
        Assert.NotEqual("2030-01-02 10:00", row.RawValues["PickupStart"]);
    }

    [Fact]
    public void 시간대있는ISO시각을UTC로읽는다()
    {
        var row = Assert.Single(Parse(Parts(new Dictionary<string, string> { ["M4"] = "2030-01-02T10:00:00+09:00" })).Rows);
        Assert.Empty(row.Issues);
        Assert.Equal(new DateTime(2030, 1, 2, 1, 0, 0, DateTimeKind.Utc), row.Draft.Pickup.시간창!.시작일시);
    }

    [Fact]
    public void 역순시간창과잘못된포장규격은행보완으로남긴다()
    {
        var row = Assert.Single(Parse(Parts(new Dictionary<string, string> { ["N4"] = "2030-01-02 09:00", ["O4"] = "Unknown" })).Rows);
        Assert.Contains(row.Issues, x => x.Code == "InvalidTimeWindow");
        Assert.Contains(row.Issues, x => x.Code == "InvalidPackageSize");
        Assert.Equal("Unknown", row.Draft.PackageSizeCode);
        Assert.False(NeighborhoodPackageSizes.IsKnown(row.Draft.PackageSizeCode));
    }

    [Fact]
    public void 잘못된선택값은미입력처럼지워지지않고수정검증까지남는다()
    {
        var detail = new string('가', 301);
        var notes = new string('나', 1001);
        var row = Assert.Single(Parse(Parts(new Dictionary<string, string>
        { ["F4"] = detail, ["J4"] = detail, ["R4"] = notes, ["O4"] = "Unknown" })).Rows);
        Assert.Equal(detail, row.Draft.Pickup.주소.상세주소);
        Assert.Equal(detail, row.Draft.Dropoff.주소.상세주소);
        Assert.Equal(notes, row.Draft.Notes);
        Assert.Equal("Unknown", row.Draft.PackageSizeCode);
        Assert.False(NeighborhoodPackageSizes.IsKnown(row.Draft.PackageSizeCode));
        Assert.Contains(row.Issues, issue => issue.FieldKey == "PickupDetailedAddress" && issue.Code == "ValueTooLong");
        Assert.Contains(row.Issues, issue => issue.FieldKey == "DropoffDetailedAddress" && issue.Code == "ValueTooLong");
        Assert.Contains(row.Issues, issue => issue.FieldKey == "Notes" && issue.Code == "ValueTooLong");
        Assert.Equal(notes, row.RawValues["Notes"]);
    }

    [Theory]
    [InlineData("A1", "other.version", "SchemaMismatch")]
    [InlineData("B3", "Wrong", "HeaderMismatch")]
    [InlineData("T3", "Unknown", "HeaderMismatch")]
    public void 다른판본과잘못된열을거절한다(string cell, string value, string code)
        => AssertRejected(Parse(Parts(new Dictionary<string, string> { [cell] = value })), code);

    [Fact]
    public void 중복행ID는양쪽행에보완항목을남긴다()
    {
        var parts = Parts();
        AddCopyRow(parts, 4, 5);
        var result = Parse(parts);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Contains(row.Issues, x => x.Code == "DuplicateRowId"));
    }

    [Fact]
    public void 같은내용별도배송은행ID없이자동합치지않는다()
    {
        var parts = Parts(new Dictionary<string, string> { ["A4"] = "" });
        AddCopyRow(parts, 4, 5);
        var result = Parse(parts);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => { Assert.Null(row.SourceRowId); Assert.DoesNotContain(row.Issues, x => x.Code == "DuplicateRowId"); });
    }

    [Fact]
    public void 백개업무행상한과빈서식행을구분한다()
    {
        var parts = Parts(new Dictionary<string, string> { ["A4"] = "" });
        for (var row = 5; row < 104; row++) AddCopyRow(parts, 4, row);
        Assert.Equal(100, Parse(parts).Rows.Count);
        AddCopyRow(parts, 4, 104);
        AssertRejected(Parse(parts), "RowLimit");
    }

    [Fact]
    public void 공유문자열을읽고유효하지않은참조를거절한다()
    {
        var parts = Parts();
        parts["xl/sharedStrings.xml"] = $"<sst xmlns='{SheetNs}'><si><t>공유 물품</t></si></sst>";
        ReplaceCell(parts, "B4", new XElement(SheetNs + "c", new XAttribute("r", "B4"), new XAttribute("t", "s"), new XElement(SheetNs + "v", "0")));
        Assert.Equal("공유 물품", Assert.Single(Parse(parts).Rows).Draft.CargoName);
        ReplaceCell(parts, "B4", new XElement(SheetNs + "c", new XAttribute("r", "B4"), new XAttribute("t", "s"), new XElement(SheetNs + "v", "9")));
        AssertRejected(Parse(parts), "InvalidSharedString");
    }

    [Fact]
    public void 수식은결과캐시가있어도거절한다()
    {
        var parts = Parts();
        ReplaceCell(parts, "C4", new XElement(SheetNs + "c", new XAttribute("r", "C4"), new XElement(SheetNs + "f", "1+1"), new XElement(SheetNs + "v", "2")));
        AssertRejected(Parse(parts), "FormulaNotAllowed");
    }

    [Fact]
    public void 외부연결과매크로와경로순회를거절한다()
    {
        var external = Parts();
        external["xl/_rels/sheet1.xml.rels"] = "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='x' Type='link' TargetMode='External' Target='https://example.invalid/private'/></Relationships>";
        AssertRejected(Parse(external), "ExternalLinkNotAllowed");
        var macro = Parts(); macro["xl/vbaProject.bin"] = "private";
        AssertRejected(Parse(macro), "UnsupportedPart");
        var traversal = Parts(); traversal["../private.xml"] = "<a/>";
        AssertRejected(Parse(traversal), "UnsafeArchivePath");
    }

    [Fact]
    public void 중복ZIP구성과중복셀출처를거절한다()
    {
        AssertRejected(생활배송엑셀초안Service.Parse(Zip(Parts(), duplicatePath: "xl/workbook.xml")), "DuplicatePart");
        var parts = Parts();
        var sheet = XDocument.Parse(parts["xl/worksheets/sheet1.xml"]);
        var row = sheet.Descendants(SheetNs + "row").Single(r => (string?)r.Attribute("r") == "4");
        row.Add(new XElement(row.Elements(SheetNs + "c").First()));
        parts["xl/worksheets/sheet1.xml"] = sheet.ToString();
        AssertRejected(Parse(parts), "InvalidCell");
    }

    [Theory]
    [InlineData("[Content_Types].xml", "Override")]
    [InlineData("xl/_rels/workbook.xml.rels", "Relationship")]
    public void 중복구성선택오류는원문없이안전한응답으로반환한다(string path, string elementName)
    {
        var parts = Parts();
        var document = XDocument.Parse(parts[path]);
        document.Root!.Add(new XElement(document.Root.Elements().Single(x => x.Name.LocalName == elementName)));
        parts[path] = document.ToString();
        AssertRejected(Parse(parts), "InvalidFile");
    }

    [Fact]
    public void OPC기본contenttype을지원하되Override우선과중복기본정의를검증한다()
    {
        const string type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
        const string ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        var parts = Parts();
        parts["[Content_Types].xml"] = $"<Types xmlns='{ns}'><Default Extension='xml' ContentType='{type}'/></Types>";
        Assert.Empty(Parse(parts).Errors);
        parts["[Content_Types].xml"] = $"<Types xmlns='{ns}'><Default Extension='xml' ContentType='application/xml'/><Override PartName='/xl/workbook.xml' ContentType='{type}'/></Types>";
        Assert.Empty(Parse(parts).Errors);
        parts["[Content_Types].xml"] = $"<Types xmlns='{ns}'><Default Extension='xml' ContentType='{type}'/><Override PartName='/xl/workbook.xml' ContentType='application/xml'/></Types>";
        AssertRejected(Parse(parts), "UnsupportedWorkbook");
        parts["[Content_Types].xml"] = $"<Types xmlns='{ns}'><Default Extension='xml' ContentType='{type}'/><Default Extension='XML' ContentType='{type}'/></Types>";
        AssertRejected(Parse(parts), "DuplicateContentType");
    }

    [Theory]
    [InlineData("WrongRoot", "InvalidSheetStructure")]
    [InlineData("WrongNamespace", "InvalidSheetStructure")]
    [InlineData("MissingSheetData", "InvalidSheetStructure")]
    [InlineData("NestedCell", "InvalidSheetStructure")]
    [InlineData("MismatchedRow", "InvalidCellReference")]
    [InlineData("DuplicateRow", "InvalidRowReference")]
    public void 표준시트구조와셀행출처일치를확인한다(string variation, string code)
    {
        var parts = Parts();
        var sheet = XDocument.Parse(parts["xl/worksheets/sheet1.xml"]);
        var data = sheet.Root!.Element(SheetNs + "sheetData")!;
        var row = data.Elements(SheetNs + "row").Single(element => (string?)element.Attribute("r") == "4");
        switch (variation)
        {
            case "WrongRoot": sheet.Root.Name = SheetNs + "other"; break;
            case "WrongNamespace":
                sheet.Root.Attribute("xmlns")?.Remove();
                sheet.Root.Name = XNamespace.Get("urn:unsupported") + "worksheet"; break;
            case "MissingSheetData": data.Remove(); break;
            case "NestedCell":
                sheet.Root.Add(new XElement(SheetNs + "extLst", new XElement(SheetNs + "c", new XAttribute("r", "T5")))); break;
            case "MismatchedRow": row.SetAttributeValue("r", "5"); break;
            case "DuplicateRow": data.Add(new XElement(row)); break;
        }
        parts["xl/worksheets/sheet1.xml"] = sheet.ToString();
        AssertRejected(Parse(parts), code);
    }

    [Theory]
    [InlineData("WorkbookWrongRoot", "InvalidWorkbookStructure")]
    [InlineData("WorkbookWrongNamespace", "InvalidWorkbookStructure")]
    [InlineData("NestedSheet", "InvalidWorkbookStructure")]
    [InlineData("RelationshipsWrongRoot", "InvalidWorkbookRelationship")]
    [InlineData("RelationshipsWrongNamespace", "InvalidWorkbookRelationship")]
    [InlineData("NestedRelationship", "InvalidWorkbookRelationship")]
    public void 통합문서와시트연결의표준구조를확인한다(string variation, string code)
    {
        var parts = Parts();
        var path = variation.StartsWith("Workbook", StringComparison.Ordinal) || variation == "NestedSheet"
            ? "xl/workbook.xml" : "xl/_rels/workbook.xml.rels";
        var document = XDocument.Parse(parts[path]);
        if (variation.EndsWith("WrongRoot", StringComparison.Ordinal))
            document.Root!.Name = document.Root.Name.Namespace + "other";
        else if (variation.EndsWith("WrongNamespace", StringComparison.Ordinal))
        {
            document.Root!.Attribute("xmlns")?.Remove();
            document.Root.Name = XNamespace.Get("urn:unsupported") + document.Root.Name.LocalName;
        }
        else
        {
            var child = document.Descendants().Single(e => e.Name.LocalName == (variation == "NestedSheet" ? "sheet" : "Relationship"));
            child.Remove();
            document.Root!.Add(new XElement(document.Root.Name.Namespace + "extension", child));
        }
        parts[path] = document.ToString();
        AssertRejected(Parse(parts), code);
    }

    [Fact]
    public void DTD와암호화형식과파일크기초과를거절한다()
    {
        var parts = Parts(); parts["private.xml"] = "<!DOCTYPE a [<!ENTITY p SYSTEM 'file:///private'>]><a>&p;</a>";
        AssertRejected(Parse(parts), "InvalidFile");
        AssertRejected(생활배송엑셀초안Service.Parse([0xD0, 0xCF, 0x11, 0xE0]), "InvalidFile");
        AssertRejected(생활배송엑셀초안Service.Parse(new byte[NeighborhoodDeliveryInputSchema.MaximumFileBytes + 1]), "FileTooLarge");
    }

    [Fact]
    public void XML파트와전체압축해제크기를제한한다()
    {
        var part = Parts(); part["private.xml"] = "<a>" + new string(' ', 2 * 1024 * 1024) + "</a>";
        AssertRejected(Parse(part), "PartTooLarge");
        var expanded = Parts();
        for (var index = 0; index < 11; index++) expanded[$"private{index}.xml"] = "<a>" + new string(' ', 2 * 1024 * 1024 - 7) + "</a>";
        AssertRejected(Parse(expanded), "ExpandedFileTooLarge");
    }

    [Fact]
    public void ZIP항목수와공유문자열수와셀수를제한한다()
    {
        var entries = Parts();
        for (var index = 0; index < 256; index++) entries[$"private{index}.xml"] = "<a/>";
        AssertRejected(Parse(entries), "ArchiveLimit");
        var shared = Parts(); shared["xl/sharedStrings.xml"] = $"<sst xmlns='{SheetNs}'>" + string.Concat(Enumerable.Repeat("<si><t>x</t></si>", 2049)) + "</sst>";
        AssertRejected(Parse(shared), "SharedStringLimit");
        var cells = Parts(); cells["private.xml"] = $"<worksheet xmlns='{SheetNs}'>" + string.Concat(Enumerable.Repeat("<c r='A1'/>", 4097)) + "</worksheet>";
        AssertRejected(Parse(cells), "CellLimit");
    }

    [Fact]
    public void 취소를중단상태로전파한다()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => 생활배송엑셀초안Service.Parse(Zip(Parts()), cancellation.Token));
    }

    [Fact]
    public async Task 인증과현재사용자동의를파싱보다먼저확인한다()
    {
        var current = new Current { UserId = null };
        var consent = new Consent();
        var service = new 생활배송엑셀초안Service(current, consent);
        var request = new NeighborhoodDeliveryInputPreviewRequest { ContentBase64 = "invalid", PrivacyConsentEvidenceId = ConsentId };
        Assert.True((await service.미리보기Async(request)).IsFailed);
        Assert.Equal(0, consent.Reads);
        current.UserId = "owner";
        consent.Allowed = false;
        Assert.True((await service.미리보기Async(request)).IsFailed);
        Assert.Equal(1, consent.Reads);
        Assert.Equal("owner", consent.Actor);
        Assert.Equal(신청개인정보업무Codes.운송대행, consent.Work);
        Assert.Equal(NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, consent.Source);
        consent.Allowed = true;
        consent.AfterRead = () => current.UserId = null;
        Assert.True((await service.미리보기Async(request)).IsFailed);
    }

    [Fact]
    public async Task 정상동의후미리보기는원장접수나동의기록을수행하지않는다()
    {
        var consent = new Consent();
        var result = await new 생활배송엑셀초안Service(new Current(), consent).미리보기Async(new()
        {
            FileName = "input.xlsx", PrivacyConsentEvidenceId = ConsentId, ContentBase64 = Convert.ToBase64String(Zip(Parts()))
        });
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Errors);
        Assert.Single(result.Value.Rows);
        Assert.Equal(1, consent.Reads);
    }

    [Fact]
    public void 실제배포양식의빈서식행을읽고실제리소스바이트를반환한다()
    {
        var service = new 생활배송엑셀초안Service(new Current(), new Consent());
        var template = service.양식조회();
        var bytes = Convert.FromBase64String(template.ContentBase64);
        using var stream = typeof(생활배송엑셀초안Service).Assembly.GetManifestResourceStream(NeighborhoodDeliveryInputSchema.TemplateResourceName)!;
        using var buffer = new MemoryStream(); stream.CopyTo(buffer);
        Assert.Equal(buffer.ToArray(), bytes);
        var parsed = 생활배송엑셀초안Service.Parse(bytes);
        Assert.Empty(parsed.Rows);
        Assert.DoesNotContain(parsed.Errors, x => x.Code != "NoDataRows");
        var controller = new 생활배송의뢰Controller(null!, null!, null!, null!, excelInput: service)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<NeighborhoodDeliveryInputTemplateResponse>(Assert.IsType<OkObjectResult>(controller.입력양식()).Value);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.NotEmpty(typeof(생활배송의뢰Controller).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
    }

    private static NeighborhoodDeliveryInputPreviewResponse Parse(Dictionary<string, string> parts) => 생활배송엑셀초안Service.Parse(Zip(parts));
    private static void AssertRejected(NeighborhoodDeliveryInputPreviewResponse result, string code)
    { Assert.Empty(result.Rows); Assert.Contains(result.Errors, x => x.Code == code); }

    private static Dictionary<string, string> Parts(Dictionary<string, string>? changes = null)
    {
        var values = new Dictionary<string, string>
        {
            ["A1"] = NeighborhoodDeliveryInputSchema.Version, ["A4"] = RowId.ToString(), ["B4"] = "합성 상자", ["C4"] = "2", ["D4"] = "3.5",
            ["E4"] = "합성 픽업 주소", ["F4"] = "1층", ["G4"] = "합성 담당자", ["H4"] = "010-1234-5678",
            ["I4"] = "합성 전달 주소", ["J4"] = "2층", ["K4"] = "합성 수령인", ["L4"] = "01098765432",
            ["M4"] = "2030-01-02 10:00", ["N4"] = "2030-01-02 11:00", ["O4"] = "Small", ["R4"] = "합성 요청", ["S4"] = "오토바이"
        };
        for (var column = 0; column < NeighborhoodDeliveryInputSchema.Headers.Count; column++) values[((char)('A' + column)) + "3"] = NeighborhoodDeliveryInputSchema.Headers[column];
        if (changes is not null) foreach (var pair in changes) values[pair.Key] = pair.Value;
        var rows = values.GroupBy(pair => int.Parse(new string(pair.Key.SkipWhile(char.IsLetter).ToArray()))).OrderBy(group => group.Key)
            .Select(group => new XElement(SheetNs + "row", new XAttribute("r", group.Key), group.Select(pair =>
                new XElement(SheetNs + "c", new XAttribute("r", pair.Key), new XAttribute("t", "inlineStr"),
                    new XElement(SheetNs + "is", new XElement(SheetNs + "t", pair.Value))))));
        return new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/></Types>",
            ["xl/workbook.xml"] = $"<workbook xmlns='{SheetNs}' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='배송입력' sheetId='1' r:id='rId1'/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/></Relationships>",
            ["xl/worksheets/sheet1.xml"] = new XDocument(new XElement(SheetNs + "worksheet", new XElement(SheetNs + "sheetData", rows))).ToString()
        };
    }
    private static void ReplaceCell(Dictionary<string, string> parts, string reference, XElement replacement)
    {
        var sheet = XDocument.Parse(parts["xl/worksheets/sheet1.xml"]);
        sheet.Descendants(SheetNs + "c").Single(cell => (string?)cell.Attribute("r") == reference).ReplaceWith(replacement);
        parts["xl/worksheets/sheet1.xml"] = sheet.ToString();
    }
    private static void AddCopyRow(Dictionary<string, string> parts, int original, int rowNumber)
    {
        var sheet = XDocument.Parse(parts["xl/worksheets/sheet1.xml"]);
        var copy = new XElement(sheet.Descendants(SheetNs + "row").Single(row => (string?)row.Attribute("r") == original.ToString()));
        copy.SetAttributeValue("r", rowNumber);
        foreach (var cell in copy.Elements(SheetNs + "c")) cell.SetAttributeValue("r", new string(((string)cell.Attribute("r")!).TakeWhile(char.IsLetter).ToArray()) + rowNumber);
        sheet.Root!.Element(SheetNs + "sheetData")!.Add(copy);
        parts["xl/worksheets/sheet1.xml"] = sheet.ToString();
    }
    private static byte[] Zip(Dictionary<string, string> parts, string? duplicatePath = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var pair in parts) Write(pair.Key, pair.Value);
            if (duplicatePath is not null) Write(duplicatePath, parts[duplicatePath]);
            void Write(string path, string value)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(value);
            }
        }
        return stream.ToArray();
    }
    private sealed class Current : ICurrentUserAccessor
    { public string? UserId { get; set; } = "owner"; public string? Role => "일반회원"; }
    private sealed class Consent : I신청개인정보동의증적Service
    {
        public bool Allowed { get; set; } = true;
        public int Reads { get; private set; }
        public string? Actor { get; private set; }
        public string? Work { get; private set; }
        public string? Source { get; private set; }
        public Action? AfterRead { get; set; }
        public Task 유효한동의요구Async(Guid? evidenceId, string workCode, string sourceCode, string userId, CancellationToken cancellationToken = default)
        {
            Reads++; Actor = userId; Work = workCode; Source = sourceCode;
            if (!Allowed || evidenceId != ConsentId) throw new InvalidOperationException("private error must not leak");
            AfterRead?.Invoke(); return Task.CompletedTask;
        }
        public Task<신청개인정보동의증적Response> 동의기록Async(신청개인정보동의기록Request request, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<신청개인정보동의증적Response?> 내증적조회Async(Guid evidenceId, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<신청개인정보동의증적Response> 철회Async(Guid evidenceId, 신청개인정보동의철회Request request, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
