using UnityEngine;
using UnityEngine.UI;

// Barra de vida pequena que flota sobre un enemigo.
// Usa la misma logica que la barra del jugador: Image.fillAmount = vida / vidaMaxima.
// - Con la vida completa no se ve.
// - Al recibir dano aparece y se actualiza en tiempo real.
// - Cuando el enemigo muere (se destruye) la barra desaparece con el.
// El enemigo la crea solo en su Awake con EnemyHealthBar.Crear(transform).
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Apariencia")]
    public Vector2 tamano = new Vector2(1.2f, 0.14f);      // ancho y alto en metros
    public float margenArriba = 0.35f;                      // distancia sobre la parte mas alta del enemigo
    public Color colorFondo = new Color(0f, 0f, 0f, 0.65f);
    public Color colorVida = new Color(0.85f, 0.12f, 0.12f, 1f);

    private const float PixelesAncho = 100f;

    private Transform objetivo;
    private float altura;
    private Canvas canvas;
    private Image relleno;
    private Camera cam;

    private static Sprite spriteBlanco;

    // ---------------------------------------------------------------- Creacion

    public static EnemyHealthBar Crear(Transform enemigo)
    {
        GameObject go = new GameObject("BarraVida_" + enemigo.name);
        EnemyHealthBar barra = go.AddComponent<EnemyHealthBar>();
        barra.Construir(enemigo);
        return barra;
    }

    void Construir(Transform enemigo)
    {
        objetivo = enemigo;
        altura = CalcularAltura(enemigo) + margenArriba;

        if (spriteBlanco == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            spriteBlanco = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        // Canvas en el mundo
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rt = (RectTransform)transform;
        float pixelesAlto = PixelesAncho * (tamano.y / tamano.x);
        rt.sizeDelta = new Vector2(PixelesAncho, pixelesAlto);
        transform.localScale = Vector3.one * (tamano.x / PixelesAncho);

        // Fondo
        CrearImagen("Fondo", colorFondo, false);

        // Relleno (igual que BarraDeVida del jugador)
        relleno = CrearImagen("Vida", colorVida, true);
        relleno.type = Image.Type.Filled;
        relleno.fillMethod = Image.FillMethod.Horizontal;
        relleno.fillOrigin = (int)Image.OriginHorizontal.Left;
        relleno.fillAmount = 1f;

        canvas.enabled = false;   // con la vida completa no se muestra
        Seguir();
    }

    Image CrearImagen(string nombre, Color color, bool conMargen)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        float m = conMargen ? 1.5f : 0f;
        r.offsetMin = new Vector2(m, m);
        r.offsetMax = new Vector2(-m, -m);

        Image img = go.AddComponent<Image>();
        img.sprite = spriteBlanco;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Altura de la parte mas alta del modelo (solo mallas, no rayos ni particulas)
    static float CalcularAltura(Transform enemigo)
    {
        bool hay = false;
        Bounds b = new Bounds(enemigo.position, Vector3.zero);
        foreach (Renderer r in enemigo.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!hay) { b = r.bounds; hay = true; }
            else b.Encapsulate(r.bounds);
        }
        return hay ? Mathf.Max(0.5f, b.max.y - enemigo.position.y) : 1.5f;
    }

    // ---------------------------------------------------------------- Uso

    // Lo llama el enemigo cada vez que su vida cambia
    public void Actualizar(float vida, float vidaMaxima)
    {
        if (relleno == null) return;

        float porcentaje = vidaMaxima > 0f ? Mathf.Clamp01(vida / vidaMaxima) : 0f;
        relleno.fillAmount = porcentaje;

        // Solo visible si ya recibio dano y sigue vivo
        canvas.enabled = porcentaje > 0f && porcentaje < 1f;
    }

    void LateUpdate()
    {
        // El enemigo murio (fue destruido): la barra desaparece
        if (objetivo == null)
        {
            Destroy(gameObject);
            return;
        }

        // Enemigo desactivado (por ejemplo al reaparecer en un checkpoint): ocultar
        if (!objetivo.gameObject.activeInHierarchy)
        {
            if (canvas.enabled) canvas.enabled = false;
            return;
        }

        if (canvas.enabled) Seguir();
    }

    void Seguir()
    {
        transform.position = objetivo.position + Vector3.up * altura;

        if (cam == null || !cam.isActiveAndEnabled)
        {
            cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
        }
        if (cam != null)
            transform.rotation = cam.transform.rotation;   // siempre de frente a la camara
    }
}
