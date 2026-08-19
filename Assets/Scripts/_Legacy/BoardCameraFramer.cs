using UnityEngine;

[DisallowMultipleComponent]
public class BoardCameraFramer : MonoBehaviour
{
    public enum ProjectionMode { Orthographic, Perspective }

    [Header("References")]
    [SerializeField] private BoardGridXY board;
    [SerializeField] private Camera cam;

    [Header("Framing")]
    [SerializeField] private ProjectionMode mode = ProjectionMode.Perspective;
    [SerializeField] private float padding = 0.25f;
    [SerializeField] private float pitch = 65f;
    [SerializeField] private float yaw = 0f;

    [Header("Perspective Tuning")]
    [SerializeField] private float verticalFOV = 30f;
    [Min(1.001f)] [SerializeField] private float maxScaleRatio = 1.01f; // tighter for more uniform view

    [Header("Run Mode")]
    [SerializeField] private bool autoFitOnPlay = true;     // run once in Start
    [SerializeField] private bool liveInEditMode = false;   // re-fit on inspector edits in Edit mode
    [SerializeField] private bool liveWhilePlaying = false; // re-fit on inspector edits in Play mode
    [SerializeField] private bool disableAfterFit = true;   // stop after first fit
    [SerializeField] private bool respectParentPosition = true; // don't move camera if it's a child

    [Header("Offsets")]
    [Tooltip("Shift the look target along camera local axes (R,U,F).")]
    [SerializeField] private Vector3 localAimOffset = Vector3.zero;
    [Tooltip("Preserve your manual side/height offset (keeps your Y/Z feel).")]
    [SerializeField] private bool preserveLateralOffset = true;
    [Tooltip("Extra nudge after fitting, in camera local axes.")]
    [SerializeField] private Vector3 localCameraOffset = Vector3.zero;

    void Reset()
    {
        cam = GetComponent<Camera>();
        if (!board) board = FindObjectOfType<BoardGridXY>();
    }

    void Awake() { if (!cam) cam = GetComponent<Camera>(); }

    void Start()
    {
        if (autoFitOnPlay)
        {
            FitNow();
            //if (disableAfterFit) enabled = false;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!cam) cam = GetComponent<Camera>();
        if (!enabled) return;
        if (!Application.isPlaying && liveInEditMode) FitNow();
        if (Application.isPlaying && liveWhilePlaying) FitNow();
    }
#endif

    [ContextMenu("Fit Now")]
    public void FitNow()
    {
        if (!board || !cam) return;

        // If camera is a child and we want to respect parent positioning, only adjust FOV/rotation
        if (respectParentPosition && transform.parent != null)
        {
            // Only adjust camera settings, don't move the camera
            cam.orthographic = (mode == ProjectionMode.Orthographic);

            if (mode == ProjectionMode.Perspective)
            {
                cam.fieldOfView = verticalFOV;
            }

            // Keep camera at local position (0,0,0) - don't move it
            transform.localPosition = Vector3.zero;

            // Optionally adjust rotation if needed
            Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = targetRot;

            return;
        }

        // Original fitting logic for cameras without parents
        // Compute board center and desired camera basis from pitch/yaw
        float w = board.Width * board.CellSize;
        float h = board.Height * board.CellSize;
        Vector3 center = board.transform.TransformPoint(new Vector3(w * 0.5f, 0f, h * 0.5f));

        Quaternion desiredRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 right = desiredRot * Vector3.right;
        Vector3 up = desiredRot * Vector3.up;
        Vector3 fwd = desiredRot * Vector3.forward;

        // Aim offset (local to camera)
        center += right * localAimOffset.x + up * localAimOffset.y + fwd * localAimOffset.z;

        // Read current lateral offset (keep your manual Y/Z feel)
        float r0 = 0f, u0 = 0f;
        if (preserveLateralOffset)
        {
            Vector3 v0 = transform.position - center;
            r0 = Vector3.Dot(right, v0);
            u0 = Vector3.Dot(up, v0);
        }

        // Apply projection + distance to fit board with minimal foreshortening
        transform.rotation = desiredRot;

        if (mode == ProjectionMode.Orthographic)
        {
            cam.orthographic = true;
            transform.position = center - fwd * 10f + right * r0 + up * u0;
            Vector2 half = ExtentsOnAxes(center, right, up) + Vector2.one * padding;
            cam.orthographicSize = Mathf.Max(half.y, half.x / cam.aspect);
        }
        else
        {
            cam.orthographic = false;
            cam.fieldOfView = verticalFOV;

            // Much closer distance calculation for proper BlastyStacks-style view
            float boardSize = Mathf.Max(w, h);
            float vFovRad = verticalFOV * Mathf.Deg2Rad;

            // Calculate distance based on board size to fit nicely in view
            float distance = (boardSize * 0.5f) / Mathf.Tan(vFovRad * 0.5f);

            // Much smaller padding and closer limits for mobile-style view
            distance = distance * (1f + padding * 0.5f) + 1f; // minimal extra distance
            distance = Mathf.Clamp(distance, 3f, 15f); // much closer limits

            // Position camera at the calculated distance
            Vector3 targetWorldPos = center + right * r0 + up * u0 - fwd * distance;

            // If camera has a parent, convert world position to local position
            if (transform.parent != null)
            {
                transform.localPosition = transform.parent.InverseTransformPoint(targetWorldPos);
            }
            else
            {
                transform.position = targetWorldPos;
            }
        }

        // Optional final nudge
        Vector3 offsetWorldPos = right * localCameraOffset.x + up * localCameraOffset.y + fwd * localCameraOffset.z;

        // Apply offset in local space if camera has a parent
        if (transform.parent != null)
        {
            Vector3 currentWorldPos = transform.parent.TransformPoint(transform.localPosition);
            Vector3 newWorldPos = currentWorldPos + offsetWorldPos;
            transform.localPosition = transform.parent.InverseTransformPoint(newWorldPos);
        }
        else
        {
            transform.position += offsetWorldPos;
        }
    }

    private Vector2 ExtentsOnAxes(Vector3 center, Vector3 right, Vector3 up)
    {
        float minR = float.PositiveInfinity, maxR = float.NegativeInfinity;
        float minU = float.PositiveInfinity, maxU = float.NegativeInfinity;
        foreach (var c in BoardWorldCorners())
        {
            Vector3 q = c - center;
            float r = Vector3.Dot(right, q);
            float u = Vector3.Dot(up, q);
            minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
            minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
        }
        return new Vector2(Mathf.Max(Mathf.Abs(minR), Mathf.Abs(maxR)),
                           Mathf.Max(Mathf.Abs(minU), Mathf.Abs(maxU)));
    }

    private Vector3[] BoardWorldCorners()
    {
        float w = board.Width * board.CellSize;
        float h = board.Height * board.CellSize;
        Transform t = board.transform;
        return new[]
        {
            t.TransformPoint(new Vector3(0f, 0f, 0f)),
            t.TransformPoint(new Vector3(w, 0f, 0f)),
            t.TransformPoint(new Vector3(0f, 0f, h)),
            t.TransformPoint(new Vector3(w, 0f, h))
        };
    }
}


