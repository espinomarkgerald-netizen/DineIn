using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;

public class CharacterCustomizationUI : MonoBehaviour
{
    [Header("Buttons (assign in Inspector)")]
    public Button headLeft;
    public Button headRight;
    public Button bodyLeft;
    public Button bodyRight;
    public Button armsLeft;
    public Button armsRight;
    public Button legsLeft;
    public Button legsRight;

    [Header("Optional (Menu Preview)")]
    [Tooltip("Drag the PREVIEW character's CharacterColorCustomizer here for menu. Leave empty for in-game (local Photon player).")]
    public CharacterColorCustomizer previewCustomizer;

    private CharacterColorCustomizer targetCustomizer;

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
            ValidateButtons();
            HookupButtons();
            Debug.Log("✅ Color UI controlling PREVIEW character");
            return;
        }

        // IN-GAME: Find local Photon player
        StartCoroutine(FindLocalPhotonPlayerRoutine());
    }

    IEnumerator FindLocalPhotonPlayerRoutine()
    {
        while (targetCustomizer == null)
        {
            foreach (var player in FindObjectsOfType<CharacterColorCustomizer>())
            {
                if (player != null && player.photonView != null && player.photonView.IsMine)
                {
                    targetCustomizer = player;
                    Debug.Log("✅ Local Photon color customizer found");
                    ValidateButtons();
                    HookupButtons();
                    yield break;
                }
            }
            yield return null;
        }
    }

    void ValidateButtons()
    {
        if (!headLeft || !headRight ||
            !bodyLeft || !bodyRight ||
            !armsLeft || !armsRight ||
            !legsLeft || !legsRight)
        {
            Debug.LogError("❌ One or more UI Buttons are NOT assigned in the Inspector!");
        }
    }

    void HookupButtons()
    {
        if (targetCustomizer == null)
        {
            Debug.LogError("❌ Cannot hook buttons, targetCustomizer is NULL");
            return;
        }

        SafeHook(headLeft, () => targetCustomizer.ChangeHead(-1));
        SafeHook(headRight, () => targetCustomizer.ChangeHead(1));

        SafeHook(bodyLeft, () => targetCustomizer.ChangeBody(-1));
        SafeHook(bodyRight, () => targetCustomizer.ChangeBody(1));

        SafeHook(armsLeft, () => targetCustomizer.ChangeArms(-1));
        SafeHook(armsRight, () => targetCustomizer.ChangeArms(1));

        SafeHook(legsLeft, () => targetCustomizer.ChangeLegs(-1));
        SafeHook(legsRight, () => targetCustomizer.ChangeLegs(1));

        Debug.Log("✅ UI buttons hooked successfully");
    }

    void SafeHook(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null)
        {
            Debug.LogError("❌ Button reference missing!");
            return;
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }
}
