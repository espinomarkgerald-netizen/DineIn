using UnityEngine;

// This creates the actual dropdown menu for the Job Titles!
public enum KitchenRole {
    PrepCook,
    LineCook,
    Assembler
}

public class KitchenRoleManager : MonoBehaviour {
    public static KitchenRoleManager Instance;

    [Header("The Three Physical Chefs")]
    public KitchenPlayerMovement prepCook;
    public KitchenPlayerMovement lineCook;
    public KitchenPlayerMovement assembler;

    void Awake() {
        Instance = this;
    }

    void Start() {
        // When the game starts, automatically take control of the Prep Cook
        SwitchRole(KitchenRole.PrepCook);
    }

    // --- THESE ARE FOR YOUR UI BUTTONS ---
    public void Button_SelectPrepCook() { SwitchRole(KitchenRole.PrepCook); }
    public void Button_SelectLineCook() { SwitchRole(KitchenRole.LineCook); }
    public void Button_SelectAssembler() { SwitchRole(KitchenRole.Assembler); }

    /// <summary>Forces a specific role to be active. Used by KitchenTutorialManager during guided phases.</summary>
    public void ForceRole(KitchenRole role) { SwitchRole(role); }

    /// <summary>Enables all three roles so the player can freely switch. Used during free-play and Day 4.</summary>
    public void UnlockAllRoles() {
        // All three chefs become independently clickable; the currently active one stays active.
        prepCook.isActivePlayer  = prepCook.isActivePlayer  || false;
        lineCook.isActivePlayer  = lineCook.isActivePlayer  || false;
        assembler.isActivePlayer = assembler.isActivePlayer || false;
        // Re-enable all role buttons by simply leaving isActivePlayer as-is.
        // The UI buttons call SwitchRole which will correctly transfer control.
    }

    private void SwitchRole(KitchenRole newRole) {
        // 1. Turn off everyone's controls
        prepCook.isActivePlayer = false;
        lineCook.isActivePlayer = false;
        assembler.isActivePlayer = false;

        // 2. Stop them in their tracks so they don't keep walking
        prepCook.StopMovement();
        lineCook.StopMovement();
        assembler.StopMovement();

        // 3. Deselect all indicators
        SetIndicator(prepCook, false);
        SetIndicator(lineCook, false);
        SetIndicator(assembler, false);

        // 4. Turn on the controls and indicator ONLY for the selected role
        if (newRole == KitchenRole.PrepCook) {
            prepCook.isActivePlayer = true;
            SetIndicator(prepCook, true);
            Debug.Log("Now controlling: PREP COOK");
        } else if (newRole == KitchenRole.LineCook) {
            lineCook.isActivePlayer = true;
            SetIndicator(lineCook, true);
            Debug.Log("Now controlling: LINE COOK");
        } else if (newRole == KitchenRole.Assembler) {
            assembler.isActivePlayer = true;
            SetIndicator(assembler, true);
            Debug.Log("Now controlling: ASSEMBLER");
        }
    }

    /// <summary>Shows or hides the UFO role indicator above a chef.</summary>
    private void SetIndicator(KitchenPlayerMovement chef, bool selected) {
        if (chef == null) return;
        var indicator = chef.GetComponent<RoleIndicator>();
        if (indicator != null)
            indicator.SetSelected(selected);
    }
}