using System.Collections;
using UnityEngine;

namespace Blasty.Vfx
{
    /// <summary>Preview-scene helper only. Replays a normally one-shot slash forever.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FireSwordSlashVfx))]
    public sealed class FireSwordSlashPreviewLoop : MonoBehaviour
    {
        private FireSwordSlashVfx slash;
        private bool originalDisableAfterPlayback;
        private Coroutine loopRoutine;

        private void Awake()
        {
            slash = GetComponent<FireSwordSlashVfx>();
            originalDisableAfterPlayback = slash.DisableAfterPlayback;
        }

        private void OnEnable()
        {
            if (slash == null) slash = GetComponent<FireSwordSlashVfx>();
            originalDisableAfterPlayback = slash.DisableAfterPlayback;
            slash.DisableAfterPlayback = false;
            slash.Play();
            loopRoutine = StartCoroutine(Loop());
        }

        private void OnDisable()
        {
            if (loopRoutine != null) StopCoroutine(loopRoutine);
            loopRoutine = null;
            if (slash != null) slash.DisableAfterPlayback = originalDisableAfterPlayback;
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(FireSwordSlashVfx.BaseDuration / slash.PlaybackSpeed);
                slash.Play();
            }
        }
    }
}
