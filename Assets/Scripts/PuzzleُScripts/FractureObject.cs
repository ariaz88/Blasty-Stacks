using System.Collections;
using UnityEngine;

using DG.Tweening;

using System.Collections;


public class FractureObject : MonoBehaviour
{
    [Header("Randomness")]
    [Tooltip("How much each fragment can deviate from the base duration (0.2 = ±20%).")]
    [Range(0f, 0.9f)]
    public float durationJitter = 0.2f;

    [Tooltip("Max extra sideways drift (perpendicular to main direction).")]
    public float sideJitterMax = 0.6f;

    [Header("Rotation")]
    [Tooltip("Max random spin speed (degrees/second) around Z.")]
    public float maxRandomSpin = 180f;


    [Header("Pop In")]
    public float popInDuration = 0.3f;            // 0 disables
    public float popInStartScaleMultiplier = 0.1f; // 0 disables

    [Header("Final Fragment Size")]
    [Tooltip("Extra multiplier applied to the final (normal) size of each fragment. Set 1 for normal.")]
    public float finalFragmentSizeMultiplier = 1f;





    [Header("Fracture Prefab (parent with all shards as children)")]
    public GameObject fracturedPrefab;

    [Header("Arc Movement (world XY plane)")]
    [Tooltip("Base time (seconds) for the entire arc (up + down).")]
    public float baseDuration = 0.6f;

    [Tooltip("Speed multiplier. 2 = 2x faster (half the duration), 3 = 3x, etc.")]
    public float speedMultiplier = 1f;

    [Tooltip("Min horizontal radius of the explosion (in world units).")]
    public float horizontalRadiusMin = 1.5f;

    [Tooltip("Max horizontal radius of the explosion (in world units).")]
    public float horizontalRadiusMax = 3.0f;

    [Tooltip("Min vertical arc height (how high the fragments go on Y).")]
    public float arcHeightMin = 0.7f;

    [Tooltip("Max vertical arc height.")]
    public float arcHeightMax = 1.3f;

    [Tooltip("How much lower than start the fragments end (Y).")]
    public float endDrop = 0.2f;

    [Header("Shrink & Fade")]
    [Tooltip("Fraction of the arc where fade starts (0 = from start, 0.7 = last 30%).")]
    [Range(0f, 1f)] public float fadeStartFraction = 0.2f;

    [Tooltip("Correct visual size if shards look too small/large.")]
    public float fragmentScaleMultiplier = 1f;

    [Header("Color Mapping")]
    public Color blueColor;
    public Color crimsonColor;
    public Color greenColor;
    public Color lavenderColor;
    public Color midPinkColor;
    public Color orangeColor;
    public Color pinkColor;
    public Color purpleColor;
    public Color redColor;
    public Color yellowColor;
    public Color defaultColor = Color.white;

    // --------------------------------------------------------------------
    // PUBLIC ENTRY – call from MatchResolver:
    // fractureManager.Explode(p.transform, p.ColorId);
    // --------------------------------------------------------------------
    public void Explode(Transform origin, int colorId)
    {
        if (!fracturedPrefab)
        {
            Debug.LogError("FractureObject: fracturedPrefab is not assigned!");
            return;
        }

        GameObject instance = Instantiate(fracturedPrefab,
                                          origin.position,
                                          origin.rotation);

        // duration for the whole motion (up + down)
        float duration = Mathf.Max(0.05f, baseDuration / Mathf.Max(0.1f, speedMultiplier));
        Color fragColor = GetColor(colorId);
        Vector3 center = origin.position;
      

        foreach (Transform frag in instance.transform)
        {
            if (!frag) continue;

            // Optional size correction (your existing variable)
            frag.localScale *= fragmentScaleMultiplier;

            // Disable physics (same as before)
            Rigidbody rb = frag.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            Collider col = frag.GetComponent<Collider>();
            if (col) col.enabled = false;

            //ApplyColor(frag, fragColor);

            // Start animation from center (THIS is what fixes "full cube growing")
            StartCoroutine(AnimateFragmentArc(frag, center, duration));
        }



        //foreach (Transform frag in instance.transform)
        //{
        //    if (!frag) continue;

        //    // Optional: fix shard size if necessary
        //    frag.localScale *= fragmentScaleMultiplier;

        //    // Disable physics so movement is fully controlled by us
        //    Rigidbody rb = frag.GetComponent<Rigidbody>();
        //    if (rb)
        //    {
        //        rb.linearVelocity = Vector3.zero;
        //        rb.angularVelocity = Vector3.zero;
        //        rb.useGravity = false;
        //        rb.isKinematic = true;
        //    }

        //    Collider col = frag.GetComponent<Collider>();
        //    if (col) col.enabled = false;

        //    ApplyColor(frag, fragColor);

        //    StartCoroutine(AnimateFragmentArc(frag, duration));
        //}

        Destroy(instance, duration + 0.3f);
    }

    // --------------------------------------------------------------------
    // Animate one fragment along a Bezier arc on XY, then shrink & fade
    // --------------------------------------------------------------------
    private IEnumerator AnimateFragmentArc1(Transform frag, float duration)
    {
        if (!frag) yield break;

        Vector3 startPos = frag.position;

        // Random 2D direction on XY plane (camera sees this as sideways)
        Vector2 dir2D = Random.insideUnitCircle.normalized;
        float radius = Random.Range(horizontalRadiusMin, horizontalRadiusMax);
        float height = Random.Range(arcHeightMin, arcHeightMax);

        // End is horizontally offset and slightly lower on Y
        Vector3 endPos = startPos +
                         new Vector3(dir2D.x * radius, -endDrop, 0f);

        // Peak is half-way horizontally, higher on Y
        Vector3 peakPos = startPos +
                          new Vector3(dir2D.x * radius * 0.5f, height, 0f);

        Renderer rend = frag.GetComponentInChildren<Renderer>();
        Material mat = null;
        if (rend != null)
        {
            mat = new Material(rend.material);
            rend.material = mat;
        }

        Vector3 startScale = frag.localScale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            // Quadratic Bezier: start → peak → end
            float oneMinusK = 1f - k;
            Vector3 pos =
                oneMinusK * oneMinusK * startPos +
                2f * oneMinusK * k * peakPos +
                k * k * endPos;

            frag.position = pos;

            // Shrink over the whole duration
            frag.localScale = Vector3.Lerp(startScale, Vector3.zero, k);

            // Fade only over the last part of the motion
            if (mat != null)
            {
                float fadeT = Mathf.InverseLerp(fadeStartFraction, 1f, k);
                Color c = mat.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                mat.color = c;
            }

            yield return null;
        }

        if (frag != null)
            Destroy(frag.gameObject);
    }
    private IEnumerator AnimateFragmentArc2(Transform frag, float baseDuration)
    {
        if (!frag) yield break;

        // Each fragment gets its own slightly different duration
        float localDuration = baseDuration;
        if (durationJitter > 0f)
        {
            float factor = Random.Range(1f - durationJitter, 1f + durationJitter);
            localDuration = Mathf.Max(0.05f, baseDuration * factor);
        }

        Vector3 startPos = frag.position;

        // Random 2D direction on XY plane
        Vector2 dir2D = Random.insideUnitCircle.normalized;
        float radius = Random.Range(horizontalRadiusMin, horizontalRadiusMax);
        float height = Random.Range(arcHeightMin, arcHeightMax);

        // Perpendicular sideways jitter for more natural spreading
        Vector2 perp2D = new Vector2(-dir2D.y, dir2D.x);
        float sideAmount = Random.Range(-sideJitterMax, sideJitterMax);
        Vector2 offset2D = dir2D * radius + perp2D * sideAmount;

        // End is offset horizontally and slightly lower on Y
        Vector3 endPos = startPos +
                         new Vector3(offset2D.x, -endDrop, 0f);

        // Peak is halfway horizontally, higher on Y
        Vector3 peakPos = startPos +
                          new Vector3(offset2D.x * 0.5f, height, 0f);

        Renderer rend = frag.GetComponentInChildren<Renderer>();
        Material mat = null;
        if (rend != null)
        {
            mat = new Material(rend.material);
            rend.material = mat;
        }

        Vector3 startScale = frag.localScale;

        // Random spin speed around Z for this fragment
        float spinSpeed = 0f;
        if (maxRandomSpin > 0f)
            spinSpeed = Random.Range(-maxRandomSpin, maxRandomSpin);

        float t = 0f;
        while (t < localDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / localDuration);

            // Quadratic Bezier: start → peak → end
            float oneMinusK = 1f - k;
            Vector3 pos =
                oneMinusK * oneMinusK * startPos +
                2f * oneMinusK * k * peakPos +
                k * k * endPos;

            frag.position = pos;

            // Shrink over the whole duration
            frag.localScale = Vector3.Lerp(startScale, Vector3.zero, k);

            // Spin
            if (Mathf.Abs(spinSpeed) > 0.01f)
                frag.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);

            // Fade only over the last part of the motion
            if (mat != null)
            {
                float fadeT = Mathf.InverseLerp(fadeStartFraction, 1f, k);
                Color c = mat.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                mat.color = c;
            }

            yield return null;
        }

        if (frag != null)
            Destroy(frag.gameObject);
    }
    private IEnumerator AnimateFragmentArc(Transform frag, Vector3 center, float baseDuration)
    {
        if (!frag) yield break;

        // Per-fragment duration jitter (optional)
        float localDuration = baseDuration;
        if (durationJitter > 0f)
        {
            float factor = Random.Range(1f - durationJitter, 1f + durationJitter);
            localDuration = Mathf.Max(0.08f, baseDuration * factor);
        }

        // Pop-in settings (disabled if either is 0)
        bool doPopIn = popInDuration > 0f && popInStartScaleMultiplier > 0f;
        float popTime = doPopIn ? Mathf.Min(popInDuration, localDuration * 0.9f) : 0f;
        float fadeTime = Mathf.Max(0.05f, localDuration - popTime);

        // Random direction + spread jitter
        Vector2 dir2D = Random.insideUnitCircle.normalized;
        float radius = Random.Range(horizontalRadiusMin, horizontalRadiusMax);
        float height = Random.Range(arcHeightMin, arcHeightMax);

        Vector2 perp2D = new Vector2(-dir2D.y, dir2D.x);
        float sideAmount = Random.Range(-sideJitterMax, sideJitterMax);
        Vector2 offset2D = dir2D * radius + perp2D * sideAmount;

        // Start from center so it looks like "explosion from a tiny cube"
        Vector3 startPos = center;
        Vector3 endPos = startPos + new Vector3(offset2D.x, -endDrop, 0f);
        Vector3 peakPos = startPos + new Vector3(offset2D.x * 0.5f, height, 0f);

        // Material instance for fade-out
        Renderer rend = frag.GetComponentInChildren<Renderer>();
        Material mat = null;
        if (rend != null)
        {
            mat = new Material(rend.material);
            rend.material = mat;
            Color c0 = mat.color; c0.a = 1f; mat.color = c0;
        }

        // Define "normal" size explicitly
        Vector3 baseScale = frag.localScale;
        Vector3 normalScale = baseScale * Mathf.Max(0.0001f, finalFragmentSizeMultiplier);

        Vector3 popStartScale = normalScale * popInStartScaleMultiplier;

        // Random spin
        float spinSpeed = 0f;
        if (maxRandomSpin > 0f)
            spinSpeed = Random.Range(-maxRandomSpin, maxRandomSpin);

        // Start collapsed at center
        frag.position = startPos;

        // Start scale (tiny if pop-in enabled, otherwise normal)
        frag.localScale = doPopIn ? popStartScale : normalScale;

        float t = 0f;
        while (t < localDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / localDuration);

            //// Quadratic Bezier arc: start -> peak -> end
            //float oneMinusK = 1f - k;
            //Vector3 pos =
            //    oneMinusK * oneMinusK * startPos +
            //    2f * oneMinusK * k * peakPos +
            //    k * k * endPos;

            //frag.position = pos;


            // --------------------------------------------------
            // NEW: Immediate separation + controlled arc
            // --------------------------------------------------

            // separation grows from the first frame (no sticking)
            float separation = Mathf.SmoothStep(0.15f, 1f, k);

            // vertical arc (clean up -> down motion)
            float y = Mathf.Sin(k * Mathf.PI) * height;

            // final position
            Vector3 pos =
                center +
                new Vector3(
                    offset2D.x * separation,
                    y - endDrop * k,
                    0f
                );

            frag.position = pos;


            // Phase A: Pop-in (tiny -> normal)
            if (doPopIn && t <= popTime)
            {
                float pin = Mathf.Clamp01(t / popTime);
                float eased = 1f - Mathf.Pow(1f - pin, 3f); // cubic ease-out

                frag.localScale = Vector3.Lerp(popStartScale, normalScale, eased);

                // Keep fully visible during pop-in
                if (mat != null)
                {
                    Color c = mat.color; c.a = 1f; mat.color = c;
                }
            }
            // Phase B: Fade-out + shrink (normal -> zero) AFTER pop-in completes
            else
            {
                float tf = Mathf.Clamp01((t - popTime) / fadeTime);

                frag.localScale = Vector3.Lerp(normalScale, Vector3.zero, tf);

                if (mat != null)
                {
                    Color c = mat.color;
                    c.a = Mathf.Lerp(1f, 0f, tf);
                    mat.color = c;
                }
            }

            // Spin
            if (Mathf.Abs(spinSpeed) > 0.01f)
                frag.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);

            yield return null;
        }

        if (frag != null)
            Destroy(frag.gameObject);
    }



    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------
    private void ApplyColor(Transform frag, Color col)
    {
        Renderer r = frag.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            Material m = new Material(r.material);
            m.color = col;
            r.material = m;
        }
    }

    private Color GetColor(int id)
    {
        return id switch
        {
            0 => blueColor,
            1 => crimsonColor,
            2 => greenColor,
            3 => lavenderColor,
            4 => midPinkColor,
            5 => orangeColor,
            6 => pinkColor,
            7 => purpleColor,
            8 => redColor,
            9 => yellowColor,
            _ => defaultColor
        };
    }
}



public class FractureObject3 : MonoBehaviour
{
    [Header("Prefab References")]
    public GameObject fracturedPrefab;      // The fractured object prefab (with fragments inside)
    public float explosionMinForce = 5f;
    public float explosionMaxForce = 80f;
    public float explosionRadius = 10f;
    public float upwardsModifier = 2f;
    public ForceMode explosionForceMode = ForceMode.Impulse;


    [Header("Fragment FX")]
    public float shrinkRate = 0.05f;      // shrink smoothly
    public float fadeDuration = 0.4f;     // fade duration
    public float delayBeforeFade = 0.3f;  // wait before fading

    [Header("Color Mapping")]
    public Color blueColor;
    public Color crimsonColor;
    public Color greenColor;
    public Color lavenderColor;
    public Color midPinkColor;
    public Color orangeColor;
    public Color pinkColor;
    public Color purpleColor;
    public Color redColor;
    public Color yellowColor;
    public Color defaultColor = Color.white;

    // ======================================================================
    //  PUBLIC EXPLODE (MAIN ENTRY)
    //  This is called by MatchResolver for each matched piece.
    //  originTransform = where to spawn explosion
    // ======================================================================
    public void Explode(Transform originTransform, int colorId)
    {
        if (fracturedPrefab == null)
        {
            Debug.LogError("FractureObject: fracturedPrefab is NOT assigned!");
            return;
        }

        // 1. Spawn fractured explosion object at origin
        GameObject fractured = Instantiate(
            fracturedPrefab,
            originTransform.position,
            originTransform.rotation
        );

        // determine fragment color
        Color fragColor = GetColor(colorId);

        // 2. Apply explosion and start shrinking/fading per fragment

        foreach (Transform fragA in fractured.transform)
        {
            foreach (Transform fragB in fractured.transform)
            {
                if (fragA == fragB) continue;

                Physics.IgnoreCollision(
                    fragA.GetComponent<Collider>(),
                    fragB.GetComponent<Collider>(),
                    true
                );
            }
        }

        foreach (Transform frag in fractured.transform)
        {
            //Rigidbody rb = frag.GetComponent<Rigidbody>();
            //if (rb != null)
            //{
            //    float force = Random.Range(explosionMinForce, explosionMaxForce);
            //    rb.AddExplosionForce(force, originTransform.position, explosionRadius, upwardsModifier, ForceMode.Impulse);
            //}

            Rigidbody rb = frag.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float force = Random.Range(explosionMinForce, explosionMaxForce);

                rb.AddExplosionForce(
                    force,
                   originTransform.position,
                    explosionRadius,
                    upwardsModifier,
                    explosionForceMode
                );

                rb.useGravity = true;
            }

            // Apply color to visible renderer
            ApplyColor(frag, fragColor);

            // Start shrink + fade
            StartCoroutine(ShrinkFragment(frag));
            StartCoroutine(FadeFragment(frag));
        }

        // Clean up fractured object after fragments are gone
        //Destroy(fractured, 4f);
    }
    //public void Explode1()
    //{
    //    if (originalObject == null || fracturedObject == null)
    //        return;



    //    // 1) Get ColorId from the parent piece (PieceSimple)
    //    int colorId = 0;
    //    Color fragColor = defaultFragmentColor;

    //    PieceSimple piece = originalObject.GetComponent<PieceSimple>();
    //    if (piece != null)
    //    {
    //        // Adjust this field name if your script uses a different one
    //        colorId = piece.ColorId;   // or piece.ColorId;
    //        fragColor = GetColorFromId(colorId);
    //    }

    //    // 2) Hide original
    //    //originalObject.SetActive(false);
    //    //HideOriginalVisuals();


    //    // 3) Spawn fractured version at same position/rotation
    //    fractObj = Instantiate(
    //        fracturedObject,
    //        originalObject.transform.position,
    //        originalObject.transform.rotation
    //    );

    //    // 4) For each fragment:
    //    foreach (Transform t in fractObj.transform)
    //    {
    //        // 4a) Apply explosion force
    //        Rigidbody rb = t.GetComponent<Rigidbody>();
    //        if (rb != null)
    //        {
    //            float force = Random.Range(explosionMinForce, explosionMaxForce);

    //            rb.AddExplosionForce(
    //                force,
    //                originalObject.transform.position,
    //                explosionForceRadius,
    //                explosionUpwardsModifier,
    //                explosionForceMode
    //            );

    //            rb.useGravity = true;
    //        }



    //        // 4b) Apply the color from ColorId
    //        ApplyColorToFragment(t, fragColor);

    //        // 4c) Start shrinking after delay
    //        StartCoroutine(Shrink(t, shrinkDelay));
    //        // fade starts at 0.5 sec and lasts 0.4 sec

    //        StartCoroutine(ConvertToFadeSpriteAndDestroy(t, 2f));

    //    }

    //    // 5) Optional VFX at explosion point
    //    if (explosionVFX != null)
    //    {
    //        GameObject exploVFX = Instantiate(
    //            explosionVFX,
    //            originalObject.transform.position,
    //            Quaternion.identity
    //        );
    //        Destroy(exploVFX, 7f);
    //    }

    //    // 6) Clean up parent holder after children are gone
    //    if (fractObj != null)
    //        Destroy(fractObj, 8f);
    //}


    // ======================================================================
    // SHRINK FRAGMENT (runs independently)
    // ======================================================================
    IEnumerator ShrinkFragment(Transform frag)
    {
        while (frag != null)
        {
            frag.localScale -= Vector3.one * shrinkRate;

            if (frag.localScale.x <= 0.05f)
                break;

            yield return null;
        }
    }

    // ======================================================================
    // FADE FRAGMENT (works even if original destroyed)
    // ======================================================================
    IEnumerator FadeFragment(Transform frag)
    {
        yield return new WaitForSeconds(delayBeforeFade);

        Renderer rend = frag.GetComponentInChildren<Renderer>();
        if (rend == null) yield break;

        // ensure unique material so objects fade independently
        Material mat = new Material(rend.material);
        rend.material = mat;

        Color start = mat.color;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = t / fadeDuration;

            Color c = start;
            c.a = Mathf.Lerp(1f, 0f, k);
            mat.color = c;

            yield return null;
        }

        // remove individual fragment after fade
        if (frag != null)
            Destroy(frag.gameObject);
    }

    // ======================================================================
    // COLOR MAPPING
    // ======================================================================
    Color GetColor(int id)
    {
        return id switch
        {
            0 => blueColor,
            1 => crimsonColor,
            2 => greenColor,
            3 => lavenderColor,
            4 => midPinkColor,
            5 => orangeColor,
            6 => pinkColor,
            7 => purpleColor,
            8 => redColor,
            9 => yellowColor,
            _ => defaultColor
        };
    }

    // ======================================================================
    // APPLY COLOR TO FRAGMENT
    // ======================================================================
    void ApplyColor(Transform frag, Color col)
    {
        Renderer r = frag.GetComponentInChildren<Renderer>();
        if (r != null && r.material != null)
        {
            Material m = new Material(r.material);
            m.color = col;
            r.material = m;
        }
    }
}



public class FractureObject2 : MonoBehaviour
{
    [Header("References")]
    public GameObject originalObject;      // Piece that will fracture
    public GameObject fracturedObject;     // Fractured prefab (children = fragments)
    public GameObject explosionVFX;        // Optional VFX

    [Header("Explosion settings")]
    public float explosionMinForce = 5f;
    public float explosionMaxForce = 80f;
    public float explosionForceRadius = 10f;
    public float explosionUpwardsModifier = 2f;
    public ForceMode explosionForceMode = ForceMode.Impulse;

    [Header("Fragment shrinking")]
    public float fragScaleFactor = 0.1f;   // How much to shrink per step
    public float shrinkDelay = 2f;         // Wait before starting shrink

    [Header("Fragment colors (mapped by ColorId)")]
    // 0
    public Color blueColor;
    // 1
    public Color crimsonColor;
    // 2
    public Color greenColor;
    // 3
    public Color lavenderColor;
    // 4
    public Color midPinkColor;
    // 5
    public Color orangeColor;
    // 6
    public Color pinkColor;
    // 7
    public Color purpleColor;
    // 8
    public Color redColor;
    // 9
    public Color yellowColor;

    public Color defaultFragmentColor = Color.white;

    private GameObject fractObj;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Explode();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Reset();
        }
    }

    public  void Explode()
    {
        if (originalObject == null || fracturedObject == null)
            return;

      

        // 1) Get ColorId from the parent piece (PieceSimple)
        int colorId = 0;
        Color fragColor = defaultFragmentColor;

        PieceSimple piece = originalObject.GetComponent<PieceSimple>();
        if (piece != null)
        {
            // Adjust this field name if your script uses a different one
            colorId = piece.ColorId;   // or piece.ColorId;
            fragColor = GetColorFromId(colorId);
        }

        // 2) Hide original
        //originalObject.SetActive(false);
        //HideOriginalVisuals();


        // 3) Spawn fractured version at same position/rotation
        fractObj = Instantiate(
            fracturedObject,
            originalObject.transform.position,
            originalObject.transform.rotation
        );

        // 4) For each fragment:
        foreach (Transform t in fractObj.transform)
        {
            // 4a) Apply explosion force
            Rigidbody rb = t.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float force = Random.Range(explosionMinForce, explosionMaxForce);

                rb.AddExplosionForce(
                    force,
                    originalObject.transform.position,
                    explosionForceRadius,
                    explosionUpwardsModifier,
                    explosionForceMode
                );

                rb.useGravity = true;
            }

        

            // 4b) Apply the color from ColorId
            ApplyColorToFragment(t, fragColor);

            // 4c) Start shrinking after delay
            StartCoroutine(Shrink(t, shrinkDelay));
            // fade starts at 0.5 sec and lasts 0.4 sec

            StartCoroutine(ConvertToFadeSpriteAndDestroy(t, 2f));

        }

        // 5) Optional VFX at explosion point
        if (explosionVFX != null)
        {
            GameObject exploVFX = Instantiate(
                explosionVFX,
                originalObject.transform.position,
                Quaternion.identity
            );
            Destroy(exploVFX, 7f);
        }

        // 6) Clean up parent holder after children are gone
        if (fractObj != null)
            Destroy(fractObj, 8f);
    }

    void Reset()
    {
        if (fractObj != null)
        {
            Destroy(fractObj);
        }

        if (originalObject != null)
        {
            originalObject.SetActive(true);
        }
    }

    IEnumerator Shrink(Transform t, float delay)
    {
        if (t == null)
            yield break;

        yield return new WaitForSeconds(delay);

        if (t == null)
            yield break;

        Vector3 newScale = t.localScale;

        while (t != null && newScale.x > 0f)
        {
            newScale -= new Vector3(fragScaleFactor, fragScaleFactor, fragScaleFactor);
            if (newScale.x < 0f)
                newScale = Vector3.zero;

            if (t == null)
                yield break;

            t.localScale = newScale;

            yield return new WaitForSeconds(0.05f);
        }

        if (t != null)
            Destroy(t.gameObject);
    }

    // ----------------- COLOR LOGIC -----------------

    // Map ColorId (0–9) to the correct color
    Color GetColorFromId(int id)
    {
        switch (id)
        {
            case 0: return blueColor;      // Blue 1X / 2X / 3X
            case 1: return crimsonColor;   // Crimson
            case 2: return greenColor;     // Green
            case 3: return lavenderColor;  // Lavender
            case 4: return midPinkColor;   // Mid Pink
            case 5: return orangeColor;    // Orange
            case 6: return pinkColor;      // Pink
            case 7: return purpleColor;    // Purple
            case 8: return redColor;       // Red
            case 9: return yellowColor;    // Yellow
            default: return defaultFragmentColor;
        }
    }

    // Apply color to SpriteRenderer or 3D Renderer
    void ApplyColorToFragment(Transform frag, Color color)
    {
        if (frag == null) return;

        // For 2D sprites
        SpriteRenderer sr = frag.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = color;
            return;
        }

        // For 3D meshes or other renderers
        Renderer rend = frag.GetComponentInChildren<Renderer>();
        if (rend != null && rend.material != null)
        {
            rend.material.color = color;
        }
    }
    void HideOriginalVisuals()
    {
        SpriteRenderer[] renderers = originalObject.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
            sr.enabled = false;
    }


    IEnumerator ConvertToFadeSpriteAndDestroy(Transform frag, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (frag == null) yield break;

        // Get 3D mesh renderer
        Renderer r = frag.GetComponentInChildren<Renderer>();
        if (r == null) yield break;

        // Create a GameObject with SpriteRenderer
        GameObject sObj = new GameObject("FragSpriteFade");
        sObj.transform.position = frag.position;
        sObj.transform.localScale = frag.localScale * 1.0f;

        SpriteRenderer sr = sObj.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 9999;

        // Generate a flat sprite using the fragment’s material color
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, r.material.color);
        tex.Apply();

        Sprite spr = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        sr.sprite = spr;

        // Hide the 3D fragment
        r.enabled = false;

        // Fade out the sprite
        float t = 0f;
        float duration = 0.5f;
        Color startColor = sr.color;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;

            Color c = startColor;
            c.a = 1f - k;
            sr.color = c;

            yield return null;
        }

        Destroy(sObj);
        Destroy(frag.gameObject);
    }



}


public class FractureObject1 : MonoBehaviour
{
    [Header("References")]
    public GameObject originalObject;     // solid object
    public GameObject fracturedObject;    // fractured prefab (children = fragments with colliders + rigidbodies)
    public GameObject explosionVFX;       // optional VFX prefab

    [Header("Explosion settings")]
    public float explosionMinForce = 5f;
    public float explosionMaxForce = 80f;
    public float explosionForceRadius = 10f;
    public float timeToDestroty = 1f;

    // This makes the explosion push fragments UP as well as outwards
    // (bigger value = stronger upward kick)
    public float explosionUpwardsModifier = 2f;

    // Use impulse so it’s an instant “kick” like a firework
    public ForceMode explosionForceMode = ForceMode.Impulse;

    [Header("Fragment shrinking")]
    public float fragScaleFactor = 0.1f;  // how much to shrink per step

    private GameObject fractObj;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Explode();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Reset();
        }
    }

    void Explode()
    {
        if (originalObject == null || fracturedObject == null)
            return;

        // Hide the original object
        originalObject.SetActive(false);

        // Spawn fractured version exactly where the original was
        fractObj = Instantiate(
            fracturedObject,
            originalObject.transform.position,
            originalObject.transform.rotation
        );

        // For each fragment, add an upward-biased explosion impulse
        foreach (Transform t in fractObj.transform)
        {
            Rigidbody rb = t.GetComponent<Rigidbody>();

            if (rb != null)
            {
                float force = Random.Range(explosionMinForce, explosionMaxForce);

                rb.AddExplosionForce(
                    force,
                    originalObject.transform.position,
                    explosionForceRadius,
                    explosionUpwardsModifier,
                    explosionForceMode
                );

                // Make sure gravity is enabled so fragments fall back down
                rb.useGravity = true;
            }

            // Start shrinking each fragment after some delay
            StartCoroutine(Shrink(t, 0.5f));
        }

        // Destroy all fragments after a few seconds
        Destroy(fractObj, timeToDestroty);

        // Optional VFX at the explosion point
        if (explosionVFX != null)
        {
            GameObject exploVFX = Instantiate(
                explosionVFX,
                originalObject.transform.position,
                Quaternion.identity
            );
            Destroy(exploVFX, 7f);
        }
    }

    void Reset()
    {
        if (fractObj != null)
        {
            Destroy(fractObj);
        }

        if (originalObject != null)
        {
            originalObject.SetActive(true);
        }
    }

    IEnumerator Shrink(Transform t, float delay)
    {
        if (t == null)
            yield break;

        yield return new WaitForSeconds(delay);

        // Cache current scale once we are sure Transform still exists
        if (t == null)
            yield break;

        Vector3 newScale = t.localScale;

        // Shrink until almost zero or until object is destroyed
        while (t != null && newScale.x > 0f)
        {
            newScale -= new Vector3(fragScaleFactor, fragScaleFactor, fragScaleFactor);

            if (newScale.x < 0f)
                newScale = Vector3.zero;

            // Check again before accessing
            if (t == null)
                yield break;

            t.localScale = newScale;

            yield return new WaitForSeconds(0.05f);
        }

        // Finally destroy this fragment (if it still exists)
        if (t != null)
            Destroy(t.gameObject);
    }
}

