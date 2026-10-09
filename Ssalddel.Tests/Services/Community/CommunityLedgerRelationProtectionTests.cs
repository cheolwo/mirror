using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacyRetention;

namespace Ssalddel.Tests.Services.Community;

public sealed class CommunityLedgerRelationProtectionTests
{
    [Theory]
    [InlineData("detach")]
    [InlineData("role")]
    [InlineData("optional")]
    [InlineData("child-template")]
    [InlineData("relation-kind")]
    [InlineData("root-template")]
    [InlineData("case-id")]
    public void Root_relationship_changes_that_remove_existing_meaning_require_child_protection(string change)
    {
        var current = Root();
        var next = Root();
        switch (change)
        {
            case "detach": next.포함원장목록 = []; break;
            case "role": next.포함원장목록[0].역할 = 주문원장포함역할.배송; break;
            case "optional": next.포함원장목록[0].필수여부 = false; break;
            case "child-template": next.포함원장목록[0].원장템플릿Key = CommunityLedgerTemplateKeys.FoodOrder; break;
            case "relation-kind": next.포함원장목록[0].관계유형 = CommunityLedgerRelationTypes.Reference; break;
            case "root-template": next.원장템플릿Key = CommunityLedgerTemplateKeys.GroupOrder; break;
            case "case-id": next.포함원장목록[0].원장Id = "CHILD-1"; break;
        }

        Assert.Equal("child-1", Assert.Single(커뮤니티원장관계보호Policy.삭제성변경대상(current, next)).원장Id);
    }

    [Fact]
    public void New_child_ordering_and_required_strengthening_preserve_existing_non_destructive_save()
    {
        var current = Root();
        current.포함원장목록[0].필수여부 = false;
        var next = Root();
        next.포함원장목록[0].표시순서 = 8;
        next.포함원장목록 = [next.포함원장목록[0], Reference("active-child")];

        Assert.Empty(커뮤니티원장관계보호Policy.삭제성변경대상(current, next));
        Assert.Empty(커뮤니티원장관계보호Policy.삭제성변경대상(null, next));
    }

    [Theory]
    [InlineData(CommunityLedgerTemplateKeys.Order)]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder)]
    [InlineData(CommunityLedgerTemplateKeys.SsalddelMart)]
    public void Existing_order_root_templates_receive_the_same_protection(string template)
    {
        var current = Root(); current.원장템플릿Key = template;
        var next = Root(); next.원장템플릿Key = template; next.포함원장목록 = [];
        Assert.Single(커뮤니티원장관계보호Policy.삭제성변경대상(current, next));
    }

    [Theory]
    [InlineData(CommunityLedgerTemplateKeys.GroupOrder)]
    [InlineData(CommunityLedgerTemplateKeys.GroupPurchase)]
    [InlineData(CommunityLedgerTemplateKeys.GroupImport)]
    [InlineData(CommunityLedgerTemplateKeys.GroupExport)]
    public void Other_aggregate_templates_keep_their_existing_write_semantics(string template)
    {
        var current = Root(); current.원장템플릿Key = template;
        var next = Root(); next.원장템플릿Key = template; next.포함원장목록 = [];
        Assert.Empty(커뮤니티원장관계보호Policy.삭제성변경대상(current, next));
    }

    [Theory]
    [InlineData(커뮤니티원장상태.진행중, 커뮤니티원장상태.초안)]
    [InlineData(커뮤니티원장상태.초안, 커뮤니티원장상태.진행중)]
    [InlineData(커뮤니티원장상태.초안, 커뮤니티원장상태.완료)]
    [InlineData(커뮤니티원장상태.초안, "Unknown")]
    public void Generic_save_cannot_replace_current_state_with_draft_to_escape_relationship_guard(string rootState, string childState)
    {
        var root = Root(); root.상태 = rootState;
        var child = new 커뮤니티원장문서 { 원장Id = "child-1", 상태 = childState };
        Assert.Throws<InvalidOperationException>(() => 커뮤니티원장관계보호Policy.잠금검증(root, child));
    }

    [Fact]
    public void Missing_or_privacy_scrubbed_child_is_never_treated_as_a_draft()
    {
        Assert.Throws<InvalidOperationException>(() => 커뮤니티원장관계보호Policy.잠금검증(Root(), null));
        var child = new 커뮤니티원장문서 { 원장Id = "child-1" };
        child.확장속성[배송원장개인정보파기Service.Marker] = "FoodOrder:private-source";
        Assert.Equal("PrivacyDeletionRestoreBlocked", Assert.Throws<InvalidOperationException>(
            () => 커뮤니티원장관계보호Policy.잠금검증(Root(), child)).Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resetting_current_state_to_draft_does_not_erase_the_recorded_work_fact(bool rootProgressed)
    {
        var root = Root();
        var child = new 커뮤니티원장문서 { 원장Id = "child-1" };
        var progressed = rootProgressed ? root : child;
        progressed.상태이력 = [new() { 상태 = 커뮤니티원장상태.진행중 }, new() { 상태 = 커뮤니티원장상태.초안 }];
        Assert.Throws<InvalidOperationException>(() => 커뮤니티원장관계보호Policy.잠금검증(root, child));
        Assert.Equal(커뮤니티원장상태.초안, progressed.상태);
        Assert.Equal(2, progressed.상태이력.Count);
    }

    [Fact]
    public void Storage_fence_is_not_an_exposed_business_or_projection_revision()
    {
        var document = Root();
        document.Revision = 7; document.투영완료Revision = 6; document.관계편집Revision = 41;
        var dto = 커뮤니티원장문서읽기Mapper.ToDto(document);
        Assert.Equal(7, dto.Revision);
        Assert.Equal(6, dto.투영완료Revision);
        Assert.DoesNotContain("관계편집Revision", JsonSerializer.Serialize(dto), StringComparison.Ordinal);
    }

    private static 커뮤니티원장문서 Root() => new()
    {
        원장Id = "root-1", 원장템플릿Key = CommunityLedgerTemplateKeys.Order,
        포함원장목록 = [Reference("child-1")]
    };

    private static 커뮤니티포함원장참조문서 Reference(string id) => new()
    {
        원장Id = id, 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
        역할 = 주문원장포함역할.운송, 필수여부 = true
    };
}
