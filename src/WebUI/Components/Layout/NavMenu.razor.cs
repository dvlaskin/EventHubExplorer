using Domain.Configs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Options;

namespace WebUI.Components.Layout;

public partial class NavMenu : ComponentBase, IDisposable
{
    [Inject] private IOptionsMonitor<AppConfiguration>? Config { get; set; }
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private IDisposable? configSubscription;
    private List<EventHubConfig>? EventHubsConfigs { get; set; }
    private List<StorageQueueConfig>? StorageQueuesConfigs { get; set; }
    private List<ServiceBusConfig>? ServiceBusConfigs { get; set; }
    private List<RabbitMqConfig>? RabbitMqConfigs { get; set; }

    private bool isEventHubsExpanded = false;
    private bool isStorageQueuesExpanded = false;
    private bool isServiceBusExpanded = false;
    private bool isRabbitMqExpanded = false;

    private bool IsEventHubRouteActive => IsRouteActive("eventhub/");
    private bool IsStorageQueueRouteActive => IsRouteActive("storagequeue/");
    private bool IsServiceBusRouteActive => IsRouteActive("servicebus/");
    private bool IsRabbitMqRouteActive => IsRouteActive("rabbitmq/");

    protected override void OnInitialized()
    {
        EventHubsConfigs = Config?.CurrentValue.EventHubsConfigs ?? [];
        StorageQueuesConfigs = Config?.CurrentValue.StorageQueuesConfigs ?? [];
        ServiceBusConfigs = Config?.CurrentValue.ServiceBusConfigs ?? [];
        RabbitMqConfigs = Config?.CurrentValue.RabbitMqConfigs ?? [];
        EnsureSectionExpandedForCurrentRoute();

        configSubscription = Config?.OnChange(x =>
        {
            EventHubsConfigs = x.EventHubsConfigs;
            StorageQueuesConfigs = x.StorageQueuesConfigs;
            ServiceBusConfigs = x.ServiceBusConfigs;
            RabbitMqConfigs = x.RabbitMqConfigs;
            EnsureSectionExpandedForCurrentRoute();
            InvokeAsync(StateHasChanged).ConfigureAwait(false);
        });

        Navigation.LocationChanged += OnLocationChanged;
    }

    private void ToggleEventHubs() => isEventHubsExpanded = !isEventHubsExpanded;
    private void ToggleStorageQueues() => isStorageQueuesExpanded = !isStorageQueuesExpanded;
    private void ToggleServiceBus() => isServiceBusExpanded = !isServiceBusExpanded;
    private void ToggleRabbitMq() => isRabbitMqExpanded = !isRabbitMqExpanded;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        EnsureSectionExpandedForCurrentRoute();
        _ = InvokeAsync(StateHasChanged);
    }

    private bool IsRouteActive(string routePrefix)
        => Navigation.ToBaseRelativePath(Navigation.Uri)
            .StartsWith(routePrefix, StringComparison.OrdinalIgnoreCase);

    private void EnsureSectionExpandedForCurrentRoute()
    {
        if (IsEventHubRouteActive)
            isEventHubsExpanded = true;

        if (IsStorageQueueRouteActive)
            isStorageQueuesExpanded = true;

        if (IsServiceBusRouteActive)
            isServiceBusExpanded = true;

        if (IsRabbitMqRouteActive)
            isRabbitMqExpanded = true;
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        configSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
