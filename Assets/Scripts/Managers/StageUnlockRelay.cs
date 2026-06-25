using UnityEngine;

public class StageUnlockRelay : MonoBehaviour
{
    public void OnStageUnlockAnimationEvent()
    {
        PlayerWaveManager pwm = FindObjectOfType<PlayerWaveManager>();
        if (pwm == null) return;

        pwm.OnOneStageUnlockEventFired();
    }
}
