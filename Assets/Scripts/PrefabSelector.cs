using UnityEngine;

/// <summary>
/// Static shared state — no GameObject required.
/// Replace your old PrefabSelector.cs with this file.
/// </summary>
public static class PrefabSelector
{
    public static GameObject SelectedPrefab { get; private set; }

    public static void Select(GameObject prefab)
    {
        SelectedPrefab = prefab;
        Debug.Log($"[PrefabSelector] Selected: {prefab?.name ?? "null"}");
    }
}