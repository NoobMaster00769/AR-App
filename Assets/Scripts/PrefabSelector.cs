using UnityEngine;

public static class PrefabSelector
{
    public static GameObject SelectedPrefab { get; private set; }

    public static void Select(GameObject prefab)
    {
        SelectedPrefab = prefab;
        Debug.Log($"[PrefabSelector] Selected: {prefab?.name ?? "null"}");
    }
}