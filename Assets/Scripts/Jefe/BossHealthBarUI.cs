using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Barra de vida del jefe en la interfaz del jugador (arriba al centro).
// Misma logica que la barra del jugador (fillAmount = vida / vidaMaxima) + una barra blanca
// que baja con retraso para que se note cuanto dano hiciste.
public class BossHealthBarUI : MonoBehaviour
{
    Canvas canvas;
    CanvasGroup grupo;
    Image relleno, rastro;
    TextMeshProUGUI txtNombre, txtFase;
    RectTransform panel;

    public float segundaFase = 0.75f;
    public float terceraFase = 0.40f;

    float porcentaje = 1f, porcentajeRastro = 1f, esperaRastro;
    float alfaObjetivo;
    float sacudida;
    float avisoFase;

    static Sprite spriteBlanco;

    // Las marcas de fase se toman de los campos Vida Fase 2 / Vida Fase 3 del Boss Soldier
    public static BossHealthBarUI Crear(string nombre, TMP_FontAsset fuente, Color colorVida, float marcaFase2, float marcaFase3)
    {
        GameObject go = new GameObject("BarraVidaJefe", typeof(RectTransform));
        BossHealthBarUI ui = go.AddComponent<BossHealthBarUI>();
        ui.segundaFase = marcaFase2;
        ui.terceraFase = marcaFase3;
        ui.Construir(nombre, fuente, colorVida);
        return ui;
    }

    void Construir(string nombre, TMP_FontAsset fuente, Color colorVida)
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        CanvasScaler s = gameObject.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        GameObject p = new GameObject("Panel", typeof(RectTransform));
        p.transform.SetParent(transform, false);
        panel = (RectTransform)p.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0, -40);
        panel.sizeDelta = new Vector2(900, 90);

        txtNombre = Texto("Nombre", panel, 34, fuente);
        txtNombre.text = nombre;
        RectTransform rn = txtNombre.rectTransform;
        rn.anchorMin = new Vector2(0, 1); rn.anchorMax = new Vector2(1, 1); rn.pivot = new Vector2(0.5f, 1f);
        rn.anchoredPosition = Vector2.zero; rn.sizeDelta = new Vector2(0, 44);

        Image fondo = Imagen("Fondo", panel, new Color(0f, 0f, 0f, 0.7f));
        RectTransform rf = fondo.rectTransform;
        rf.anchorMin = new Vector2(0, 1); rf.anchorMax = new Vector2(1, 1); rf.pivot = new Vector2(0.5f, 1f);
        rf.anchoredPosition = new Vector2(0, -48); rf.sizeDelta = new Vector2(0, 28);

        rastro = Imagen("Rastro", rf, new Color(1f, 1f, 1f, 0.85f));
        Llenar(rastro.rectTransform, 3f);
        rastro.type = Image.Type.Filled; rastro.fillMethod = Image.FillMethod.Horizontal;
        rastro.fillOrigin = (int)Image.OriginHorizontal.Left;

        relleno = Imagen("Vida", rf, colorVida);
        Llenar(relleno.rectTransform, 3f);
        relleno.type = Image.Type.Filled; relleno.fillMethod = Image.FillMethod.Horizontal;
        relleno.fillOrigin = (int)Image.OriginHorizontal.Left;

        // Marcas de fase (66% y 33%)
        foreach (float m in new[] { segundaFase, terceraFase })
        {
            Image marca = Imagen("MarcaFase", rf, new Color(0f, 0f, 0f, 0.9f));
            RectTransform rm = marca.rectTransform;
            rm.anchorMin = new Vector2(m, 0); rm.anchorMax = new Vector2(m, 1);
            rm.pivot = new Vector2(0.5f, 0.5f);
            rm.sizeDelta = new Vector2(4, 0);
            rm.anchoredPosition = Vector2.zero;
        }

        txtFase = Texto("Fase", panel, 26, fuente);
        RectTransform rfz = txtFase.rectTransform;
        rfz.anchorMin = new Vector2(0, 1); rfz.anchorMax = new Vector2(1, 1); rfz.pivot = new Vector2(0.5f, 1f);
        rfz.anchoredPosition = new Vector2(0, -82); rfz.sizeDelta = new Vector2(0, 36);
        txtFase.color = new Color(1f, 0.6f, 0.2f);
        txtFase.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ API

    public void Mostrar(bool visible) { alfaObjetivo = visible ? 1f : 0f; }

    public void Actualizar(float vida, float vidaMaxima)
    {
        float nuevo = vidaMaxima > 0f ? Mathf.Clamp01(vida / vidaMaxima) : 0f;
        if (nuevo < porcentaje) { esperaRastro = 0.45f; sacudida = 1f; }
        porcentaje = nuevo;
        if (porcentaje >= porcentajeRastro) porcentajeRastro = porcentaje;
        relleno.fillAmount = porcentaje;
    }

    public void CambiarNombre(string nombre)
    {
        if (txtNombre != null && txtNombre.text != nombre) txtNombre.text = nombre;
    }

    public void AvisoFase(string texto)
    {
        txtFase.text = texto;
        txtFase.gameObject.SetActive(true);
        avisoFase = 2.5f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo, dt * 2.5f);

        if (esperaRastro > 0f) esperaRastro -= dt;
        else porcentajeRastro = Mathf.MoveTowards(porcentajeRastro, porcentaje, dt * 0.6f);
        rastro.fillAmount = porcentajeRastro;

        sacudida = Mathf.MoveTowards(sacudida, 0f, dt * 6f);
        panel.anchoredPosition = new Vector2(Random.Range(-1f, 1f) * 6f * sacudida, -40f + Random.Range(-1f, 1f) * 3f * sacudida);

        if (avisoFase > 0f)
        {
            avisoFase -= dt;
            txtFase.alpha = Mathf.Clamp01(avisoFase) * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 10f));
            if (avisoFase <= 0f) txtFase.gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------ Helpers

    Image Imagen(string n, RectTransform padre, Color c)
    {
        if (spriteBlanco == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            spriteBlanco = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.sprite = spriteBlanco;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Texto(string n, RectTransform padre, float tam, TMP_FontAsset fuente)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.fontSize = tam;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        if (fuente == null) { t.outlineWidth = 0.18f; t.outlineColor = new Color32(0, 0, 0, 200); }
        return t;
    }

    static void Llenar(RectTransform r, float margen)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margen, margen); r.offsetMax = new Vector2(-margen, -margen);
    }
}
