using UnityEngine;

public class CameraBoardDebug : MonoBehaviour
{
    public Camera cam;
    public Transform boardRoot;   // the parent transform of the whole board

    void Start()
    {
        // Angle between board normal and camera view
        float angle = Vector3.Angle(boardRoot.forward, -cam.transform.forward);
        Debug.Log($"[DBG] Board↔Camera angle: {angle:F1}° (0° means board faces camera head-on)");

        // Camera rotation relative to board (Euler)
        var rel = Quaternion.Inverse(boardRoot.rotation) * cam.transform.rotation;
        Debug.Log($"[DBG] Cam vs Board (Euler): {rel.eulerAngles}");
    }
}
