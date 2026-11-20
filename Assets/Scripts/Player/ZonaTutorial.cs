using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Zona de tutorial: al tocarla el jugador, muestra un aviso con las teclas, con el mismo estilo
// que el aviso "[E] Recoger" del fusil.
// Se agrega SOLA a los objetos llamados "TArma" y "TSalto" de cualquier escena (no hace falta
// ponerla a mano). Si el objeto no tiene collider, se usa una zona de "tamanoZona" alrededor de el.
public class ZonaTutorial : MonoBehaviour
{
    public enum Tipo { Arma, Salto, Personalizado }

    [System.Serializable]
    public class Linea
    {
        public string tecla;
        public string texto;
        public Linea(string t, string x) { tecla = t; texto = x; }
    }

    public Tipo tipo = Tipo.Personalizado;
    public List<Linea> lineas = new List<Linea>();      // solo para "Personalizado"
    public Vector3 tamanoZona = new Vector3(4f, 3f, 4f); // si el objeto no tiene collider
    public float tiempoMinimo = 5f;                     // segundos que se ve como minimo
    public float tiempoAlSalir = 1.5f;                  // sigue visible este tiempo al salir de la zona
    public string playerTag = "Player";
    public TMP_FontAsset fuente;

    Collider zona;
    Transform jugador;
    CharacterController jugadorCC;
    PlayerMovement movimiento;
    bool dentro;
    float visibleHasta;

    // ------------------------------------------------------------------ auto-configuracion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        AlCargar(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "TArma") Asegurar(t.gameObject, Tipo.Arma);
                else if (t.name == "TSalto") Asegurar(t.gameObject, Tipo.Salto);
            }
    }

    static void Asegurar(GameObject go, Tipo tipo)
    {
        if (go.GetComponent<ZonaTutorial>() != null) return;
        ZonaTutorial z = go.AddComponent<ZonaTutorial>();
        z.tipo = tipo;
    }

    // ------------------------------------------------------------------ logica

    void Start()
    {
        zona = GetComponent<Collider>();
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
        {
            jugador = p.transform;
            jugadorCC = p.GetComponent<CharacterController>();
            movimiento = p.GetComponent<PlayerMovement>();
        }
    }

    void Update()
    {
        if (jugador == null) return;

        bool ahora = JugadorDentro();
        if (ahora && !dentro) visibleHasta = Time.time + tiempoMinimo;
        if (ahora) visibleHasta = Mathf.Max(visibleHasta, Time.time + tiempoAlSalir);
        dentro = ahora;

        if (Time.time < visibleHasta) AvisoTutorial.Mostrar(this, Lineas(), fuente);
        else AvisoTutorial.Ocultar(this);
    }

    void OnDisable() { AvisoTutorial.Ocultar(this); }

    bool JugadorDentro()
    {
        Bounds b = zona != null ? zona.bounds : new Bounds(transform.position, tamanoZona);
        if (jugadorCC != null) return b.Intersects(jugadorCC.bounds);
        return b.Contains(jugador.position);
    }

    List<Linea> Lineas()
    {
        switch (tipo)
        {
            case Tipo.Arma:
                return new List<Linea> { new Linea("Click izq", "Disparar"), new Linea("R", "Recargar") };
            case Tipo.Salto:
                return new List<Linea> { new Linea("Espacio", "Saltar (hasta 2 veces)"), new Linea(NombreTecla(DashKey()), "Dash") };
            default:
                return lineas;
        }
    }

    Key DashKey() { return movimiento != null ? movimiento.dashKey : Key.CapsLock; }

    static string NombreTecla(Key k)
    {
        switch (k)
        {
            case Key.CapsLock: return "Bloq Mayus";
            case Key.LeftShift: case Key.RightShift: return "Shift";
            case Key.LeftCtrl: case Key.RightCtrl: return "Ctrl";
            case Key.LeftAlt: case Key.RightAlt: return "Alt";
            case Key.Space: return "Espacio";
            default: return k.ToString();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (GetComponent<Collider>() != null) return;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
        Gizmos.DrawWireCube(transform.position, tamanoZona);
    }
}

// Panel con varias filas [tecla] texto, mismo estilo que AvisoInteraccion (un poco mas arriba para no encimarse).
public class AvisoTutorial : MonoBehaviour
{
    static AvisoTutorial instancia;
    static Sprite spriteBlanco;

    Object duenio;
    CanvasGroup grupo;
    RectTransform panel;
    TMP_FontAsset fuente;
    readonly List<RectTransform> teclas = new List<RectTransform>();
    readonly List<TextMeshProUGUI> letras = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> textos = new List<TextMeshProUGUI>();
    float alfaObjetivo, aparicion;
    string firma = "";

    const float AltoFila = 62f, Margen = 14f;

    public static void Mostrar(Object quien, List<ZonaTutorial.Linea> lineas, TMP_FontAsset fuente)
    {
        if (instancia == null) Crear(fuente);
        if (instancia.duenio != quien || instancia.alfaObjetivo < 1f) instancia.aparicion = 0f;
        instancia.duenio = quien;
        instancia.alfaObjetivo = 1f;
        instancia.Poner(lineas);
    }

    public static void Ocultar(Object quien)
    {
        if (instancia == null || instancia.duenio != quien) return;
        instancia.alfaObjetivo = 0f;
    }

    static void Crear(TMP_FontAsset fuente)
    {
        GameObject go = new GameObject("AvisoTutorial", typeof(RectTransform));
        instancia = go.AddComponent<AvisoTutorial>();
        instancia.fuente = fuente;
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 39;
        CanvasScaler s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        instancia.grupo = go.AddComponent<CanvasGroup>();
        instancia.grupo.alpha = 0f;
        instancia.grupo.blocksRaycasts = false;

        Image fondo = Imagen("Panel", (RectTransform)go.transform, new Color(0f, 0f, 0f, 0.55f));
        instancia.panel = fondo.rectTransform;
        instancia.panel.anchorMin = instancia.panel.anchorMax = new Vector2(0.5f, 0f);
        instancia.panel.pivot = new Vector2(0.5f, 0f);
        instancia.panel.anchoredPosition = new Vector2(0f, 290f);
    }

    void Poner(List<ZonaTutorial.Linea> lineas)
    {
        string f = "";
        foreach (var l in lineas) f += l.tecla + "|" + l.texto + "\n";
        if (f == firma) return;
        firma = f;

        while (teclas.Count < lineas.Count) NuevaFila();
        for (int i = 0; i < teclas.Count; i++)
        {
            bool activa = i < lineas.Count;
            teclas[i].gameObject.SetActive(activa);
            textos[i].gameObject.SetActive(activa);
            if (!activa) continue;
            letras[i].text = lineas[i].tecla;
            textos[i].text = lineas[i].texto;
        }
        Acomodar(lineas.Count);
    }

    void NuevaFila()
    {
        Image borde = Imagen("Tecla", panel, Color.white);
        RectTransform t = borde.rectTransform;
        t.anchorMin = t.anchorMax = new Vector2(0f, 1f);
        t.pivot = new Vector2(0.5f, 0.5f);
        Image interior = Imagen("Interior", t, new Color(0.1f, 0.1f, 0.1f, 1f));
        RectTransform ri = interior.rectTransform;
        ri.anchorMin = Vector2.zero; ri.anchorMax = Vector2.one;
        ri.offsetMin = new Vector2(3f, 3f); ri.offsetMax = new Vector2(-3f, -3f);
        TextMeshProUGUI letra = Texto("Letra", t, 26f);
        RectTransform rl = letra.rectTransform;
        rl.anchorMin = Vector2.zero; rl.anchorMax = Vector2.one;
        rl.offsetMin = rl.offsetMax = Vector2.zero;
        letra.alignment = TextAlignmentOptions.Center;
        letra.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI texto = Texto("Accion", panel, 30f);
        texto.alignment = TextAlignmentOptions.Left;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform rt = texto.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);

        teclas.Add(t); letras.Add(letra); textos.Add(texto);
    }

    void Acomodar(int n)
    {
        float anchoTecla = 48f;
        for (int i = 0; i < n; i++) { letras[i].ForceMeshUpdate(); anchoTecla = Mathf.Max(anchoTecla, letras[i].preferredWidth + 24f); }
        float anchoTexto = 0f;
        for (int i = 0; i < n; i++) { textos[i].ForceMeshUpdate(); anchoTexto = Mathf.Max(anchoTexto, textos[i].preferredWidth); }

        for (int i = 0; i < n; i++)
        {
            float y = -(Margen + AltoFila * (i + 0.5f));
            teclas[i].sizeDelta = new Vector2(anchoTecla, 48f);
            teclas[i].anchoredPosition = new Vector2(24f + anchoTecla * 0.5f, y);
            textos[i].rectTransform.sizeDelta = new Vector2(anchoTexto + 10f, AltoFila);
            textos[i].rectTransform.anchoredPosition = new Vector2(24f + anchoTecla + 22f, y);
        }
        panel.sizeDelta = new Vector2(Mathf.Clamp(24f + anchoTecla + 22f + anchoTexto + 30f, 220f, 1100f), Margen * 2f + AltoFila * n);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo, dt * 6f);
        aparicion = Mathf.MoveTowards(aparicion, 1f, dt * 5f);
        float pop = 1f + 0.15f * Mathf.Sin(aparicion * Mathf.PI) * (1f - aparicion * 0.5f);
        panel.localScale = Vector3.one * pop;
        float lat = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f);
        foreach (RectTransform t in teclas) t.localScale = Vector3.one * lat;
    }

    static Image Imagen(string n, RectTransform padre, Color color)
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
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Texto(string n, RectTransform padre, float tam)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.fontSize = tam;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}
