using UnityEngine;

public class PrefabSelector : MonoBehaviour
{
    public static GameObject selectedPrefab;

    public void SelectPrefab(GameObject prefab)
    {
        selectedPrefab = prefab;
        Debug.Log("✅ Selected: " + prefab.name);
    }
}