using UnityEngine;

[System.Serializable]
public struct ChildLocalSnapshot
{
    public Transform t;
    public Vector3 localPos;
    public Quaternion localRot;
    public Vector3 localScale;
}
