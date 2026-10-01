using Hongdal.Contracts.Common.Community;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.도메인.사용자;

namespace Hongdal.Services.Community;

public sealed class CommunityProfessionalEligibilityService : ICommunityProfessionalEligibilityService
{
    private static readonly string[] RoadFreightBrokerIdentityRoles = ["화물운송주선업자", "FreightBroker", "RoadFreightBroker"];
    private static readonly string[] OceanFreightForwarderIdentityRoles = ["해상운송주선업자", "OceanFreightForwarder"];
    private static readonly string[] AirFreightForwarderIdentityRoles = ["항공화물주선업자", "AirFreightForwarder"];
    private static readonly string[] MultimodalCoordinatorIdentityRoles = ["국제물류주선업자", "복합운송주선업자", "MultimodalCoordinator"];
    private static readonly string[] RoadCarrierIdentityRoles = [역할명.기사, 역할명.용달기사, 역할명.배달기사, "Carrier", "RoadCarrier"];
    private static readonly string[] OceanCarrierIdentityRoles = ["해상운송사", "OceanCarrier"];
    private static readonly string[] AirCarrierIdentityRoles = ["항공운송사", "AirCarrier"];
    private static readonly string[] RailCarrierIdentityRoles = ["철도운송사", "RailCarrier"];
    private static readonly string[] WarehouseIdentityRoles = [역할명.창고관리자, "WarehouseOperator"];
    private static readonly string[] CustomsControlledFacilityIdentityRoles =
        [역할명.보세창고운영자, 역할명.FTZ운영자, "CustomsBondedWarehouseOperator", "ForeignTradeZoneOperator", "CustomsControlledFacilityOperator"];
    private static readonly string[] InBondCarrierIdentityRoles =
        [역할명.보세운송사, "InBondCarrier"];
    private static readonly string[] FulfillmentIdentityRoles =
        [역할명.창고관리자, 역할명.풀필먼트운영자, "WarehouseOperator", "FulfillmentOperator", "ThirdPartyLogisticsProvider"];
    private static readonly string[] ParticipantAddressDeliveryIdentityRoles =
        [역할명.배달기사, 역할명.택배운송사, "ParcelCarrier", "LastMileCarrier"];

    private readonly HongdalContext _db;

    public CommunityProfessionalEligibilityService(HongdalContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> GetVerifiedRoleCodesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var normalizedUserId = userId.Trim();
        var identityRoles = await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == normalizedUserId && role.Name != null
                select role.Name!)
            .ToListAsync(cancellationToken);
        var participant = await _db.홍달참여자
            .AsNoTracking()
            .Where(item => item.Id == normalizedUserId)
            .Select(item => new { item.활성화여부 })
            .SingleOrDefaultAsync(cancellationToken);
        var participantRoles = participant?.활성화여부 == true
            ? await _db.홍달참여자역할
                .AsNoTracking()
                .Where(role => role.참여자Id == normalizedUserId && role.활성화여부)
                .Select(role => role.역할유형)
                .ToListAsync(cancellationToken)
            : [];

        var verifiedRoles = new List<string>();
        if (participantRoles.Contains(홍달역할유형.기사)
            || HasAnyRole(identityRoles, RoadCarrierIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.RoadCarrier);
        }

        if (HasAnyRole(identityRoles, OceanCarrierIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.OceanCarrier);
        }

        if (HasAnyRole(identityRoles, AirCarrierIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.AirCarrier);
        }

        if (HasAnyRole(identityRoles, RailCarrierIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.RailCarrier);
        }

        if (participantRoles.Contains(홍달역할유형.창고관리자)
            || HasAnyRole(identityRoles, WarehouseIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.WarehouseOperator);
        }

        if (HasAnyRole(identityRoles, CustomsControlledFacilityIdentityRoles))
        {
            verifiedRoles.Add(
                CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator);
        }

        if (HasAnyRole(identityRoles, InBondCarrierIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.InBondCarrier);
        }

        if (participantRoles.Contains(홍달역할유형.창고관리자)
            || HasAnyRole(identityRoles, FulfillmentIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.DomesticFulfillmentOperator);
        }

        if (HasAnyRole(identityRoles, ParticipantAddressDeliveryIdentityRoles))
        {
            verifiedRoles.Add(
                CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider);
        }

        if (HasAnyRole(identityRoles, RoadFreightBrokerIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.RoadFreightBroker);
        }

        if (HasAnyRole(identityRoles, OceanFreightForwarderIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.OceanFreightForwarder);
        }

        if (HasAnyRole(identityRoles, AirFreightForwarderIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.AirFreightForwarder);
        }

        if (HasAnyRole(identityRoles, MultimodalCoordinatorIdentityRoles))
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.MultimodalCoordinator);
        }

        var hasCustomsBrokerRole = participantRoles.Contains(홍달역할유형.관세사)
                                   || HasAnyRole(identityRoles, [역할명.관세사, "CustomsBroker"]);
        var customsProfile = hasCustomsBrokerRole
            ? await _db.관세사프로필
                .AsNoTracking()
                .Where(profile => profile.참여자Id == normalizedUserId
                                  && profile.관리자승인여부
                                  && profile.수임가능여부)
                .Select(profile => new { profile.수입전문여부, profile.수출전문여부 })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        if (customsProfile?.수입전문여부 == true)
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.ImportCustomsBroker);
        }

        if (customsProfile?.수출전문여부 == true)
        {
            verifiedRoles.Add(CommunityPostPartyRoleCodes.ExportCustomsBroker);
        }

        return verifiedRoles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool HasAnyRole(IEnumerable<string> actualRoles, IEnumerable<string> expectedRoles)
        => actualRoles.Any(actual => expectedRoles.Contains(actual, StringComparer.OrdinalIgnoreCase));
}
