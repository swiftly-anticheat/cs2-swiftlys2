using System.Runtime.CompilerServices;
using SwiftlyAC.Services.Strafe;
using SwiftlyAC.Services.Subtick;
using SwiftlyS2.Shared.Players;

namespace SwiftlyAC;

internal sealed class PlayerData
{
    public readonly SubtickData Subtick = new();
    public readonly StrafeData Strafe = new();
}

internal static class PlayerExtensions
{
    private static readonly ConditionalWeakTable<IPlayer, PlayerData> Table = new();

    extension(IPlayer player)
    {
        public PlayerData AcData => Table.GetValue(player, static _ => new PlayerData());
    }
}
