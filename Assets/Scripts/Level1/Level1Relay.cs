using UnityEngine;

public sealed class Level1Relay : Trigger
{
    [SerializeField] private Level1Controller level;
    [SerializeField] private int stage = 1;
    public override void onStartTrigger() => level.PowerRelay(stage);
    // Restored access is latched: taking a cube away cannot trap a player beyond a door.
    public override void onEndTrigger() { }
}
