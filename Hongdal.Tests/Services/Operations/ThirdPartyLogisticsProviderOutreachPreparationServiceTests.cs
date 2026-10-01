using Hongdal.Contracts.Common.Operations;
using Hongdal.Controllers.Admin.Master06;
using Hongdal.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Tests.Services.Operations;

public sealed class ThirdPartyLogisticsProviderOutreachPreparationServiceTests
{
    [Fact]
    public void 발신자준수정보가없으면_열개업체초안을만들되_발송준비상태로표시하지않는다()
    {
        var service = new UnitedStatesThirdPartyLogisticsProviderOutreachPreparationService();

        var response = service.Prepare(
            new PrepareThirdPartyLogisticsProviderOutreachRequest());

        Assert.True(response.Success);
        Assert.Equal(10, response.TotalDraftCount);
        Assert.Equal(0, response.ReadyForManualApprovalCount);
        Assert.Equal(10, response.BlockedDraftCount);
        Assert.Equal(1, response.DirectEmailDraftCount);
        Assert.Equal(9, response.OfficialInquiryFormDraftCount);
        Assert.False(response.AutomaticDispatchEnabled);
        Assert.True(response.RequiresPerRecipientApproval);
        Assert.Contains(
            ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                .PhysicalPostalAddress,
            response.MissingSenderRequirementCodes);
        Assert.Contains(
            ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                .SuppressionListCheckConfirmation,
            response.MissingSenderRequirementCodes);
        Assert.All(response.Items, draft =>
        {
            Assert.Equal(
                ThirdPartyLogisticsProviderOutreachReadinessCodes
                    .MissingSenderRequirements,
                draft.ReadinessCode);
            Assert.False(draft.CanCreateManualEmailDraft);
            Assert.False(draft.CanUseOfficialInquiryForm);
            Assert.False(draft.AutomaticDispatchEnabled);
            Assert.True(draft.RequiresRecipientAddressReverification);
        });

        var phoenix = response.Items.Single(draft =>
            draft.ProviderKey == "phoenix-warehouse");
        Assert.Equal(
            ThirdPartyLogisticsProviderOutreachContactChannelCodes
                .VerifiedPublicBusinessEmail,
            phoenix.ContactChannelCode);
        Assert.Equal("info@phoenix-warehouse.com", phoenix.RecipientEmailAddress);
        Assert.True(phoenix.RecipientEmailVerifiedFromOfficialSource);
        Assert.Contains("phoenix-warehouse.com/contact-us", phoenix.ContactSourceUrl);
    }

    [Fact]
    public void 발신자정보와수동검토확인이있으면_이메일과공식문의양식초안을검토가능하게한다()
    {
        var service = new UnitedStatesThirdPartyLogisticsProviderOutreachPreparationService();
        var request = CompleteRequest(
            "phoenix-warehouse",
            "ups-supply-chain-solutions");

        var response = service.Prepare(request);

        Assert.True(response.Success);
        Assert.Equal(2, response.TotalDraftCount);
        Assert.Equal(2, response.ReadyForManualApprovalCount);
        Assert.Equal(0, response.BlockedDraftCount);
        Assert.Empty(response.MissingSenderRequirementCodes);
        Assert.Empty(response.UnknownProviderKeys);

        var phoenix = response.Items.Single(draft =>
            draft.ProviderKey == "phoenix-warehouse");
        Assert.True(phoenix.CanCreateManualEmailDraft);
        Assert.False(phoenix.CanUseOfficialInquiryForm);

        var ups = response.Items.Single(draft =>
            draft.ProviderKey == "ups-supply-chain-solutions");
        Assert.False(ups.CanCreateManualEmailDraft);
        Assert.True(ups.CanUseOfficialInquiryForm);
        Assert.Empty(ups.RecipientEmailAddress);
        Assert.Contains("ask an expert", ups.ContactSourceTitle, StringComparison.OrdinalIgnoreCase);

        Assert.All(response.Items, draft =>
        {
            Assert.Equal(
                ThirdPartyLogisticsProviderOutreachReadinessCodes
                    .ReadyForManualApproval,
                draft.ReadinessCode);
            Assert.Contains("Hongdal is not presenting itself as a freight broker", draft.PlainTextBody);
            Assert.Contains("non-binding Hongdal ledger role slot", draft.PlainTextBody);
            Assert.Contains("no active shipment or booking", draft.PlainTextBody);
            Assert.Contains("123 Test Street, Seoul, Republic of Korea", draft.PlainTextBody);
            Assert.Contains("reply 'unsubscribe' to outreach@hongdal.test", draft.PlainTextBody);
            Assert.Contains(
                ThirdPartyLogisticsProviderOutreachComplianceRequirementCodes
                    .OfficialRecipientSourceReverified,
                draft.ComplianceRequirementCodes);
            Assert.False(draft.AutomaticDispatchEnabled);
        });
    }

    [Fact]
    public void 요청한업체만선택하고_알수없는업체키를별도로반환한다()
    {
        var service = new UnitedStatesThirdPartyLogisticsProviderOutreachPreparationService();
        var request = CompleteRequest("geodis", "unknown-provider");

        var response = service.Prepare(request);

        Assert.True(response.Success);
        Assert.Single(response.Items);
        Assert.Equal("geodis", response.Items[0].ProviderKey);
        Assert.Equal(["unknown-provider"], response.UnknownProviderKeys);
    }

    [Fact]
    public void 지원하지않는범위와한국배포에서는_준비를거부한다()
    {
        var unitedStatesService =
            new UnitedStatesThirdPartyLogisticsProviderOutreachPreparationService();
        var unsupported = unitedStatesService.Prepare(
            new PrepareThirdPartyLogisticsProviderOutreachRequest
            {
                ScopeCode = "MassEmailCampaign"
            });
        var unavailableService =
            new UnavailableThirdPartyLogisticsProviderOutreachPreparationService(
                new OperatingMarketDeployment(OperatingMarketCodes.Korea));
        var unavailable = unavailableService.Prepare(
            new PrepareThirdPartyLogisticsProviderOutreachRequest());

        Assert.False(unsupported.Success);
        Assert.Equal(
            ThirdPartyLogisticsProviderOutreachErrorCodes.UnsupportedScope,
            unsupported.ErrorCode);
        Assert.False(unavailable.Success);
        Assert.Equal(OperatingMarketCodes.Korea, unavailable.MarketCode);
        Assert.Equal(
            ThirdPartyLogisticsProviderOutreachErrorCodes
                .MarketNotAvailableInDeployment,
            unavailable.ErrorCode);
        Assert.False(unavailable.AutomaticDispatchEnabled);
    }

    [Fact]
    public void 미리보기API는_서버관리자에게만열리고_발송Action을제공하지않는다()
    {
        var controller = typeof(ThirdPartyLogisticsProviderOutreachController);
        var authorization = Assert.Single(
            controller.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());
        var preview = controller.GetMethod(
            nameof(ThirdPartyLogisticsProviderOutreachController.Preview));

        Assert.Equal("서버관리자전용", authorization.Policy);
        Assert.NotNull(preview);
        Assert.Equal(
            "preview",
            Assert.Single(preview!.GetCustomAttributes(typeof(HttpPostAttribute), true)
                    .Cast<HttpPostAttribute>())
                .Template);
        Assert.DoesNotContain(
            controller.GetMethods(),
            method => method.Name.Contains("Send", StringComparison.OrdinalIgnoreCase)
                      || method.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase));
    }

    private static PrepareThirdPartyLogisticsProviderOutreachRequest CompleteRequest(
        params string[] providerKeys)
        => new()
        {
            ProviderKeys = providerKeys,
            SenderName = "Hongdal Operator",
            SenderOrganizationName = "Hongdal",
            SenderRole = "Platform facilitator",
            SenderEmail = "operator@hongdal.test",
            ReplyToEmail = "outreach@hongdal.test",
            SenderOrganizationWebsiteUrl = "https://hongdal.test",
            PhysicalPostalAddress = "123 Test Street, Seoul, Republic of Korea",
            PlannedCargoDescription = "Shelf-stable packaged food for a pilot",
            OriginDescription = "Republic of Korea",
            DestinationDescription = "United States",
            EstimatedVolumeDescription = "One pallet for initial feasibility review",
            TargetTimingDescription = "Exploratory stage; no active shipment or booking",
            ConfirmSenderIdentityAccuracy = true,
            ConfirmPhysicalAddressValidity = true,
            ConfirmSuppressionListChecked = true,
            ConfirmPerRecipientReview = true
        };
}
