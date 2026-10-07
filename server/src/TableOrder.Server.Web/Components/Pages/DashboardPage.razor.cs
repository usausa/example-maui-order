namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

public sealed partial class DashboardPage
{
    private List<TenantEntity> tenants = [];

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required TenantService TenantService { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        tenants = await TenantService.GetAllAsync(CancellationToken.None);
    }
}
