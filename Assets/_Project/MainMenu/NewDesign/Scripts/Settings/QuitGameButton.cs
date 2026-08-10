using UnityEngine;

public class QuitGameButton : MonoBehaviour
{
    public void QuitGame()
    {
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false; // stop Play mode in the Editor
        #else
                Application.Quit(); // actually closes the app in a real build
        #endif
    }
}