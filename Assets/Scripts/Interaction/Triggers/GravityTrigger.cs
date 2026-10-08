using UnityEngine;

public class GravityTrigger : Trigger
{
    public override void onStartTrigger()
    {
        GlobalState.get().gravity = 0.0f;
    }

    public override void onEndTrigger()
    {
        GlobalState.get().gravity = GlobalState.StationGravityStrength;
    }
}
