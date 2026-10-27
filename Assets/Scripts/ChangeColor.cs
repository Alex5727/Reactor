
using UnityEngine;
using System.Collections;

public class ChangeColor : MonoBehaviour
{
 public Color hitColor = Color.red;
public float duration = 0.2f;

private Renderer objectRenderer;
private Color originalColor;
private Coroutine colorRoutine;

void Awake()
{
    objectRenderer = GetComponent<Renderer>();

    if (objectRenderer != null)
    {
        originalColor = objectRenderer.material.color;
    }
}

public void TakeDamage(float damage)
{
    if (objectRenderer == null)
        return;

    if (colorRoutine != null)
        StopCoroutine(colorRoutine);

    colorRoutine = StartCoroutine(FChangeColor());
}

IEnumerator FChangeColor()
{
    objectRenderer.material.color = hitColor;

    yield return new WaitForSeconds(duration);

    objectRenderer.material.color = originalColor;
    colorRoutine = null;
}
}
