using System;
using UnityEngine;

namespace DineIn.Appearance
{
    [CreateAssetMenu(menuName = "Dine In/Employee Uniforms")]
    public sealed class EmployeeUniforms : ScriptableObject
    {
        [Serializable] public sealed class Assignment
        {
            public EmployeeRole role;
            public string bodyId, outfitId, requiredHatId;
        }
        public Assignment[] assignments = Array.Empty<Assignment>();
        public AppearanceRecipe Apply(AppearanceRecipe personal, EmployeeRole role, AppearanceCatalog catalog)
        {
            var result = catalog.Validate(personal);
            var match = Array.Find(assignments, x => x.role == role && x.bodyId == result.bodyId);
            if (match != null)
            {
                result.outfitId = match.outfitId;
                if (!string.IsNullOrEmpty(match.requiredHatId)) result.hatId = match.requiredHatId;
            }
            return catalog.Validate(result);
        }
    }

    public static class EmployeeAppearance
    {
        private static EmployeeUniforms casual, fastFood;
        public static void Ensure(EmployeeData employee)
        {
            if (employee == null || MultiplayerRestaurantBridge.IsObserver) return;
            // JsonUtility can materialize an empty nested recipe for pre-customization saves.
            if (employee.appearance != null && employee.appearance.version == 2 && !string.IsNullOrWhiteSpace(employee.appearance.bodyId)) return;
            var catalog = AppearanceCatalog.Load();
            if (catalog != null) employee.appearance = catalog.GenerateEmployee(employee.EmployeeID);
        }
        public static void Bind(GameObject worker, EmployeeData employee, EmployeeRole role, bool isFastFood)
        {
            if (worker == null || employee == null) return;
            Ensure(employee);
            if (employee.appearance == null || employee.appearance.version != 2 || string.IsNullOrWhiteSpace(employee.appearance.bodyId)) return; // Observers wait for the host's roster, never roll traits.
            var look = worker.GetComponent<CharacterAppearance>();
            if (look == null) return; // Only explicitly authored compatible visual roots participate.
            if (casual == null) casual = Resources.Load<EmployeeUniforms>("CasualEmployeeUniforms");
            if (fastFood == null) fastFood = Resources.Load<EmployeeUniforms>("FastFoodEmployeeUniforms");
            var uniforms = isFastFood ? fastFood : casual;
            look.Apply(uniforms != null ? uniforms.Apply(employee.appearance, role, look.Catalog) : employee.appearance);
        }
    }
}
