using System;
using System.Collections;
using UnityEngine;

namespace Blasty.Vfx
{
    /// <summary>Reusable, one-shot sprite slash. Attach this to the slash prefab.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class FireSwordSlashVfx : MonoBehaviour
    {
        public const float BaseDuration = 16f / 60f;

        [Header("Rendering")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private bool flipX;
        [SerializeField] private bool flipY;

        [Header("Fire Fade")]
        [Range(0f, 1f)] [SerializeField] private float tailFadeStart = 0.62f;
        [Range(0.01f, 0.5f)] [SerializeField] private float tailFadeSoftness = 0.13f;
        [Range(0f, 1f)] [SerializeField] private float lowerOpacity = 0.15f;

        [Header("Playback")]
        [Min(0.01f)] [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool disableAfterPlayback = true;

        public event Action<FireSwordSlashVfx> ReturnedToPool;

        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private Coroutine finishRoutine;
        private MaterialPropertyBlock propertyBlock;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int TailFadeStartId = Shader.PropertyToID("_TailFadeStart");
        private static readonly int TailFadeSoftnessId = Shader.PropertyToID("_TailFadeSoftness");
        private static readonly int LowerOpacityId = Shader.PropertyToID("_LowerOpacity");

        public float PlaybackSpeed
        {
            get => playbackSpeed;
            set { playbackSpeed = Mathf.Max(0.01f, value); ApplySettings(); }
        }

        /// <summary>Keep false on the gameplay prefab; preview helpers can set it true/false as needed.</summary>
        public bool DisableAfterPlayback
        {
            get => disableAfterPlayback;
            set => disableAfterPlayback = value;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            ApplySettings();
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        private void LateUpdate()
        {
            if (animator == null || spriteRenderer == null) return;
            UpdateVisualMask(Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(0).normalizedTime));
        }

        private void OnDisable()
        {
            if (finishRoutine != null)
                StopCoroutine(finishRoutine);
            finishRoutine = null;
        }

        private void OnValidate()
        {
            playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
            tailFadeSoftness = Mathf.Max(0.01f, tailFadeSoftness);
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<Animator>();
            ApplySettings();
        }

        /// <summary>Restarts the 16-frame slash from frame zero.</summary>
        public void Play()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            ApplySettings();
            animator.speed = playbackSpeed;
            animator.Rebind();
            animator.Update(0f);
            animator.Play(0, 0, 0f);
            UpdateVisualMask(0f);

            if (finishRoutine != null)
                StopCoroutine(finishRoutine);
            finishRoutine = StartCoroutine(FinishAfterPlayback());
        }

        /// <summary>Stops playback and returns this instance to its pool (or disables it).</summary>
        public void Stop()
        {
            if (finishRoutine != null)
                StopCoroutine(finishRoutine);
            finishRoutine = null;
            animator.Rebind();
            ReturnToPoolOrDisable();
        }

        private IEnumerator FinishAfterPlayback()
        {
            yield return new WaitForSeconds(BaseDuration / playbackSpeed);
            finishRoutine = null;
            ReturnToPoolOrDisable();
        }

        private void ReturnToPoolOrDisable()
        {
            ReturnedToPool?.Invoke(this);
            if (disableAfterPlayback && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void ApplySettings()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.sortingLayerName = sortingLayerName;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = color;
            spriteRenderer.flipX = flipX;
            spriteRenderer.flipY = flipY;
        }

        private void UpdateVisualMask(float progress)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ProgressId, progress);
            propertyBlock.SetFloat(TailFadeStartId, tailFadeStart);
            propertyBlock.SetFloat(TailFadeSoftnessId, tailFadeSoftness);
            propertyBlock.SetFloat(LowerOpacityId, lowerOpacity);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
