// GameAudio.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The game's sound: the gameplay music loop and the two hit sounds.
///
/// MUSIC plays only in a STAGE (a scene that contains a LevelGameManager -
/// Level_1_Stage_1..10). It starts from the top when BATTLE is pressed
/// (BattleStartController.OnAnyBattleStarted - the board phase is silent),
/// loops, and fades out when the stage ends (LevelGameManager
/// reports Won or Lost - i.e. as Level Complete / Lose appears). Menus, the
/// tutorial board and the Coming Soon screen have no LevelGameManager, so they
/// stay silent. If a stage resumes after a revive, the music comes back.
///
/// HITS: PlayHeroHit / PlayEnemyHit are called by the weapon colliders at the
/// moment a blow lands (PlayerDamageCollider / EnemyDamageCollider). Each kind
/// is rate-limited and slightly pitch-varied so a crowded melee stays readable
/// instead of turning into one loud crack.
///
/// SELF-BOOTSTRAPPING: nothing to place in a scene. Clips and volumes come from
/// Resources/Audio/GameAudioConfig.asset (GameAudioConfig).
/// </summary>
[DefaultExecutionOrder(-900)]
public class GameAudio : MonoBehaviour
{
    private static GameAudio instance;

    private GameAudioConfig config;
    private AudioSource music;
    private AudioSource[] voices;
    private int nextVoice;
    private float lastHeroHit = -10f, lastEnemyHit = -10f;
    private Coroutine musicFade;
    private bool inStage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance) return;
        var go = new GameObject("[GameAudio]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameAudio>();
    }

    private void Awake()
    {
        config = GameAudioConfig.Load();
        if (!config) Debug.LogWarning("[GameAudio] No Resources/" + GameAudioConfig.ResourcePath + ".asset - the game will be silent.");

        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.spatialBlend = 0f;
        music.priority = 0;          // never stolen by hit sounds

        int n = config ? config.hitVoices : 6;
        voices = new AudioSource[n];
        for (int i = 0; i < n; i++)
        {
            var v = gameObject.AddComponent<AudioSource>();
            v.playOnAwake = false;
            v.spatialBlend = 0f;
            v.priority = 128;
            voices[i] = v;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        LevelGameManager.OnGameStateChanged += HandleGameStateChanged;
        BattleStartController.OnAnyBattleStarted += HandleBattleStarted;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        LevelGameManager.OnGameStateChanged -= HandleGameStateChanged;
        BattleStartController.OnAnyBattleStarted -= HandleBattleStarted;
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------ music

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Decided by WHAT IS LOADED, not by the load mode: the tutorial preloads
        // MenuScene additively, and a stage is reached through an async load.
        bool stageNow = FindObjectOfType<LevelGameManager>() != null;

        if (!stageNow)
        {
            StopMusic(fast: true);
        }
        else if (!inStage || mode == LoadSceneMode.Single)
        {
            // A new stage: silent during the board phase - the music starts on
            // BATTLE (HandleBattleStarted). A stage with no BATTLE button at all
            // would never get that event, so it starts here instead.
            StopMusic(fast: true);
            if (!FindObjectOfType<BattleStartController>()) StartMusic();
        }

        inStage = stageNow;
    }

    // Arash 2026-10-07: the music starts when BATTLE is pressed, not at stage load.
    private void HandleBattleStarted()
    {
        if (inStage) StartMusic();
    }

    private void HandleGameStateChanged(LevelGameManager.GameState state)
    {
        if (state == LevelGameManager.GameState.Won || state == LevelGameManager.GameState.Lost)
            StopMusic(fast: false);
        else if (state == LevelGameManager.GameState.Playing && inStage && !music.isPlaying
                 && BattleStartController.BattleIsRunning)
            StartMusic();   // resumed after a revive
    }

    private void StartMusic()
    {
        if (!config || !config.gameplayMusic) return;

        music.clip = config.gameplayMusic;
        music.time = 0f;              // every stage starts the track from the top
        music.volume = 0f;
        music.Play();
        FadeMusic(config.musicVolume, config.musicFadeIn, stopAtEnd: false);
    }

    private void StopMusic(bool fast)
    {
        if (!music.isPlaying) return;
        float t = fast || !config ? 0.15f : config.musicFadeOut;
        FadeMusic(0f, t, stopAtEnd: true);
    }

    private void FadeMusic(float target, float seconds, bool stopAtEnd)
    {
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeRoutine(target, seconds, stopAtEnd));
    }

    private IEnumerator FadeRoutine(float target, float seconds, bool stopAtEnd)
    {
        float start = music.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;   // fades still run while the game is paused
            music.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / seconds));
            yield return null;
        }
        music.volume = target;
        if (stopAtEnd) music.Stop();
        musicFade = null;
    }

    // ------------------------------------------------------------ hits

    /// <summary>A hero's weapon landed on an enemy or the enemy base.</summary>
    public static void PlayHeroHit()
    {
        if (instance && instance.config)
            instance.PlayHit(instance.config.heroHit, ref instance.lastHeroHit);
    }

    /// <summary>An enemy's weapon landed on a hero or the player base.</summary>
    public static void PlayEnemyHit()
    {
        if (instance && instance.config)
            instance.PlayHit(instance.config.enemyHit, ref instance.lastEnemyHit);
    }

    private void PlayHit(AudioClip clip, ref float lastTime)
    {
        if (!clip || voices.Length == 0) return;

        float now = Time.unscaledTime;
        if (now - lastTime < config.minHitInterval) return;
        lastTime = now;

        var v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        v.pitch = 1f + Random.Range(-config.pitchJitter, config.pitchJitter);
        v.PlayOneShot(clip, config.hitVolume * Random.Range(0.9f, 1f));
    }
}
