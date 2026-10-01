using CommunityToolkit.Mvvm.ComponentModel;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using MudBlazor;
using Color = MudBlazor.Color;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사상차인수증ViewModel : ObservableObject
{
    public const string 문서사진방식 = "문서사진";
    public const string 직접서명방식 = "직접서명";
    public const string 서명생략방식 = "서명생략";

    private 기사운송샘플항목? _운송;

    public 기사상차인수증ViewModel()
    {
        증빙방식 = 문서사진방식;
    }

    [ObservableProperty]
    public partial string? 인수자명 { get; set; }

    [ObservableProperty]
    public partial string? 인수자소속 { get; set; }

    [ObservableProperty]
    public partial string? 인수자서명 { get; set; }

    [ObservableProperty]
    public partial string? 기사서명 { get; set; }

    [ObservableProperty]
    public partial bool 인수증확인 { get; set; }

    [ObservableProperty]
    public partial bool 문서사진확인 { get; set; }

    [ObservableProperty]
    public partial string? 서명생략사유 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(문서사진선택))]
    [NotifyPropertyChangedFor(nameof(직접서명선택))]
    [NotifyPropertyChangedFor(nameof(서명생략선택))]
    [NotifyPropertyChangedFor(nameof(직접서명완료))]
    [NotifyPropertyChangedFor(nameof(서명생략완료))]
    public partial string 증빙방식 { get; set; }

    public bool 필요
        => _운송?.인수증필요 == true
           || (_운송?.결제방식.Contains("인수증", StringComparison.OrdinalIgnoreCase) ?? false);

    public bool 서명필수 => 필요 && _운송?.인수증서명필수 == true;
    public bool 문서사진선택 => string.Equals(증빙방식, 문서사진방식, StringComparison.Ordinal);
    public bool 직접서명선택 => string.Equals(증빙방식, 직접서명방식, StringComparison.Ordinal);
    public bool 서명생략선택 => string.Equals(증빙방식, 서명생략방식, StringComparison.Ordinal) && !서명필수;

    public bool 직접서명완료
        => 직접서명선택
           && 인수증확인
           && !string.IsNullOrWhiteSpace(인수자명)
           && !string.IsNullOrWhiteSpace(인수자서명)
           && !string.IsNullOrWhiteSpace(기사서명);

    public bool 서명생략완료 => 필요 && 서명생략선택;

    public string 결제증빙라벨
        => _운송 is null
            ? "-"
            : string.IsNullOrWhiteSpace(_운송.결제방식)
                ? 필요 ? "인수증" : "확인 필요"
                : _운송.결제방식;

    public string 서명칩라벨 => 서명필수 ? "서명 필수" : "서명 선택";
    public Color 서명칩색 => 서명필수 ? Color.Warning : Color.Info;

    public void 운송설정(기사운송샘플항목? transport)
    {
        _운송 = transport;
        초기화입력();
        OnPropertyChanged(nameof(필요));
        OnPropertyChanged(nameof(서명필수));
        OnPropertyChanged(nameof(결제증빙라벨));
        OnPropertyChanged(nameof(서명칩라벨));
        OnPropertyChanged(nameof(서명칩색));
    }

    public bool 완료(bool hasPhoto)
        => !필요
           || (문서사진선택 && 문서사진확인 && hasPhoto)
           || 직접서명완료
           || 서명생략완료;

    public DriverTransportPickupReceiptEvidence? 증빙생성(bool hasPhoto)
    {
        if (!필요)
        {
            return null;
        }

        if (직접서명완료)
        {
            return new DriverTransportPickupReceiptEvidence(
                EvidenceMethod: 직접서명방식,
                Signed: true,
                SignatureOmitted: false,
                SignatureOmissionReason: null,
                RecipientName: 인수자명?.Trim(),
                RecipientOrganization: 인수자소속?.Trim(),
                RecipientSignature: 인수자서명?.Trim(),
                DriverSignature: 기사서명?.Trim());
        }

        if (문서사진선택 && 문서사진확인 && hasPhoto)
        {
            return new DriverTransportPickupReceiptEvidence(
                EvidenceMethod: 문서사진방식,
                Signed: true,
                SignatureOmitted: false,
                SignatureOmissionReason: null,
                RecipientName: null,
                RecipientOrganization: null,
                RecipientSignature: null,
                DriverSignature: null);
        }

        return new DriverTransportPickupReceiptEvidence(
            EvidenceMethod: 서명생략방식,
            Signed: false,
            SignatureOmitted: true,
            SignatureOmissionReason: string.IsNullOrWhiteSpace(서명생략사유)
                ? "현장 합의에 따라 상차 인수 서명 없이 진행"
                : 서명생략사유.Trim(),
            RecipientName: null,
            RecipientOrganization: null,
            RecipientSignature: null,
            DriverSignature: null);
    }

    private void 초기화입력()
    {
        인수자명 = null;
        인수자소속 = null;
        인수자서명 = null;
        기사서명 = null;
        인수증확인 = false;
        문서사진확인 = false;
        서명생략사유 = null;
        증빙방식 = 문서사진방식;
    }
}
