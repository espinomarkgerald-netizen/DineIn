#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class FineDiningDrinkAuthoring
{
    const string Folder = "Assets/_Project/Restaurant/Assets/Fine Dining Drink Service";
    public static string Apply(Scene scene)
    {
        if (Application.isPlaying || scene.name != "Lobby3") throw new InvalidOperationException("Author Lobby3 in Edit Mode only.");
        var objects = scene.GetRootGameObjects();
        var settings = objects.SelectMany(o => o.GetComponentsInChildren<FineDiningReservations>(true)).Single();
        var kitchen = objects.SelectMany(o => o.GetComponentsInChildren<KitchenManager>(true)).Single();
        System.IO.Directory.CreateDirectory(Folder);
        var bottle = CreateBottle();
        settings.wineBottlePrefab = bottle;
        settings.wineBottleHandOffset = new Vector3(0f, .04f, 0f);
        settings.wineBottleHandRotation = new Vector3(0f, 0f, 0f);
        settings.wineBottleHandScale = Vector3.one;
        var wine = AssetDatabase.LoadAssetAtPath<Recipe>("Assets/_Project/Office/Manager/Recipe/Fine Dining/Red Wine.asset");
        if (wine == null) throw new InvalidOperationException("Red Wine recipe missing.");
        settings.wineRefillProducts = new[] { wine };
        wine.servingPrefab = CreateGlass();
        wine.normalizedServingTransform = true;
        wine.servingScale = Vector3.one;
        wine.servingRotation = wine.servingPositionOffset = Vector3.zero;
        EditorUtility.SetDirty(wine);
        var root = objects.FirstOrDefault(o => o.name == "Fine Dining Drink Service");
        if (root == null) { root = new GameObject("Fine Dining Drink Service"); SceneManager.MoveGameObjectToScene(root, scene); }
        var pickup = Child(root.transform, "BarDrinkPickupApproach");
        if (!NavMesh.SamplePosition(new Vector3(-8.8f, .62f, -66.9f), out var hit, 3f, NavMesh.AllAreas))
            throw new InvalidOperationException("Bar pickup has no nearby NavMesh.");
        pickup.position = hit.position;
        pickup.rotation = Quaternion.Euler(0, 180, 0);
        var left = Child(root.transform, "BarDrinkOutput1");
        var right = Child(root.transform, "BarDrinkOutput2");
        left.position = new Vector3(-10.2f, 3.84f, -70.1f);
        right.position = new Vector3(-7.8f, 3.84f, -70.1f);
        kitchen.drinkTraySpawnPoints = new[] { left, right };
        kitchen.drinkPickupApproach = pickup;
        var card = objects.SelectMany(o => o.GetComponentsInChildren<CardPaymentUI>(true)).FirstOrDefault();
        if (card == null) throw new InvalidOperationException("Card Payment UI parent missing.");
        var ui = objects.SelectMany(o => o.GetComponentsInChildren<WinePouringUI>(true)).FirstOrDefault();
        if (ui == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/WinePouringUI.prefab");
            if (prefab == null) throw new InvalidOperationException("Run WinePouringUIAuthoring.Create first.");
            ui = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, card.transform.parent)).GetComponent<WinePouringUI>();
        }
        ui.gameObject.SetActive(false);
        EditorUtility.SetDirty(kitchen); EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        return "Bound two bar drink outputs, reachable pickup, reusable bottle/glass models, Red Wine refill recipe, and WinePouringUI. Scene not saved.";
    }

    static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        return child;
    }
    static Material Material(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .22f);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static Mesh Lathe(string name, Vector2[] profile, int sides = 20)
    {
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        for (int ring = 0; ring < profile.Length; ring++)
            for (int side = 0; side <= sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * profile[ring].x, profile[ring].y, Mathf.Sin(angle) * profile[ring].x));
                uv.Add(new Vector2((float)side / sides, (float)ring / (profile.Length - 1)));
                if (ring == profile.Length - 1 || side == sides) continue;
                int a = ring * (sides + 1) + side, b = a + sides + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }
        var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    static void Part(GameObject root, string name, Vector2[] shape, Material material)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(root.transform, false);
        part.GetComponent<MeshFilter>().sharedMesh = Lathe(name, shape); part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
    static GameObject CreateBottle()
    {
        string path = Folder + "/Wine Refill Bottle.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
        var root = new GameObject("Wine Refill Bottle");
        try
        {
            root.AddComponent<WineBottleCarryPose>();
            Part(root, "Wine Bottle Body", new[] { new Vector2(0,-.39f),new Vector2(.14f,-.39f),new Vector2(.18f,-.34f),new Vector2(.18f,.26f),new Vector2(.08f,.4f),new Vector2(.065f,.59f),new Vector2(0,.59f) }, Material("Bottle Forest Green",new Color(.08f,.23f,.14f)));
            Part(root, "Wine Bottle Label", new[] {new Vector2(.182f,-.21f),new Vector2(.182f,.13f)}, Material("Bottle Cream Label",new Color(.91f,.81f,.56f)));
            Part(root, "Wine Bottle Seal", new[] {new Vector2(.183f,-.065f),new Vector2(.183f,-.01f)}, Material("Wine Ruby",new Color(.48f,.035f,.12f)));
            Part(root, "Wine Bottle Foil", new[] {new Vector2(.071f,.46f),new Vector2(.075f,.58f),new Vector2(0,.58f)}, Material("Wine Ruby",new Color(.48f,.035f,.12f)));
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static GameObject CreateGlass()
    {
        string path = Folder + "/Served Red Wine Glass.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
        var root = new GameObject("Served Red Wine Glass");
        try
        {
            var glass=Material("Glass Blue Grey",new Color(.65f,.79f,.85f));
            Part(root,"Wine Glass Base",new[] {new Vector2(0,0),new Vector2(.17f,0),new Vector2(.17f,.035f),new Vector2(.035f,.055f),new Vector2(.03f,.22f)},glass);
            Part(root,"Wine Glass Bowl",new[] {new Vector2(.03f,.2f),new Vector2(.14f,.25f),new Vector2(.22f,.39f),new Vector2(.225f,.57f),new Vector2(.205f,.57f),new Vector2(.20f,.4f),new Vector2(.12f,.28f),new Vector2(.03f,.23f)},glass);
            Part(root,"Wine Glass Liquid",new[] {new Vector2(.203f,.48f),new Vector2(0,.48f)},Material("Wine Ruby",new Color(.48f,.035f,.12f)));
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
#endif

