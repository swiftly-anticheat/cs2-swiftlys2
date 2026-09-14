using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace SwiftlyAC.Services.Sanction;

internal class SanctionService
{
    private ISwiftlyCore Core;

    public SanctionService(ISwiftlyCore core, ILogger<SanctionService> logger)
    {
        Core = core;
    }
}