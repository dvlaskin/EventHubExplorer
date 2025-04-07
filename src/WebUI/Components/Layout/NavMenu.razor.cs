using Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace WebUI.Components.Layout;

public partial class NavMenu : ComponentBase
{
    [Inject] private IOptionsMonitor<AppConfiguration>? Config { get; set; }
    
    private List<EventHubConfig>? EventHubsConfigs { get; set; }

    protected override void OnInitialized()
    {
        EventHubsConfigs = Config?.CurrentValue.EventHubsConfigs ?? [];
        Config?.OnChange(x =>
        {
            EventHubsConfigs = x.EventHubsConfigs;
            InvokeAsync(StateHasChanged);
        });
    }
}