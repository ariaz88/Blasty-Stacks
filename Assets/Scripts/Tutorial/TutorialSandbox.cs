using System.Collections;
using UnityEngine;

/// <summary>
/// Turns the scene it sits in into a throwaway playground for the onboarding.
///
/// Two things, and both matter:
///
///  1) IT STOPS THE GAME WRITING TO DISK. SaveSystem.SuppressWrites is switched on
///     before anything else runs, so every upgrade, every coin spent and every
///     tutorial "already seen" flag stays in memory and dies with Play mode. The
///     player's real save is never opened for writing.
///
///  2) IT HANDS OUT FAKE RESOURCES. Coins, gems and Hero XP are set to a big round
///     number so the Upgrade button is always affordable and the flow can be
///     watched end to end without grinding for it.
///
/// This is NOT the save/load toggle that was deleted on 2026-09-22. That one was a
/// user-facing switch that could silently disable saving in the real game; this is
/// a per-scene test harness that restores the flag and drops the poisoned cache in
/// OnDestroy, and it refuses to arm itself in a player build.
///
/// Put it on one GameObject in the test scene, alongside TutorialAutoAnchors and a
/// TutorialTrigger.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class TutorialSandbox : MonoBehaviour
{
    [Header("Fake wallet")]
    [SerializeField] private int coins = 999999;
    [SerializeField] private int gems = 99999;
    [SerializeField] private int heroXp = 9999;

    [Header("Tutorial state")]
    [Tooltip("Forget every tutorial flag on entry (in memory only), so the chain " +
             "replays every time you press Play on this scene.")]
    [SerializeField] private bool forgetTutorialsOnEntry = true;

    [Tooltip("Seconds to keep re-applying the fake wallet. CurrencyManager is loaded " +
             "from the save during boot, which can land AFTER this scene's Awake.")]
    [SerializeField] private float topUpWindowSeconds = 2f;

    private bool _armed;

    private void Awake()
    {
#if !UNITY_EDITOR
        // A sandbox that shipped would quietly stop the real game saving. Never.
        Debug.LogWarning("[TutorialSandbox] Ignored outside the Editor.", this);
        return;
#else
        _armed = true;

        SaveSystem.SuppressWrites = true;
        Debug.LogWarning("[TutorialSandbox] Save writes SUPPRESSED for this Play session. " +
                         "Nothing done in this scene touches the real save.", this);

        if (forgetTutorialsOnEntry) SaveSystem.ResetTutorials();
#endif
    }

    private IEnumerator Start()
    {
        if (!_armed) yield break;

        // Boot order is not ours to control: with DirectPlayBootstrap the managers
        // come from StarterScene and this scene is loaded afterwards, but
        // GameStartManager may still be loading currency from the save when we get
        // here. Re-applying for a short window is simpler and more robust than
        // guessing the exact frame.
        float until = Time.unscaledTime + Mathf.Max(0f, topUpWindowSeconds);
        bool everApplied = false;

        while (Time.unscaledTime < until)
        {
            if (ApplyWallet()) everApplied = true;
            yield return null;
        }

        if (!everApplied)
        {
            Debug.LogWarning("[TutorialSandbox] No CurrencyManager appeared - the fake wallet " +
                             "was never applied. Enable Tools/Testing/Play Any Scene Directly so " +
                             "StarterScene boots the managers first.", this);
        }
    }

    private bool ApplyWallet()
    {
        var wallet = CurrencyManager.Instance;
        if (!wallet) return false;

        if (wallet.Coins != coins) wallet.SetCoins(coins);
        if (wallet.Gems != gems) wallet.SetGems(gems);
        if (wallet.HeroXP != heroXp) wallet.SetHeroXP(heroXp);

        return true;
    }

    private void OnDestroy()
    {
        if (!_armed) return;

        SaveSystem.SuppressWrites = false;

        // The in-memory cache is full of fake numbers now. Drop it so the next read
        // comes from disk - otherwise a later save in the same Editor session would
        // write the sandbox's wallet into the player's real progress.
        SaveSystem.ReloadFromDisk();

        Debug.Log("[TutorialSandbox] Save writes restored and the sandbox cache dropped.");
    }
}
