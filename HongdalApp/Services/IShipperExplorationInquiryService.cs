using Hongdal.Contracts.Common.Exploration;

namespace HongdalApp.Services;

public interface IShipperExplorationInquiryService
{
    IReadOnlyList<탐색문의목록항목응답> 목록();

    탐색문의상세응답 상세(long campaignId);
}
