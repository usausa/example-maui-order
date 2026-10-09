namespace TableOrder.Server.Web.Application.Context;

using System.Security.Claims;

using TableOrder.Server.Core.Models.Enums;
using TableOrder.Server.Web.Application.Account;
using TableOrder.Server.Web.Application.Authentication;

public sealed class StoreSelectionTests
{
    private static readonly Guid OwnTenantId = Guid.CreateVersion7();

    private static readonly Guid OtherTenantId = Guid.CreateVersion7();

    private static readonly Guid AssignedStoreId = Guid.CreateVersion7();

    private static readonly Guid OtherStoreId = Guid.CreateVersion7();

    // 運営者はどのテナントの店舗も選べる
    [Fact]
    public void OperatorSelectsAnyTenant()
    {
        // Arrange
        var selection = new StoreSelection(Scope(AdminRole.Operator, null));

        // Act
        selection.Select(OtherTenantId, OtherStoreId);

        // Assert
        Assert.Equal(OtherTenantId, selection.TenantId);
        Assert.Equal(OtherStoreId, selection.StoreId);
    }

    // テナントの管理者は自分のテナントのどの店舗も選べ、ほかのテナントは選べない
    [Fact]
    public void TenantAdminSelectsOnlyOwnTenant()
    {
        // Arrange
        var selection = new StoreSelection(Scope(AdminRole.TenantAdmin, OwnTenantId));

        // Act / Assert: 自分のテナントの店舗
        selection.Select(OwnTenantId, OtherStoreId);
        Assert.Equal(OwnTenantId, selection.TenantId);
        Assert.Equal(OtherStoreId, selection.StoreId);

        // Act / Assert: ほかのテナントは選ばない
        selection.Select(OtherTenantId, OtherStoreId);
        Assert.Null(selection.TenantId);
        Assert.Null(selection.StoreId);
    }

    // 店舗の担当は受け持つ店舗だけを選べる
    [Fact]
    public void StoreStaffSelectsOnlyAssignedStores()
    {
        // Arrange
        var selection = new StoreSelection(Scope(AdminRole.StoreStaff, OwnTenantId, AssignedStoreId));

        // Act / Assert: 受け持たない店舗は選ばない
        selection.Select(OwnTenantId, OtherStoreId);
        Assert.Equal(OwnTenantId, selection.TenantId);
        Assert.Null(selection.StoreId);

        // Act / Assert: 受け持つ店舗
        selection.Select(OwnTenantId, AssignedStoreId);
        Assert.Equal(AssignedStoreId, selection.StoreId);
    }

    // サインインのクレームがなければ (サインインしていない)、何も選ばない
    [Fact]
    public void NothingIsSelectedWithoutClaims()
    {
        // Arrange
        var scope = new AdminScope();
        scope.Initialize(new ClaimsPrincipal(new ClaimsIdentity()));
        var selection = new StoreSelection(scope);

        // Act
        selection.Select(OwnTenantId, AssignedStoreId);

        // Assert
        Assert.False(scope.IsInitialized);
        Assert.Null(selection.TenantId);
        Assert.Null(selection.StoreId);
    }

    private static AdminScope Scope(AdminRole role, Guid? tenantId, params Guid[] storeIds)
    {
        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, Guid.CreateVersion7().ToString()),
            new(AdminClaimNames.Role, role.ToString())
        };
        if (tenantId is { } id)
        {
            claims.Add(new Claim(ClaimNames.TenantId, id.ToString()));
        }

        claims.AddRange(storeIds.Select(static x => new Claim(AdminClaimNames.StoreId, x.ToString())));

        var scope = new AdminScope();
        scope.Initialize(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
        return scope;
    }
}
