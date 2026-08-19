using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BackButtonRelay : MonoBehaviour
{
    public UnitsPanelController panel; // assign once (e.g., the Units root)

    void Awake()
    {
        var btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => panel.ShowCardsScreenPublic());
    }
}
