using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class FastFoodCookingView
{
    public Camera TutorialCamera => opened && activeStation != null ? stationCamera : null;
    public bool TutorialCameraSettled => !cameraMoving && stationTransition == null;
    public RectTransform TutorialHotbar => hotbarContainer;
    public RectTransform TutorialDiscard => discardBox;
    public bool TutorialPrepTransferred => prepPickups.Count == 0;
    public bool TutorialPreparing => prepView && TutorialCameraSettled;
    public void HoldTutorialRackView()
    {
        // Extend only the native collection glance while this tutorial explains
        // the rack. The normal view resumes as soon as that lesson ends.
        if (FastFoodTutorialBridge.Active && opened && State.Mode == FastFoodStationMode.Fry)
            fryerPrepUntil = Mathf.Max(fryerPrepUntil, Time.unscaledTime + prepLookSeconds);
    }
    public RectTransform TutorialStationArrow(FastFoodStationMode destination)
    {
        // Same enum ring as SwitchStation; selection has real Enter buttons instead.
        if (State.Mode == FastFoodStationMode.None) return TutorialControl("Enter " + destination);
        if (State.Mode == destination) return null;
        int next = ((int)State.Mode - 1 + 1 + stations.Length) % stations.Length + 1;
        return TutorialControl(next == (int)destination ? "Next Station" : "Previous Station");
    }
    public RectTransform TutorialControl(string key)
    {
        RectTransform result = key == "Serve" ? (RectTransform)serve.transform :
            key == "Hotbar" ? hotbarContainer : key == "Selection" ? selection :
            key == "Header" ? heading.transform.parent as RectTransform :
            key == "Ticket" ? ticketLabels.FirstOrDefault(e => e.ticket == State.PlayerTicket && e.view != null && e.view.gameObject.activeInHierarchy).view?.transform as RectTransform :
            key == "Restock" ? notification :
            canvas.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == key)?.transform as RectTransform;
        return result != null && result.gameObject.activeInHierarchy ? result : null;
    }
    public Transform TutorialSource(ItemData item, FastFoodCookingState.Portion portion, bool raw)
    {
        if (item != null)
            return productionCells.FirstOrDefault(c => c.slot != null && c.slot.gameObject.activeInHierarchy && c.slot.item == item && c.raw == raw).slot?.transform;
        if (State.Mode == FastFoodStationMode.Assembler && portion != null)
            return hotbar.GetComponentsInChildren<FastFoodCookingDragHandle>()
                .FirstOrDefault(h => h.isActiveAndEnabled && h.portion == portion)?.transform;
        // Fryer food lives under the appliance's moving basket, outside the rig.
        // This is the same current-visual list used by real collection.
        return portion != null ? cookingVisuals.FirstOrDefault(v => v.portion == portion &&
            v.root != null && v.root.gameObject.activeInHierarchy)?.root : null;
    }
    public Transform TutorialTarget(int kind, int slot)
    {
        if (activeStation == null) return null;
        if (kind == 2) return discardBox;
        if (kind == 1 && State.Mode == FastFoodStationMode.Assembler) return prepTarget != null ? prepTarget.transform : null;
        var targets = kind == 1 ? activeStation.PrepTargets : activeStation.Slots;
        return targets.FirstOrDefault(t => t.slotIndex == slot)?.transform;
    }
    public Transform TutorialBasket(int slot) => activeStation?.baskets.FirstOrDefault(b => b != null && b.isActiveAndEnabled && b.firstSlot == slot / 2 * 2)?.transform;
    public Transform TutorialSubject(string subject, int slot)
    {
        if (activeStation == null) return null;
        if (subject == "Cooking") return TutorialTarget(0, slot);
        if (subject == "Prep" || subject == "Tray") return TutorialTarget(1, slot);
        if (subject == "Basket") return TutorialBasket(slot);
        if (subject == "Rack") return activeStation.preparation != null ? activeStation.preparation.transform : null;
        return null;
    }
    // Read-only geometry for this tutorial. Uses the same contact/footprint as real drops.
    public bool TryTutorialBounds(Transform target, FastFoodCookingState.Portion portion, out Bounds bounds, out Transform space)
    {
        bounds = default; space = null;
        if (activeStation == null || target == null) return false;
        if (cookingVisuals.Any(v => v.root == target))
        {
            bounds = FoodBounds(target.gameObject); return bounds.size.sqrMagnitude > 0;
        }
        if (activeStation.baskets.Any(b => b != null && b.transform == target) &&
            target.TryGetComponent<BoxCollider>(out var basketBox))
        {
            bounds = new Bounds(basketBox.center, basketBox.size); space = target; return true;
        }
        if (!target.IsChildOf(activeStation.transform)) return false;
        if (IsAssembler && prepTarget != null && target == prepTarget.transform)
        {
            space = AssemblySurface;
            Vector2 size = activeStation.assemblyArea;
            Vector3 center = Vector3.zero;
            if (portion != null && State.PlayerTicket != null && State.PlayerTicket.portions.Contains(portion))
                center = space.InverseTransformPoint(AssemblyContact(portion, out size));
            bounds = new Bounds(center, new Vector3(size.x, .02f, size.y));
            return true;
        }
        if (target.TryGetComponent<FastFoodCookingDropTarget>(out _) && target.TryGetComponent<BoxCollider>(out var box))
        {
            bounds = new Bounds(box.center, box.size); space = box.transform; return true;
        }
        if (target.TryGetComponent<FastFoodCookingDragHandle>(out _))
        {
            bounds = FoodBounds(target.gameObject); return true;
        }
        return false;
    }
    public void ShowTutorialRestock() { RefreshTutorialView(); ShowNotice(true); }
    public void RefreshTutorialView() { lastSignature = null; Refresh(); }
}
