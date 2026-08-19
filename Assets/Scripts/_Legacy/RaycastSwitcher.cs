using UnityEngine;
using UnityEngine.EventSystems;

public class RaycastSwitcher : MonoBehaviour
{
    public Camera tdCam;
    public Camera boardCam;
    private PhysicsRaycaster tdRay;
    private PhysicsRaycaster boardRay;

    void Awake()
    {
        tdRay = tdCam.GetComponent<PhysicsRaycaster>();
        boardRay = boardCam.GetComponent<PhysicsRaycaster>();
    }

    void Update()
    {
        bool overBoard = boardCam.pixelRect.Contains(Input.mousePosition);
        if (tdRay) tdRay.enabled = !overBoard;
        if (boardRay) boardRay.enabled = overBoard;
    }
}
