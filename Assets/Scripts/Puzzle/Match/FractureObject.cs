using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FractureObject : MonoBehaviour
{
    [Header("Drop / Particle Template")]
    [Tooltip("Use a prefab parent that contains small drop pieces as children. The script will clone random children as tiny particles.")]
    public GameObject fracturedPrefab;

    [Header("Blasty-Style Particle Count")]
    [Tooltip("Minimum tiny drops spawned per matched block.")]
    [Min(1)] public int particlesPerBurstMin = 24;

    [Tooltip("Maximum tiny drops spawned per matched block.")]
    [Min(1)] public int particlesPerBurstMax = 36;

    [Header("Particle Size")]
    [Tooltip("Main size multiplier for every tiny drop. If drops look too big, reduce this.")]
    public float particleBaseScale = 0.18f;

    [Tooltip("Random scale range for each tiny drop.")]
    public Vector2 particleScaleRandomRange = new Vector2(0.65f, 1.35f);

    [Tooltip("Small squash/stretch randomness.")]
    public Vector2 particleStretchRange = new Vector2(0.85f, 1.2f);

    [Header("Burst Timing")]
    [Tooltip("Total lifetime of each burst particle.")]
    public float burstDuration = 0.48f;

    [Tooltip("Per-particle lifetime randomness. 0.2 means +/-20%.")]
    [Range(0f, 0.8f)]
    public float durationJitter = 0.18f;

    [Tooltip("Fast pop at the beginning.")]
    public float popDuration = 0.055f;

    [Tooltip("When the particle starts shrinking and fading.")]
    [Range(0f, 1f)]
    public float fadeStartFraction = 0.52f;

    [Header("Burst Movement")]
    [Tooltip("Small random offset at spawn so particles do not start perfectly stacked.")]
    public float spawnRadius = 0.045f;

    [Tooltip("Minimum burst speed.")]
    public float speedMin = 1.25f;

    [Tooltip("Maximum burst speed.")]
    public float speedMax = 3.15f;

    [Tooltip("Horizontal speed multiplier.")]
    public float horizontalSpeedMultiplier = 0.85f;

    [Tooltip("Vertical speed multiplier.")]
    public float verticalSpeedMultiplier = 1.15f;

    [Tooltip("Gravity applied after the burst. Negative value pulls particles downward.")]
    public float gravity = -6.2f;

    [Tooltip("Drag slows the particles smoothly. Higher = particles stop faster.")]
    public float drag = 2.6f;

    [Tooltip("0 = full circle burst, 1 = mostly upward burst.")]
    [Range(0f, 1f)]
    public float upwardBias = 0.72f;

    [Tooltip("Extra tiny random motion added over time.")]
    public float turbulence = 0.22f;

    [Header("Impact Punch")]
    [Tooltip("Particles briefly scale up at the start.")]
    public float popScaleBoost = 1.28f;

    [Tooltip("Small delay randomness so all particles do not move exactly on same frame.")]
    public float startDelayMax = 0.025f;

    [Header("Rotation")]
    public float maxRandomSpin = 520f;

    [Header("Sorting / Layer")]
    [Tooltip("Optional sorting order offset for SpriteRenderers. Use 0 if you do not need it.")]
    public int sortingOrderOffset = 2;

    [Header("Color Mapping")]
    public Color blueColor;
    public Color crimsonColor;
    public Color greenColor;
    public Color pinkColor;
    public Color midPinkColor;
    public Color darkPinkColor;
    public Color purpleColor;
    public Color midPurpleColor;
    public Color orangeColor;
    public Color yellowColor;
    public Color defaultColor = Color.white;

    // --------------------------------------------------------------------
    // PUBLIC ENTRY
    // Keep this compatible with your current MatchResolver:
    // fractureManager.Explode(p.transform, p.ColorId);
    // --------------------------------------------------------------------
    public void Explode(Transform origin, int colorId)
    {
        if (!origin) return;
        SpawnBlastyBurst(origin.position, colorId, origin.rotation);
    }

    // Optional direct position version.
    public void ExplodeAtPosition(Vector3 worldPosition, int colorId)
    {
        SpawnBlastyBurst(worldPosition, colorId, Quaternion.identity);
    }

    // --------------------------------------------------------------------
    // Main Blasty-style burst.
    // --------------------------------------------------------------------
    private void SpawnBlastyBurst(Vector3 center, int colorId, Quaternion rotation)
    {
        if (!fracturedPrefab)
        {
            Debug.LogError("FractureObject: fracturedPrefab is not assigned.");
            return;
        }

        List<GameObject> templates = GetParticleTemplates();

        if (templates.Count == 0)
        {
            Debug.LogError("FractureObject: fracturedPrefab has no usable renderers or child templates.");
            return;
        }

        GameObject root = new GameObject("BlastyBurst_" + colorId + "_" + Random.Range(1000, 9999));
        root.transform.position = center;
        root.transform.rotation = rotation;

        int minCount = Mathf.Max(1, particlesPerBurstMin);
        int maxCount = Mathf.Max(minCount, particlesPerBurstMax);
        int count = Random.Range(minCount, maxCount + 1);

        Color burstColor = GetColor(colorId);

        float longestLife = burstDuration * (1f + durationJitter) + startDelayMax + 0.25f;

        for (int i = 0; i < count; i++)
        {
            GameObject template = templates[Random.Range(0, templates.Count)];

            GameObject particle = Instantiate(template, root.transform);
            particle.name = "Drop_" + i;

            particle.SetActive(true);

            Transform tr = particle.transform;
            tr.position = center + RandomInsideCircle3D(spawnRadius);
            tr.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            DisablePhysicsAndColliders(tr);
            ApplyColor(tr, burstColor);
            ApplySortingOffset(tr);

            float scaleRandom = Random.Range(
                Mathf.Min(particleScaleRandomRange.x, particleScaleRandomRange.y),
                Mathf.Max(particleScaleRandomRange.x, particleScaleRandomRange.y)
            );

            float stretchX = Random.Range(
                Mathf.Min(particleStretchRange.x, particleStretchRange.y),
                Mathf.Max(particleStretchRange.x, particleStretchRange.y)
            );

            float stretchY = Random.Range(
                Mathf.Min(particleStretchRange.x, particleStretchRange.y),
                Mathf.Max(particleStretchRange.x, particleStretchRange.y)
            );

            Vector3 sourceScale = template.transform.localScale;

            // Force the effect to be tiny drops instead of large joined chunks.
            Vector3 targetScale = new Vector3(
                Mathf.Abs(sourceScale.x) * particleBaseScale * scaleRandom * stretchX,
                Mathf.Abs(sourceScale.y) * particleBaseScale * scaleRandom * stretchY,
                Mathf.Abs(sourceScale.z) * particleBaseScale * scaleRandom
            );

            tr.localScale = targetScale * 0.1f;

            StartCoroutine(AnimateBlastyParticle(tr, targetScale));
        }

        Destroy(root, longestLife);
    }

    // --------------------------------------------------------------------
    // Particle motion: fast pop, outward/upward burst, gravity, fade.
    // --------------------------------------------------------------------
    private IEnumerator AnimateBlastyParticle(Transform particle, Vector3 targetScale)
    {
        if (!particle) yield break;

        float delay = Random.Range(0f, Mathf.Max(0f, startDelayMax));

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!particle) yield break;

        float life = burstDuration;

        if (durationJitter > 0f)
        {
            float factor = Random.Range(1f - durationJitter, 1f + durationJitter);
            life = Mathf.Max(0.08f, burstDuration * factor);
        }

        Vector2 direction = GetBlastyDirection();

        float speed = Random.Range(speedMin, speedMax);

        Vector2 velocity = new Vector2(
            direction.x * speed * horizontalSpeedMultiplier,
            direction.y * speed * verticalSpeedMultiplier
        );

        float spin = Random.Range(-maxRandomSpin, maxRandomSpin);
        Vector3 startPos = particle.position;

        VisualCache visualCache = BuildVisualCache(particle);
        SetVisualAlpha(visualCache, 1f);

        float t = 0f;

        while (t < life)
        {
            if (!particle) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);

            // Drag + gravity.
            velocity *= Mathf.Exp(-drag * Time.deltaTime);
            velocity.y += gravity * Time.deltaTime;

            Vector2 noise = Random.insideUnitCircle * turbulence * Time.deltaTime;

            Vector3 move = new Vector3(
                (velocity.x + noise.x) * Time.deltaTime,
                (velocity.y + noise.y) * Time.deltaTime,
                0f
            );

            particle.position += move;

            // Scale curve:
            // 1) very fast pop
            // 2) hold
            // 3) shrink to zero
            float scaleMul;

            float popK = popDuration <= 0f ? 1f : Mathf.Clamp01(t / popDuration);

            if (popK < 1f)
            {
                float easedPop = EaseOutBack(popK);
                scaleMul = Mathf.Lerp(0.15f, popScaleBoost, easedPop);
            }
            else if (k < fadeStartFraction)
            {
                float holdK = Mathf.InverseLerp(popDuration / life, fadeStartFraction, k);
                scaleMul = Mathf.Lerp(popScaleBoost, 1f, EaseOutCubic(holdK));
            }
            else
            {
                float fadeK = Mathf.InverseLerp(fadeStartFraction, 1f, k);
                scaleMul = Mathf.Lerp(1f, 0f, EaseInCubic(fadeK));
            }

            particle.localScale = targetScale * scaleMul;

            // Alpha curve.
            float alpha = 1f;

            if (k >= fadeStartFraction)
            {
                float fadeK = Mathf.InverseLerp(fadeStartFraction, 1f, k);
                alpha = Mathf.Lerp(1f, 0f, EaseInCubic(fadeK));
            }

            SetVisualAlpha(visualCache, alpha);

            if (Mathf.Abs(spin) > 0.01f)
                particle.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);

            yield return null;
        }

        if (particle != null)
            Destroy(particle.gameObject);
    }

    // --------------------------------------------------------------------
    // Direction helper.
    // Mostly upward/sideways, with a few full radial particles.
    // This is closer to the Blasty Stacks burst than big fragment arcs.
    // --------------------------------------------------------------------
    private Vector2 GetBlastyDirection()
    {
        bool mostlyUp = Random.value < upwardBias;

        float angle;

        if (mostlyUp)
        {
            // Upward cone: 25 to 155 degrees.
            angle = Random.Range(25f, 155f);
        }
        else
        {
            // A few particles go in all directions for natural splash.
            angle = Random.Range(0f, 360f);
        }

        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.up;

        return dir.normalized;
    }

    // --------------------------------------------------------------------
    // Template helper.
    // If fracturedPrefab has children, random children are used as drop templates.
    // If not, the prefab itself is used.
    // --------------------------------------------------------------------
    private List<GameObject> GetParticleTemplates()
    {
        List<GameObject> templates = new List<GameObject>();

        if (!fracturedPrefab)
            return templates;

        if (fracturedPrefab.transform.childCount > 0)
        {
            foreach (Transform child in fracturedPrefab.transform)
            {
                if (!child) continue;

                if (child.GetComponentInChildren<Renderer>(true) != null)
                    templates.Add(child.gameObject);
            }
        }

        if (templates.Count == 0)
        {
            if (fracturedPrefab.GetComponentInChildren<Renderer>(true) != null)
                templates.Add(fracturedPrefab);
        }

        return templates;
    }

    // --------------------------------------------------------------------
    // Physics / collider cleanup.
    // --------------------------------------------------------------------
    private void DisablePhysicsAndColliders(Transform obj)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (rb)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        Rigidbody2D rb2D = obj.GetComponent<Rigidbody2D>();

        if (rb2D)
        {
            rb2D.linearVelocity = Vector2.zero;
            rb2D.angularVelocity = 0f;
            rb2D.gravityScale = 0f;
            rb2D.bodyType = RigidbodyType2D.Kinematic;
        }

        Collider[] colliders3D = obj.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders3D)
            col.enabled = false;

        Collider2D[] colliders2D = obj.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D col in colliders2D)
            col.enabled = false;
    }

    // --------------------------------------------------------------------
    // Color / material helpers.
    // Supports SpriteRenderer, URP _BaseColor, and Built-in _Color.
    // --------------------------------------------------------------------
    private void ApplyColor(Transform obj, Color color)
    {
        SpriteRenderer[] spriteRenderers = obj.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer sr in spriteRenderers)
        {
            Color c = color;
            c.a = sr.color.a <= 0f ? 1f : sr.color.a;
            sr.color = c;
        }

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            if (rend is SpriteRenderer) continue;

            Material[] sourceMaterials = rend.sharedMaterials;
            Material[] runtimeMaterials = new Material[sourceMaterials.Length];

            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material sourceMat = sourceMaterials[i];

                if (sourceMat == null)
                {
                    runtimeMaterials[i] = null;
                    continue;
                }

                Material mat = new Material(sourceMat);
                SetMaterialColor(mat, color);
                SetMaterialAlpha(mat, 1f);
                runtimeMaterials[i] = mat;
            }

            rend.materials = runtimeMaterials;
        }
    }

    private void ApplySortingOffset(Transform obj)
    {
        if (sortingOrderOffset == 0)
            return;

        SpriteRenderer[] spriteRenderers = obj.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer sr in spriteRenderers)
            sr.sortingOrder += sortingOrderOffset;
    }

    private class VisualCache
    {
        public readonly List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        public readonly List<Material> materials = new List<Material>();
    }

    private VisualCache BuildVisualCache(Transform obj)
    {
        VisualCache cache = new VisualCache();

        SpriteRenderer[] spriteRenderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
        cache.spriteRenderers.AddRange(spriteRenderers);

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            if (rend is SpriteRenderer) continue;

            Material[] mats = rend.materials;

            foreach (Material mat in mats)
            {
                if (mat != null && !cache.materials.Contains(mat))
                    cache.materials.Add(mat);
            }
        }

        return cache;
    }

    private void SetVisualAlpha(VisualCache cache, float alpha)
    {
        if (cache == null) return;

        foreach (SpriteRenderer sr in cache.spriteRenderers)
        {
            if (!sr) continue;

            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }

        foreach (Material mat in cache.materials)
            SetMaterialAlpha(mat, alpha);
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;

        float alpha = GetMaterialAlpha(mat);
        color.a = alpha;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        else
            mat.color = color;
    }

    private void SetMaterialAlpha(Material mat, float alpha)
    {
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
        }
        else if (mat.HasProperty("_Color"))
        {
            Color c = mat.GetColor("_Color");
            c.a = alpha;
            mat.SetColor("_Color", c);
        }
        else
        {
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
        }
    }

    private float GetMaterialAlpha(Material mat)
    {
        if (mat == null) return 1f;

        if (mat.HasProperty("_BaseColor"))
            return mat.GetColor("_BaseColor").a;

        if (mat.HasProperty("_Color"))
            return mat.GetColor("_Color").a;

        return mat.color.a;
    }

    // --------------------------------------------------------------------
    // General helpers.
    // --------------------------------------------------------------------
    private Vector3 RandomInsideCircle3D(float radius)
    {
        Vector2 p = Random.insideUnitCircle * radius;
        return new Vector3(p.x, p.y, 0f);
    }

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private Color GetColor(int id)
    {
        return id switch
        {
            0 => blueColor,
            1 => crimsonColor,
            2 => greenColor,
            3 => pinkColor,
            4 => midPinkColor,
            5 => darkPinkColor,
            6 => purpleColor,
            7 => midPurpleColor,
            8 => orangeColor,
            9 => yellowColor,
            _ => defaultColor
        };
    }
}



public class FractureObject1 : MonoBehaviour
{
    [Header("Fragment Count")]
    [Tooltip("Minimum number of shards used per explosion. Lower = cleaner effect.")]
    [Min(1)] public int fragmentsPerExplosionMin = 5;

    [Tooltip("Maximum number of shards used per explosion. Lower this if the effect looks too crowded.")]
    [Min(1)] public int fragmentsPerExplosionMax = 8;

    [Header("Randomness")]
    [Tooltip("How much each fragment can deviate from the base duration. 0.35 = +/-35%.")]
    [Range(0f, 0.9f)]
    public float durationJitter = 0.35f;

    [Tooltip("Extra sideways drift added to each fragment path.")]
    public float sideJitterMax = 0.45f;

    [Tooltip("Small starting spread so fragments do not begin completely joined.")]
    public float initialScatterRadius = 0.12f;

    [Tooltip("Use each prefab child's original local position to decide its explosion direction.")]
    public bool useOriginalShardPositionForDirection = true;

    [Tooltip("0 = fully random direction, 1 = mostly based on shard's original prefab position.")]
    [Range(0f, 1f)]
    public float originalShardDirectionWeight = 0.6f;

    [Header("Scale Randomness")]
    [Tooltip("Random scale range applied to the whole fracture instance.")]
    public Vector2 explosionParentScaleRange = new Vector2(0.85f, 1.15f);

    [Tooltip("Random scale range applied to each individual fragment.")]
    public Vector2 fragmentScaleRandomRange = new Vector2(0.65f, 1.25f);

    [Header("Rotation")]
    [Tooltip("Max random spin speed in degrees/second around Z.")]
    public float maxRandomSpin = 220f;

    [Header("Pop In")]
    [Tooltip("Small value is best. Do not use a value like 9 here.")]
    public float popInDuration = 0.08f;

    [Tooltip("Start size of the fragment before it pops in. 0.15 means 15% of final size.")]
    public float popInStartScaleMultiplier = 0.15f;

    [Header("Final Fragment Size")]
    [Tooltip("Extra multiplier applied to final normal size of each fragment.")]
    public float finalFragmentSizeMultiplier = 0.75f;

    [Header("Fracture Prefab Parent With Shards As Children")]
    public GameObject fracturedPrefab;

    [Header("Arc Movement World XY Plane")]
    [Tooltip("Base time in seconds for the entire movement.")]
    public float baseDuration = 0.6f;

    [Tooltip("Speed multiplier. 2 = 2x faster.")]
    public float speedMultiplier = 1.3f;

    [Tooltip("Min explosion travel distance in world units.")]
    public float horizontalRadiusMin = 1.2f;

    [Tooltip("Max explosion travel distance in world units.")]
    public float horizontalRadiusMax = 2.4f;

    [Tooltip("Min upward arc height on world Y.")]
    public float arcHeightMin = 0.45f;

    [Tooltip("Max upward arc height on world Y.")]
    public float arcHeightMax = 1.0f;

    [Tooltip("How much lower than start the fragments end on Y.")]
    public float endDrop = 0.2f;

    [Header("Shrink And Fade")]
    [Tooltip("0.55 means shrink/fade starts after 55% of the movement.")]
    [Range(0f, 1f)]
    public float fadeStartFraction = 0.55f;

    [Tooltip("Global size multiplier. Keep this around 1. Do not use 2 unless fragments are very tiny.")]
    public float fragmentScaleMultiplier = 1f;

    [Header("Color Mapping")]
    public Color blueColor;
    public Color crimsonColor;
    public Color greenColor;
    public Color pinkColor;
    public Color midPinkColor;
    public Color darkPinkColor;
    public Color purpleColor;
    public Color midPurpleColor;
    public Color orangeColor;
    public Color yellowColor;
    public Color defaultColor = Color.white;

    // --------------------------------------------------------------------
    // PUBLIC ENTRY
    // Example:
    // fractureManager.Explode(p.transform, p.ColorId);
    // --------------------------------------------------------------------
    public void Explode(Transform origin, int colorId)
    {
        if (!fracturedPrefab)
        {
            Debug.LogError("FractureObject: fracturedPrefab is not assigned!");
            return;
        }

        GameObject instance = Instantiate(
            fracturedPrefab,
            origin.position,
            origin.rotation
        );

        instance.name = fracturedPrefab.name + "_Color_" + colorId + "_" + Random.Range(1000, 9999);

        // Randomize whole parent scale so two explosions do not look identical.
        float parentScale = Random.Range(
            Mathf.Min(explosionParentScaleRange.x, explosionParentScaleRange.y),
            Mathf.Max(explosionParentScaleRange.x, explosionParentScaleRange.y)
        );
        instance.transform.localScale *= parentScale;

        float duration = Mathf.Max(
            0.05f,
            baseDuration / Mathf.Max(0.1f, speedMultiplier)
        );

        Color fragColor = GetColor(colorId);
        Vector3 center = origin.position;

        List<Transform> fragments = new List<Transform>();

        foreach (Transform child in instance.transform)
        {
            if (child != null)
                fragments.Add(child);
        }

        if (fragments.Count == 0)
        {
            Debug.LogWarning("FractureObject: fracturedPrefab has no child fragments.");
            Destroy(instance);
            return;
        }

        Shuffle(fragments);

        int minCount = Mathf.Clamp(fragmentsPerExplosionMin, 1, fragments.Count);
        int maxCount = Mathf.Clamp(fragmentsPerExplosionMax, minCount, fragments.Count);
        int selectedCount = Random.Range(minCount, maxCount + 1);

        for (int i = 0; i < fragments.Count; i++)
        {
            Transform frag = fragments[i];
            if (!frag) continue;

            // Remove extra shards so the effect is not too crowded.
            if (i >= selectedCount)
            {
                Destroy(frag.gameObject);
                continue;
            }

            Vector3 originalLocalPosition = frag.localPosition;

            float fragmentRandomScale = Random.Range(
                Mathf.Min(fragmentScaleRandomRange.x, fragmentScaleRandomRange.y),
                Mathf.Max(fragmentScaleRandomRange.x, fragmentScaleRandomRange.y)
            );

            frag.localScale *= fragmentScaleMultiplier * fragmentRandomScale;

            DisablePhysics(frag);

            // Important: this applies the match color to the shards.
            ApplyColor(frag, fragColor);

            StartCoroutine(AnimateFragmentArc(frag, center, duration, originalLocalPosition));
        }

        Destroy(instance, duration * (1f + durationJitter) + 0.7f);
    }

    // --------------------------------------------------------------------
    // Animate one fragment along a randomized XY arc, then shrink and fade.
    // --------------------------------------------------------------------
    private IEnumerator AnimateFragmentArc(
        Transform frag,
        Vector3 center,
        float baseDuration,
        Vector3 originalLocalPosition
    )
    {
        if (!frag) yield break;

        float localDuration = baseDuration;

        if (durationJitter > 0f)
        {
            float factor = Random.Range(1f - durationJitter, 1f + durationJitter);
            localDuration = Mathf.Max(0.08f, baseDuration * factor);
        }

        bool doPopIn = popInDuration > 0f && popInStartScaleMultiplier > 0f;
        float popTime = doPopIn ? Mathf.Min(popInDuration, localDuration * 0.35f) : 0f;
        float fadeStartTime = Mathf.Max(popTime, localDuration * fadeStartFraction);
        float fadeTime = Mathf.Max(0.05f, localDuration - fadeStartTime);

        Vector2 randomDir = Random.insideUnitCircle;
        if (randomDir.sqrMagnitude < 0.0001f)
            randomDir = Vector2.right;

        randomDir.Normalize();

        Vector2 originalDir = new Vector2(originalLocalPosition.x, originalLocalPosition.y);

        Vector2 finalDir = randomDir;

        if (useOriginalShardPositionForDirection && originalDir.sqrMagnitude > 0.0001f)
        {
            originalDir.Normalize();

            finalDir = Vector2.Lerp(
                randomDir,
                originalDir,
                originalShardDirectionWeight
            );

            if (finalDir.sqrMagnitude < 0.0001f)
                finalDir = randomDir;

            finalDir.Normalize();
        }

        float radius = Random.Range(horizontalRadiusMin, horizontalRadiusMax);
        float height = Random.Range(arcHeightMin, arcHeightMax);

        Vector2 perp = new Vector2(-finalDir.y, finalDir.x);
        float sideAmount = Random.Range(-sideJitterMax, sideJitterMax);

        Vector2 endOffset2D = finalDir * radius + perp * sideAmount;

        Vector2 startScatter2D = Random.insideUnitCircle * initialScatterRadius;

        Vector3 startPos = center + new Vector3(startScatter2D.x, startScatter2D.y, 0f);

        List<Material> fadeMaterials = GetMaterialsFromFragment(frag);
        SetMaterialsAlpha(fadeMaterials, 1f);

        Vector3 baseScale = frag.localScale;
        Vector3 normalScale = baseScale * Mathf.Max(0.0001f, finalFragmentSizeMultiplier);
        Vector3 popStartScale = normalScale * popInStartScaleMultiplier;

        float spinSpeed = 0f;
        if (maxRandomSpin > 0f)
            spinSpeed = Random.Range(-maxRandomSpin, maxRandomSpin);

        frag.position = startPos;
        frag.localScale = doPopIn ? popStartScale : normalScale;

        float t = 0f;

        while (t < localDuration)
        {
            if (!frag) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / localDuration);

            float separation = Mathf.SmoothStep(0f, 1f, k);

            Vector3 travel =
                new Vector3(
                    endOffset2D.x * separation,
                    endOffset2D.y * separation,
                    0f
                );

            float arcY = Mathf.Sin(k * Mathf.PI) * height;

            Vector3 pos = startPos + travel + new Vector3(0f, arcY - endDrop * k, 0f);
            frag.position = pos;

            // Pop-in stage.
            if (doPopIn && t <= popTime)
            {
                float pin = Mathf.Clamp01(t / popTime);
                float eased = 1f - Mathf.Pow(1f - pin, 3f);

                frag.localScale = Vector3.Lerp(popStartScale, normalScale, eased);
                SetMaterialsAlpha(fadeMaterials, 1f);
            }
            else
            {
                float fadeK = 0f;

                if (t >= fadeStartTime)
                    fadeK = Mathf.Clamp01((t - fadeStartTime) / fadeTime);

                frag.localScale = Vector3.Lerp(normalScale, Vector3.zero, fadeK);
                SetMaterialsAlpha(fadeMaterials, Mathf.Lerp(1f, 0f, fadeK));
            }

            if (Mathf.Abs(spinSpeed) > 0.01f)
                frag.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);

            yield return null;
        }

        if (frag != null)
            Destroy(frag.gameObject);
    }

    // --------------------------------------------------------------------
    // Physics helper
    // --------------------------------------------------------------------
    private void DisablePhysics(Transform frag)
    {
        Rigidbody rb = frag.GetComponent<Rigidbody>();

        if (rb)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        Collider col = frag.GetComponent<Collider>();
        if (col)
            col.enabled = false;
    }

    // --------------------------------------------------------------------
    // Material / color helpers
    // --------------------------------------------------------------------
    private void ApplyColor(Transform frag, Color color)
    {
        Renderer[] renderers = frag.GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;

            Material[] sourceMaterials = rend.sharedMaterials;
            Material[] runtimeMaterials = new Material[sourceMaterials.Length];

            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material sourceMat = sourceMaterials[i];

                if (sourceMat == null)
                {
                    runtimeMaterials[i] = null;
                    continue;
                }

                Material mat = new Material(sourceMat);
                SetMaterialColor(mat, color);
                SetMaterialAlpha(mat, 1f);
                runtimeMaterials[i] = mat;
            }

            rend.materials = runtimeMaterials;
        }
    }

    private List<Material> GetMaterialsFromFragment(Transform frag)
    {
        List<Material> materials = new List<Material>();
        Renderer[] renderers = frag.GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;

            Material[] mats = rend.materials;

            foreach (Material mat in mats)
            {
                if (mat != null && !materials.Contains(mat))
                    materials.Add(mat);
            }
        }

        return materials;
    }

    private void SetMaterialsAlpha(List<Material> materials, float alpha)
    {
        for (int i = 0; i < materials.Count; i++)
            SetMaterialAlpha(materials[i], alpha);
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;

        float currentAlpha = GetMaterialAlpha(mat);
        color.a = currentAlpha;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        else
            mat.color = color;
    }

    private void SetMaterialAlpha(Material mat, float alpha)
    {
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
        }
        else if (mat.HasProperty("_Color"))
        {
            Color c = mat.GetColor("_Color");
            c.a = alpha;
            mat.SetColor("_Color", c);
        }
        else
        {
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
        }
    }

    private float GetMaterialAlpha(Material mat)
    {
        if (mat == null) return 1f;

        if (mat.HasProperty("_BaseColor"))
            return mat.GetColor("_BaseColor").a;

        if (mat.HasProperty("_Color"))
            return mat.GetColor("_Color").a;

        return mat.color.a;
    }

    // --------------------------------------------------------------------
    // General helpers
    // --------------------------------------------------------------------
    private void Shuffle(List<Transform> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            Transform temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private Color GetColor(int id)
    {
        return id switch
        {
            0 => blueColor,
            1 => crimsonColor,
            2 => greenColor,
            3 => pinkColor,
            4 => midPinkColor,
            5 => darkPinkColor,
            6 => purpleColor,
            7 => midPurpleColor,
            8 => orangeColor,
            9 => yellowColor,
            _ => defaultColor
        };
    }
}


