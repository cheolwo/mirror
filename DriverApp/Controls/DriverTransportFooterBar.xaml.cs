using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Common.Drivers;

namespace DriverApp.Controls;

public partial class DriverTransportFooterBar : ContentView
{
    private bool _isExpanded;

    public event EventHandler? OpenMenuRequested;

    public event EventHandler? OpenTransportRequested;

    public DriverTransportFooterBar()
    {
        InitializeComponent();
    }

    public void ShowTransport(기사운송샘플항목? transport)
    {
        if (transport is null)
        {
            TitleLabel.Text = "운송요약";
            SummaryLabel.Text = "진행 중인 운송이 없습니다.";
            CollapsedPickupLabel.Text = "상차: -";
            CollapsedDropoffLabel.Text = "하차: -";
            CollapsedFareLabel.Text = "운임: -";
            NextActionLabel.Text = "다음 행동: 추천 목록에서 운송 선택";
            SetDetails("-", "-", "-", "-", "-", "-", "추천 목록에서 운송을 선택해 주세요.");
            return;
        }

        TitleLabel.Text = $"{transport.화물종류} 운송";
        SummaryLabel.Text = $"{transport.픽업지} → {transport.하차지}";
        NextActionLabel.Text = $"다음 행동: {transport.다음행동}";
        CollapsedPickupLabel.Text = $"상차: {transport.픽업지}";
        CollapsedDropoffLabel.Text = $"하차: {transport.하차지}";
        CollapsedFareLabel.Text = transport.예상수익.HasValue ? $"운임: {transport.운임표시}" : transport.운임표시;

        SetDetails(
            $"상차지: {transport.픽업지}",
            $"하차지: {transport.하차지}",
            $"화물: {transport.화물종류}",
            $"거리: {transport.운송거리표시} · 예정 {transport.예정시각:HH:mm}",
            $"결제/운임: {transport.운임표시}",
            $"전달받는 자: {transport.하차지} 담당자",
            $"요청사항: {transport.다음행동}");
    }

    public void ShowMarker(DriverMapMarkerItem marker)
    {
        TitleLabel.Text = marker.Title;
        SummaryLabel.Text = marker.Summary;
        NextActionLabel.Text = "다음 행동: 추천 상세 확인";
        CollapsedPickupLabel.Text = $"상차: {marker.PickupAddress}";
        CollapsedDropoffLabel.Text = string.IsNullOrWhiteSpace(marker.DropoffAddress)
            ? "하차: 추천 상세에서 확인"
            : $"하차: {marker.DropoffAddress}";
        CollapsedFareLabel.Text = $"의뢰: {marker.RequestId}";

        SetDetails(
            $"상차지: {marker.PickupAddress}",
            string.IsNullOrWhiteSpace(marker.DropoffAddress)
                ? "하차지: 추천 상세에서 확인"
                : $"하차지: {marker.DropoffAddress}",
            $"화물: {marker.Title}",
            $"운송정보: {marker.Summary}",
            $"결제/운임: 추천 상세에서 확인",
            "전달받는 자: 배차 후 표시",
            $"요청사항: 의뢰 {marker.RequestId} 상세 확인 필요");
    }

    private void SetDetails(
        string pickup,
        string dropoff,
        string cargo,
        string distance,
        string fare,
        string receiver,
        string request)
    {
        PickupLabel.Text = pickup;
        DropoffLabel.Text = dropoff;
        CargoLabel.Text = cargo;
        DistanceLabel.Text = distance;
        FareLabel.Text = fare;
        ReceiverLabel.Text = receiver;
        RequestLabel.Text = request;
    }

    private void OnToggleClicked(object? sender, EventArgs e)
    {
        _isExpanded = !_isExpanded;
        ExpandedDetailsLayout.IsVisible = _isExpanded;
        ToggleButton.Text = _isExpanded ? "⌄" : "⌃";
    }

    private void OnOpenMenuClicked(object? sender, EventArgs e)
    {
        OpenMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenTransportClicked(object? sender, EventArgs e)
    {
        OpenTransportRequested?.Invoke(this, EventArgs.Empty);
    }
}
