using System.Collections.Generic;
using UnityEngine;

namespace Blasty.Vfx
{
    /// <summary>Small reusable pool that an attack event can call at its sword pivot.</summary>
    public sealed class FireSwordSlashSpawner : MonoBehaviour
    {
        [SerializeField] private FireSwordSlashVfx slashPrefab;
        [Min(0)] [SerializeField] private int prewarmCount;
        [SerializeField] private Transform poolParent;

        private readonly List<FireSwordSlashVfx> instances = new();

        private void Awake()
        {
            for (int i = 0; i < prewarmCount; i++)
                CreateInstance();
        }

        /// <summary>Call this directly from the attack-impact animation event.</summary>
        public FireSwordSlashVfx PlayAtSwordPivot(Transform swordPivot)
        {
            if (swordPivot == null) return null;
            return PlayAt(swordPivot.position, swordPivot.rotation);
        }

        public FireSwordSlashVfx PlayAt(Vector3 position, Quaternion rotation)
        {
            if (slashPrefab == null)
            {
                Debug.LogWarning("Assign FireSwordSlashVfx prefab on " + name, this);
                return null;
            }

            FireSwordSlashVfx slash = instances.Find(item => item != null && !item.gameObject.activeSelf);
            if (slash == null) slash = CreateInstance();
            slash.transform.SetPositionAndRotation(position, rotation);
            slash.gameObject.SetActive(true); // OnEnable starts playback.
            return slash;
        }

        private FireSwordSlashVfx CreateInstance()
        {
            var slash = Instantiate(slashPrefab, poolParent != null ? poolParent : transform);
            slash.gameObject.SetActive(false);
            instances.Add(slash);
            return slash;
        }
    }
}
