using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using Microsoft.Extensions.Configuration;
using SwiftlyAC.Services.Sanction;

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
            .AddSingleton<SanctionService>();

        _serviceProvider = services.BuildServiceProvider();

        _ = _serviceProvider.GetRequiredService<SanctionService>();
    }

    public override void Unload() {
    }
} 