using Domain.Configs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace WebUI.Components.Layout;

public partial class NavMenu : ComponentBase, IDisposable
{
    [Inject] private IOptionsMonitor<AppConfiguration>? Config { get; set; }
    
    private IDisposable? configSubscription;
    private List<EventHubConfig>? EventHubsConfigs { get; set; }

    protected override void OnInitialized()
    {
        EventHubsConfigs = Config?.CurrentValue.EventHubsConfigs ?? [];
        configSubscription = Config?.OnChange(x =>
        {
            EventHubsConfigs = x.EventHubsConfigs;
            InvokeAsync(StateHasChanged).ConfigureAwait(false);
        });
    }
    
    public void Dispose()
    {
        Console.WriteLine($"NavMenu disposed - {Guid.NewGuid()}");
        configSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}