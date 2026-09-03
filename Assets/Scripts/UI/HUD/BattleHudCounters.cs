// BattleHudCounters.cs
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// The two small battle-phase readouts in the top HUD row: the ELAPSED BATTLE
/// TIMER and the ENEMY KILL COUNT.
///
/// One component drives both because they share a lifetime exactly - both start
/// at the same moment, both are meaningless before the battle, and both are
/// reset by the same things. Two separate scripts would have duplicated the
/// start detection and the pause handling for no gain.
///
/// WHERE THIS GOES: on an object that is ALWAYS ACTIVE for the whole scene -
/// the HUD root or the Canvas - NOT on the counter holders themselves. The
/// holders are switched off until the battle begins, and a component on an
/// inactive object gets no Update and would miss the start entirely. The text
/// fields below point INTO those inactive holders, which is fine: serialized
/// references to inactive objects resolve normally.
///
/// VISIBILITY IS NOT THIS SCRIPT'S JOB. Showing the holders is already handled
/// by BattlePhaseTransition - put the two holders in its fadeInAfterMove list
/// and they fade in on the same beat as the rest of the HUD. Duplicating that
/// here would mean two systems fighting over the same SetActive.
/// </summary>
public class BattleHudCounters : MonoBehaviour
{
    [Header("Timer")]
    [Tooltip("The TMP label inside 'Timer  Holder/Counter BG'. Elapsed battle time.")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Tooltip("What goes between minutes and seconds. Both sides are always padded " +
             "to two digits, so 43 seconds reads 00 : 43 and 85 seconds reads 01 : 25.")]
    [SerializeField] private string separator = " : ";

    [Tooltip("ON  = the clock starts when the first enemy actually APPEARS, which is " +
             "what the player reads as the battle beginning.\n" +
             "OFF = it starts the instant BATTLE is pressed, so the camera pan counts " +
             "towards the time.")]
    [SerializeField] private bool startOnFirstEnemy = true;

    [Header("Kill Counter")]
    [Tooltip("The TMP label inside 'Enemy Counter Holder/Counter BG'. Enemies killed.")]
    [SerializeField] private TextMeshProUGUI killText;

    [Tooltip("Optional - the skull icon beside the number. Punched alongside the text " +
             "so the whole badge reacts, not just the digits. Leave empty to skip.")]
    [SerializeField] private RectTransform killIcon;

    [Header("Kill Animation")]
    [Tooltip("How much the number overshoots on a kill. 0.35 = 35% bigger at the peak.")]
    [SerializeField, Min(0f)] private float punchScale = 0.35f;

    [Tooltip("Seconds for the punch to settle back.")]
    [SerializeField, Min(0.01f)] private float punchDuration = 0.35f;

    [Tooltip("The number flashes to this colour on a kill and fades back to its " +
             "authored colour over the punch.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.25f);

    [Tooltip("Vibrato of the punch - higher wobbles more. 6-10 reads as a snappy pop.")]
    [SerializeField, Min(1)] private int punchVibrato = 8;

    /// <summary>Seconds since the battle began. Frozen while GameplayPause is on.</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>Enemies killed this battle.</summary>
    public int Kills { get; private set; }

    private bool running;
    private int lastShownSecond = -1;
    private Color killBaseColor = Color.white;
    private Tween punchTween;
    private Tween colorTween;

    private void Awake()
    {
        // Captured before anything tints it, so the flash always returns to the
        // colour the scene author chose rather than to whatever it faded to.
        if (killText != null) killBaseColor = killText.color;
    }

    private void OnEnable()
    {
        EnemyManager.OnAnyEnemyKilled += HandleEnemyKilled;
        BattleStartController.OnAnyBattleStarted += HandleBattleStarted;

        // A static event survives a domain-reload-free play-mode restart, and the
        // spawner's EnemiesHaveAppeared defaults to TRUE for scenes that never
        // gate their spawns. Resetting here means entering play twice in a row
        // does not start the second run with the first run's numbers.
        ResetCounters();
    }

    private void OnDisable()
    {
        EnemyManager.OnAnyEnemyKilled -= HandleEnemyKilled;
        BattleStartController.OnAnyBattleStarted -= HandleBattleStarted;

        punchTween?.Kill();
        colorTween?.Kill();
    }

    private void Update()
    {
        if (!running)
        {
            // Polled rather than event-driven: "the first enemy is on screen" is a
            // spawner STATE, not a broadcast, and it is the same flag the unit
            // health bars use to decide they may appear. Reading it keeps the
            // timer honest with what the player can actually see.
            if (startOnFirstEnemy && BattleStartController.BattleIsRunning
                                  && EnemySpawner.EnemiesHaveAppeared)
                running = true;

            if (!running) return;
        }

        if (GameplayPause.IsPaused) return;

        ElapsedSeconds += Time.deltaTime;

        // Only touch the label when the displayed second actually changes.
        // TMP rebuilds its mesh on every assignment, so writing the same string
        // 60 times a second is pure waste.
        int whole = Mathf.FloorToInt(ElapsedSeconds);
        if (whole == lastShownSecond) return;

        lastShownSecond = whole;
        RenderTime(whole);
    }

    /// <summary>Zeroes both readouts. Called on enable and by a revive/retry.</summary>
    public void ResetCounters()
    {
        running = false;
        ElapsedSeconds = 0f;
        lastShownSecond = -1;
        Kills = 0;

        RenderTime(0);
        if (killText != null)
        {
            killText.text = "0";
            killText.color = killBaseColor;
        }
    }

    /// <summary>Freezes the clock - call it on win or lose to hold the final time.</summary>
    public void StopTimer() => running = false;

    private void HandleBattleStarted()
    {
        // When the timer is meant to include the camera pan there is nothing to
        // wait for; otherwise Update picks it up once the first enemy lands.
        if (!startOnFirstEnemy) running = true;
    }

    private void RenderTime(int totalSeconds)
    {
        if (timerText == null) return;

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        // Minutes are padded to two digits but NOT clamped: a very long battle
        // rolls to 100 : 00 rather than wrapping back to 00 and lying.
        timerText.text = minutes.ToString("00") + separator + seconds.ToString("00");
    }

    private void HandleEnemyKilled()
    {
        Kills++;

        if (killText == null) return;

        killText.text = Kills.ToString();

        // Killed and rebuilt rather than left to overlap: two enemies dying in
        // the same frame would otherwise stack two punches and leave the label
        // at a scale neither tween owns.
        punchTween?.Kill(true);
        colorTween?.Kill();

        var textRT = killText.rectTransform;
        textRT.localScale = Vector3.one;
        punchTween = textRT.DOPunchScale(Vector3.one * punchScale, punchDuration, punchVibrato, 0.6f)
                           .SetUpdate(true);

        if (killIcon != null)
        {
            killIcon.localScale = Vector3.one;
            killIcon.DOPunchScale(Vector3.one * punchScale * 0.6f, punchDuration, punchVibrato, 0.6f)
                    .SetUpdate(true);
        }

        killText.color = flashColor;
        colorTween = killText.DOColor(killBaseColor, punchDuration).SetUpdate(true);
    }
}
