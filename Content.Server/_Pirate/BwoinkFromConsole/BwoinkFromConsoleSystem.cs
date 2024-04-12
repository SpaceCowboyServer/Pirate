using Content.Shared.Administration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Pirate.MakeATraitor;

public sealed class BwoinkFromConsoleSystem : EntitySystem
{
    public static NetUserId SystemUserId { get; } = new NetUserId(Guid.Empty);
    public void SendBwoink(ICommonSession session, string message)
    {
        var bwonkEvent = new SharedBwoinkSystem.BwoinkTextMessage(session.UserId, SystemUserId, message);
        RaiseNetworkEvent(bwonkEvent, session.Channel);
    }
}
