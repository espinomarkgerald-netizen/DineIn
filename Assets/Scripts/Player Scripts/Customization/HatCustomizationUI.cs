using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Photon.Pun;

public class HatCustomizationUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button hatLeft;
    public Button hatRight;

    [Header("Optional (Menu Preview)")]
    [Tooltip("Drag the PREVIEW character's CharacterHatCustomizer here for menu. Leave empty for in-game (local Photon player).")]
    public CharacterHatCustomizer previewCustomizer;

    private CharacterHatCustomizer targetCustomizer;

    void Start()
    {
        TryUsePreviewOrFindPhoton();
    }

    void TryUsePreviewOrFindPhoton()
    {
        // MENU: Use preview if assigned
        if (previewCustomizer != null)
        {
            targetCustomizer = previewCustomizer;
            HookupButtons();
            Debug.Log("✅ Hat UI controlling PREVIEW character");
            return;
        }

        // IN-GAME: Find local Photon player
        StartCoroutine(FindLocalPhotonPlayerRoutine());
    }

    IEnumerator FindLocalPhotonPlayerRoutine()
    {
        while (targetCustomizer == null)
        {
            foreach (var player in FindObjectsOfType<CharacterHatCustomizer>())
            {
                if (player != null && player.photonView != null && player.photonView.IsMine)
                {
                    targetCustomizer = player;
                    HookupButtons();
                    Debug.Log("✅ Hat UI controlling LOCAL PHOTON player");
                    yield break;
                }
            }
            yield return null;
        }
    }

    void HookupButtons()
    {
        if (targetCustomizer == null)
        {
            Debug.LogError("❌ Hat UI has no targetCustomizer!");
            return;
        }

        SafeHook(hatLeft, () => targetCustomizer.PreviousHat());
        SafeHook(hatRight, () => targetCustomizer.NextHat());
    }

    void SafeHook(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null)
        {
            Debug.LogError("❌ Hat button not assigned in Inspector");
            return;
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }
}
