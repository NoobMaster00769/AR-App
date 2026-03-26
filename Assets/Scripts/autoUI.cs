using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class AutoPrefabMenu : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public GameObject buttonPrefab;   // UI Button prefab
    public Transform contentParent;   // ScrollView Content
    public List<GameObject> prefabs;  // Drag all prefabs here

    void Start()
    {
        GenerateMenu();
    }

    void GenerateMenu()
    {
        foreach (GameObject prefab in prefabs)
        {
            GameObject btnObj = Instantiate(buttonPrefab, contentParent);



TMP_Text txt = btnObj.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
                txt.text = prefab.name;

            Button btn = btnObj.GetComponent<Button>();
            GameObject selected = prefab;

            btn.onClick.AddListener(() =>
            {
                PrefabSelector.selectedPrefab = selected;
                Debug.Log("✅ Selected: " + selected.name);
            });
        }
    }
}