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
    private List<ServiceBusConfig>? ServiceBusConfigs { get; set; }

    protected override void OnInitialized()
    {
        EventHubsConfigs = Config?.CurrentValue.EventHubsConfigs ?? [];
        StorageQueuesConfigs = Config?.CurrentValue.StorageQueuesConfigs ?? [];
        ServiceBusConfigs = Config?.CurrentValue.ServiceBusConfigs ?? [];
        
        configSubscription = Config?.OnChange(x =>
        {
            EventHubsConfigs = x.EventHubsConfigs;
            StorageQueuesConfigs = x.StorageQueuesConfigs;
            ServiceBusConfigs = x.ServiceBusConfigs;
            InvokeAsync(StateHasChanged).ConfigureAwait(false);
        });
    }
    
    public void Dispose()
    {
        configSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
