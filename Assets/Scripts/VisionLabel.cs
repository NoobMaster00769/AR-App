
using UnityEngine;
using TMPro;
using System.Collections;

public class VisionLabelUI : MonoBehaviour
{
    [Header("References")]
    public TMP_Text labelText;
    public float    hideAfterSeconds = 2.5f;

    Coroutine _hideRoutine;

    public void OnDetection(string message)
    {
        var parts = message.Split(':');
        if (parts.Length < 3) return;

        string label = parts[0];

        labelText.text = char.ToUpper(label[0]) + label.Substring(1);
        labelText.gameObject.SetActive(true);

        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideAfterSeconds);
        labelText.gameObject.SetActive(false);
    }
}
