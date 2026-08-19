using UnityEngine;

[DisallowMultipleComponent]
public class Warrior : MonoBehaviour
{
    public PieceSimple Home { get; private set; }
    public bool IsDetached { get; private set; } = false;

    void Start()
    {
        if (!Home)
        {
            Home = GetComponentInParent<PieceSimple>();
            //if (Home != null) Home.RegisterWarrior(this);
        }
    }

    public void SetHome(PieceSimple home) => Home = home;

    // Call this when you want the warrior to leave its parent piece
    public void Detach(Transform newParent = null)
    {
        transform.SetParent(newParent, worldPositionStays: true);
        IsDetached = true;            // <-- your boolean becomes true here
        Home = null;
    }
}
