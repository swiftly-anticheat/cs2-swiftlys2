using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using Microsoft.Extensions.Configuration;
using SwiftlyAC.Services.Sanction;
using SwiftlyAC.Services.GameEvents;
using SwiftlyAC.Services.Notifications;
using SwiftlyAC.Services.ConVars;

namespace SwiftlyAC;

[PluginMetadata(Id = "SwiftlyAC", Version = "1.0.0", Name = "SwiftlyAC", Author = "Swiftly Anti-Cheat Development Team", Description = "Swiftly Anti-Cheat")]
public partial class SwiftlyAC : BasePlugin {
    private ServiceProvider? _serviceProvider;

    public SwiftlyAC(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager) {
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<Configuration>("config.jsonc", "Main")
            .Configure(builder => builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true));

        ServiceCollection services = new();

        services.AddSwiftly(Core)
            .AddSingleton<SanctionService>()
            .AddSingleton<GameEventsService>()
            .AddSingleton<NotificationsService>()
            .AddSingleton<ConVarsService>()
            .AddOptionsWithValidateOnStart<Configuration>().BindConfiguration("Main");

        _serviceProvider = services.BuildServiceProvider();

        //////////////////////////////////////////////////////////////////////
        _ = _serviceProvider.GetRequiredService<NotificationsService>();
        _ = _serviceProvider.GetRequiredService<SanctionService>();

        //////////////////////////////////////////////////////////////////////
        _ = _serviceProvider.GetRequiredService<GameEventsService>();
        _ = _serviceProvider.GetRequiredService<ConVarsService>();
    }

    public override void Unload()
    {
        if(_serviceProvider != null)
        {
            _serviceProvider.Dispose();
            _serviceProvider = null;
        }
    }
} 