using UnityEngine;
using UnityEngine.UI;

// Feedback visual al recibir dano: un borde rojo suave en las orillas de la pantalla que aparece
// y se desvanece rapido. El centro de la pantalla nunca se tapa.
// Se crea solo la primera vez que el jugador recibe dano (no hay que ponerlo en la escena).
public class FeedbackDano : MonoBehaviour
{
    public Color color = new Color(0.75f, 0.02f, 0.02f, 1f);
    [Range(0f, 1f)] public float opacidadMinima = 0.22f;   // golpe pequeno
    [Range(0f, 1f)] public float opacidadMaxima = 0.55f;   // golpe fuerte (25% de la vida o mas)
    public float duracion = 0.55f;                          // segundos en desvanecerse

    static FeedbackDano instancia;
    Image imagen;
    float alfa;

    public static void Mostrar(float fraccionDeVida)
    {
        if (instancia == null) Crear();
        float t = Mathf.Clamp01(fraccionDeVida / 0.25f);
        float objetivo = Mathf.Lerp(instancia.opacidadMinima, instancia.opacidadMaxima, t);
        instancia.alfa = Mathf.Max(instancia.alfa, objetivo);
    }

    static void Crear()
    {
        GameObject go = new GameObject("FeedbackDano", typeof(RectTransform));
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 40;
        instancia = go.AddComponent<FeedbackDano>();

        GameObject img = new GameObject("Borde", typeof(RectTransform));
        img.transform.SetParent(go.transform, false);
        RectTransform r = (RectTransform)img.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        instancia.imagen = img.AddComponent<Image>();
        instancia.imagen.raycastTarget = false;
        instancia.imagen.sprite = CrearSpriteVineta();
        instancia.imagen.color = new Color(instancia.color.r, instancia.color.g, instancia.color.b, 0f);
    }

    // Textura radial: transparente en el centro, opaca en las orillas
    static Sprite CrearSpriteVineta()
    {
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;   // 0 centro, 1 esquina
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    void Update()
    {
        if (alfa > 0f) alfa = Mathf.Max(0f, alfa - Time.unscaledDeltaTime / duracion * opacidadMaxima);
        imagen.color = new Color(color.r, color.g, color.b, alfa);
    }

    void OnDestroy() { if (instancia == this) instancia = null; }
}
