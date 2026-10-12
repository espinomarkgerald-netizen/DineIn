using UnityEngine;
using System.Linq;

/// <summary>Uses existing day-based recipe unlocks; dining furniture stays authored in the scene.</summary>
public sealed class FineDiningProgression : MonoBehaviour
{
    public Recipe[] recipes;
    public FineDiningTable[] tables;
    private int lastDay=-1;
    private float nextCheck;
    private void Update()
    {
        // The host advances room unlocks; guests receive them in the shared management state.
        if (gameObject.scene.name == FineDiningScene.Multiplayer
            && (!FineDiningMultiplayerSession.IsAuthority || !MultiplayerProgressionContext.Ready)) return;
        if(Time.unscaledTime<nextCheck || GameSaveManager.Instance==null || !GameSaveManager.Instance.HasCompletedInitialLoad || GameSaveManager.Instance.IsApplyingSave)return;
        nextCheck=Time.unscaledTime+1f;
        int day=GameFlowManager.Instance!=null?GameFlowManager.Instance.CurrentDay:1;
        if(day==lastDay)return;
        var reservations=FineDiningReservations.For(this);reservations?.EnsureDay();
        int announced=reservations!=null?reservations.ProgressionAnnouncedDay:0;
        var activeRecipes = MenuCatalog.ForScene(gameObject.scene.name)?.Products;
        foreach(var recipe in (activeRecipes ?? recipes).Where(r=>r!=null && r.availableOnMenu && r.dayToUnlock<=day))
        {
            if(UnlockManager.Instance!=null && !UnlockManager.Instance.IsRecipeUnlocked(recipe.recipeID))
            {
                UnlockManager.Instance.UnlockRecipe(recipe.recipeID,recipe.kitchenItemType);
                foreach(var ingredient in recipe.ingredients)if(ingredient.item!=null)UnlockManager.Instance.UnlockIngredient(ingredient.item);
                if(recipe.dayToUnlock>1)UnlockCelebrationManager.EnsureInstance().QueueRecipe(recipe.recipeID);
            }
        }
        if(announced>0)foreach(var table in tables.Where(t=>t!=null && t.unlockDay>announced && t.unlockDay<=day))WarningSlideUI.Instance?.Show(table.displayName+(table.Owned?" is now available for dining and reservations.":" is now available in Equipment."));
        lastDay=day;
        if(announced!=day)reservations?.MarkProgressionAnnounced(day);
    }
}

