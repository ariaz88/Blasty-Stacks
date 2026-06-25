using UnityEngine;
using UnityEngine.SceneManagement;

public class DevResetButton : MonoBehaviour
{
    // Hook this to a UI Button’s OnClick in your Home scene (hide in release builds)
    public void OnClickResetAll()
    {
        SaveSystem.ResetAll();
        // Reload current scene so all systems rebuild from fresh save
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
