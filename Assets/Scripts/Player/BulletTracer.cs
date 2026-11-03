using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BulletTracer : MonoBehaviour
{
    public float duration = 0.12f;
    LineRenderer lr;
    float startWidth;
    Color startColor;
    float t;

    public void Init(Vector3 start, Vector3 end)
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        startWidth = lr.widthMultiplier;
        startColor = lr.startColor;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime / duration;
        float k = 1f - t;
        lr.widthMultiplier = startWidth * k;                 // se adelgaza
        Color c = startColor; c.a = k;                       // se desvanece
        lr.startColor = c; lr.endColor = c;
        if (t >= 1f) Destroy(gameObject);
    }
}