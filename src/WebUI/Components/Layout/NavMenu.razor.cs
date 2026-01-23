using Domain.Configs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace WebUI.Components.Layout;

public partial class NavMenu : ComponentBase, IDisposable
{
    [Inject] private IOptionsMonitor<AppConfiguration>? Config { get; set; }
    
    private IDisposable? configSubscription;
    private List<EventHubConfig>? EventHubsConfigs { get; set; }
    private List<StorageQueueConfig>? StorageQueuesConfigs { get; set; }

    protected override void OnInitialized()
    {
        EventHubsConfigs = Config?.CurrentValue.EventHubsConfigs ?? [];
        StorageQueuesConfigs = Config?.CurrentValue.StorageQueuesConfigs ?? [];
        configSubscription = Config?.OnChange(x =>
        {
            EventHubsConfigs = x.EventHubsConfigs;
            StorageQueuesConfigs = x.StorageQueuesConfigs;
            InvokeAsync(StateHasChanged).ConfigureAwait(false);
        });
    }
    
    public void Dispose()
    {
        configSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
