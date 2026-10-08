using UnityEngine;

/// <summary>
/// The clips and levels GameAudio plays. Lives at
/// Assets/Resources/Audio/GameAudioConfig.asset so the self-bootstrapping
/// GameAudio can load it in a build (a Resources reference also pulls the clips
/// into the build). Tune the volumes here, not in code.
/// </summary>
[CreateAssetMenu(fileName = "GameAudioConfig", menuName = "Blasty/Audio/Game Audio Config")]
public class GameAudioConfig : ScriptableObject
{
    public const string ResourcePath = "Audio/GameAudioConfig";

    [Header("Gameplay music (stages only - never in menus)")]
    public AudioClip gameplayMusic;
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Min(0f)] public float musicFadeIn = 0.6f;
    [Tooltip("Fade when the stage ends (Level Complete / Lose).")]
    [Min(0f)] public float musicFadeOut = 1.0f;

    [Header("Hit sounds - one per landed blow")]
    [Tooltip("A HERO's weapon lands on an enemy or the enemy base.")]
    public AudioClip heroHit;
    [Tooltip("An ENEMY's weapon lands on a hero or the player base.")]
    public AudioClip enemyHit;
    [Range(0f, 1f)] public float hitVolume = 0.5f;
    [Tooltip("Random pitch +/- this fraction, so repeated blows do not sound identical.")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.06f;
    [Tooltip("Seconds that must pass before the SAME kind of hit sounds again. A big " +
             "melee lands many blows in one frame; without this they stack into one loud crack.")]
    [Min(0f)] public float minHitInterval = 0.06f;
    [Tooltip("How many hit sounds may ring at once.")]
    [Range(1, 16)] public int hitVoices = 6;

    private static GameAudioConfig cached;

    public static GameAudioConfig Load()
    {
        if (!cached) cached = Resources.Load<GameAudioConfig>(ResourcePath);
        return cached;
    }
}
