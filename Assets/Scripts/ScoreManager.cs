using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Sistema de puntuacion del nivel.
// - Se crea solo en cualquier escena que tenga un CheckpointManager (no hay que ponerlo en la escena).
// - Eliminar enemigos da puntos; eliminar varios seguidos sube el COMBO (multiplicador).
// - Si el jugador muere, pierde los puntos ganados despues del ultimo checkpoint tocado.
// - Al terminar el nivel muestra la pantalla de resultados.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Combo")]
    public float tiempoCombo = 4f;          // segundos para encadenar la siguiente eliminacion
    public int comboMaximo = 5;             // multiplicador maximo (x5)

    [Header("Muerte")]
    public int penalizacionMuerte = 50;     // puntos extra que se pierden cada vez que el jugador muere

    [Header("Interfaz")]
    public Vector2 posicionHUD = new Vector2(-40f, -30f);   // desde la esquina superior derecha
    public Color colorPuntos = new Color(1f, 0.82f, 0.25f);  // dorado
    public Color colorPopup = new Color(1f, 0.15f, 0.15f);    // rojo del "+puntos"
    public Color colorPerdido = new Color(1f, 0.3f, 0.3f);
    public TMP_FontAsset fuente;                               // fuente de todos los textos de puntuacion (vacio = la de TMP por defecto)

    // ---------------- estado
    private int puntos, puntosGuardados;
    private int eliminados, eliminadosGuardados;
    private int combo, mejorCombo;
    private int muertes;
    private float comboTimer;
    private bool terminado;

    // ---------------- visual
    private float valorMostrado;
    private int valorObjetivo;
    private float pulso;
    private Color colorPulso;
    private float avisoTimer;

    private RectTransform raiz, panelPuntos, capaPopups;
    private TextMeshProUGUI txtPuntos, txtCombo, txtAviso;
    private Image barraCombo, fondoBarraCombo;
    private readonly List<GameObject> popups = new List<GameObject>();

    private AudioSource audioSrc;
    private AudioClip clipPunto, clipTick, clipPerder, clipFinal;

    // =============================================================== Creacion automatica

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (Instance != null) return;
        if (FindAnyObjectByType<CheckpointManager>() == null) return;   // solo en niveles
        new GameObject("ScoreManager").AddComponent<ScoreManager>();
    }

    void Awake()
    {
        Instance = this;
        CrearSonidos();
        CrearHUD();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =============================================================== API

    // Lo llaman los enemigos al morir
    public static void EnemigoEliminado(int puntosBase, Vector3 posicion)
    {
        if (Instance != null) Instance.SumarEnemigo(puntosBase, posicion);
    }

    // Lo llama el CheckpointManager al tocar un checkpoint
    public static void GuardarEnCheckpoint()
    {
        if (Instance != null) Instance.Guardar();
    }

    // Lo llama el CheckpointManager cuando el jugador muere
    public static void PerderNoGuardados()
    {
        if (Instance != null) Instance.Perder();
    }

    // Lo llama el trigger de fin de nivel; alTerminar se ejecuta cuando el jugador continua
    public static void MostrarResultados(Action alTerminar)
    {
        if (Instance == null) { alTerminar?.Invoke(); return; }
        if (Instance.terminado) return;
        Instance.StartCoroutine(Instance.Resultados(alTerminar));
    }

    // =============================================================== Logica

    void SumarEnemigo(int puntosBase, Vector3 posicion)
    {
        if (terminado) return;

        combo = comboTimer > 0f ? Mathf.Min(combo + 1, comboMaximo) : 1;
        comboTimer = tiempoCombo;
        mejorCombo = Mathf.Max(mejorCombo, combo);

        int ganado = puntosBase * combo;
        puntos += ganado;
        eliminados++;

        CrearPopup(ganado, combo, posicion);
        Sonar(clipPunto, 1f + (combo - 1) * 0.12f, 0.7f);
    }

    // Al tocar un checkpoint: estos puntos ya no se pierden al morir (sin aviso en pantalla)
    void Guardar()
    {
        if (terminado) return;
        puntosGuardados = puntos;
        eliminadosGuardados = eliminados;
    }

    void Perder()
    {
        if (terminado) return;
        muertes++;
        int noGuardados = puntos - puntosGuardados;

        // Vuelve a lo guardado en el ultimo checkpoint...
        puntos = puntosGuardados;
        eliminados = eliminadosGuardados;

        // ...y ademas pierde la penalizacion por morir (sin bajar de 0).
        // Tambien se descuenta de lo guardado para que no se recupere al volver a morir.
        int penalizacion = Mathf.Min(penalizacionMuerte, puntos);
        puntos -= penalizacion;
        puntosGuardados = puntos;

        int perdidos = noGuardados + penalizacion;
        combo = 0;
        comboTimer = 0f;

        foreach (GameObject p in popups) if (p != null) Destroy(p);
        popups.Clear();
        valorObjetivo = puntos;

        if (perdidos > 0)
        {
            MostrarAviso("-" + perdidos.ToString("N0") + " PUNTOS PERDIDOS", colorPerdido);
            Pulsar(colorPerdido, 1f);
            Sonar(clipPerder, 1f, 0.6f);
        }
    }

    // =============================================================== Update visual

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Combo
        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f) { comboTimer = 0f; combo = 0; }
        }
        bool verCombo = combo >= 2 && !terminado;
        txtCombo.gameObject.SetActive(verCombo);
        fondoBarraCombo.gameObject.SetActive(verCombo);
        if (verCombo)
        {
            txtCombo.text = "COMBO x" + combo;
            barraCombo.fillAmount = comboTimer / tiempoCombo;
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 12f);
            txtCombo.rectTransform.localScale = Vector3.one * s;
        }

        // Contador que "rueda" hacia el valor nuevo
        valorMostrado = Mathf.Lerp(valorMostrado, valorObjetivo, 1f - Mathf.Exp(-10f * dt));
        if (Mathf.Abs(valorMostrado - valorObjetivo) < 0.5f) valorMostrado = valorObjetivo;
        txtPuntos.text = Mathf.RoundToInt(valorMostrado).ToString("N0");

        // Pulso (escala + color)
        pulso = Mathf.MoveTowards(pulso, 0f, dt * 2.5f);
        panelPuntos.localScale = Vector3.one * (1f + 0.25f * pulso);
        txtPuntos.color = Color.Lerp(Color.white, colorPulso, pulso);

        // Aviso temporal
        if (avisoTimer > 0f)
        {
            avisoTimer -= dt;
            Color c = txtAviso.color;
            c.a = Mathf.Clamp01(avisoTimer / 0.5f);
            txtAviso.color = c;
            txtAviso.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Clamp01(avisoTimer - 1.7f) * 3f);
            if (avisoTimer <= 0f) txtAviso.gameObject.SetActive(false);
        }
    }

    void Pulsar(Color color, float fuerza)
    {
        colorPulso = color;
        pulso = Mathf.Max(pulso, fuerza);
    }

    void MostrarAviso(string texto, Color color)
    {
        txtAviso.text = texto;
        txtAviso.color = color;
        txtAviso.gameObject.SetActive(true);
        avisoTimer = 2f;
    }

    // =============================================================== Popup "+puntos"

    void CrearPopup(int ganado, int multiplicador, Vector3 posicionMundo)
    {
        Camera cam = Camera.main;
        Vector2 pantalla = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
        if (cam != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(posicionMundo + Vector3.up * 0.5f);
            if (sp.z > 0f) pantalla = sp;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(capaPopups, pantalla, null, out Vector2 inicio);

        TextMeshProUGUI t = CrearTexto("Popup", capaPopups, 64, TextAlignmentOptions.Center);
        t.text = "+" + ganado.ToString("N0") + (multiplicador > 1 ? "\n<size=55%>COMBO x" + multiplicador + "</size>" : "");
        t.color = colorPopup;
        t.rectTransform.sizeDelta = new Vector2(400, 160);
        t.rectTransform.anchoredPosition = inicio;
        t.rectTransform.localScale = Vector3.zero;

        popups.Add(t.gameObject);
        StartCoroutine(AnimarPopup(t, inicio, ganado));
    }

    IEnumerator AnimarPopup(TextMeshProUGUI t, Vector2 inicio, int ganado)
    {
        RectTransform rt = t.rectTransform;

        // 1) Aparece con rebote y sube un poco
        float d1 = 0.45f;
        for (float e = 0f; e < d1; e += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            float k = e / d1;
            float escala = k < 0.4f ? Mathf.Lerp(0f, 1.4f, k / 0.4f) : Mathf.Lerp(1.4f, 1f, (k - 0.4f) / 0.6f);
            rt.localScale = Vector3.one * escala;
            rt.anchoredPosition = inicio + Vector2.up * (70f * Mathf.Sin(k * Mathf.PI * 0.5f));
            yield return null;
        }

        // 2) Vuela hacia el contador
        Vector2 desde = rt.anchoredPosition;
        float d2 = 0.5f;
        for (float e = 0f; e < d2; e += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            Vector2 destino = PosicionContador();
            float k = e / d2;
            k = k * k;                                   // acelera al final
            rt.anchoredPosition = Vector2.Lerp(desde, destino, k);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, k);
            yield return null;
        }

        // 3) Llega: el contador sube y "late"
        if (t == null) yield break;
        popups.Remove(t.gameObject);
        Destroy(t.gameObject);
        valorObjetivo = Mathf.Min(valorObjetivo + ganado, puntos);
        Pulsar(colorPuntos, 1f);
        Sonar(clipTick, 1.2f, 0.5f);
    }

    Vector2 PosicionContador()
    {
        Vector2 pantalla = RectTransformUtility.WorldToScreenPoint(null, txtPuntos.rectTransform.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(capaPopups, pantalla, null, out Vector2 local);
        return local;
    }

    // =============================================================== Pantalla final

    IEnumerator Resultados(Action alTerminar)
    {
        terminado = true;
        if (SpecialRoomManager.Instance != null) SpecialRoomManager.Instance.AddLock();   // no disparar

        foreach (GameObject p in popups) if (p != null) Destroy(p);
        popups.Clear();
        valorObjetivo = puntos;
        combo = 0;

        float tiempoNivel = Time.timeSinceLevelLoad;
        Time.timeScale = 0f;

        // Fondo
        Image fondo = CrearImagen("Resultados", raiz, new Color(0f, 0f, 0f, 0f));
        Estirar(fondo.rectTransform);

        TextMeshProUGUI titulo = CrearTexto("Titulo", fondo.rectTransform, 90, TextAlignmentOptions.Center);
        titulo.text = "NIVEL COMPLETADO";
        titulo.color = colorPuntos;
        Centrar(titulo.rectTransform, new Vector2(0, 280), new Vector2(1400, 140));

        TextMeshProUGUI etiqueta = CrearTexto("Etiqueta", fondo.rectTransform, 40, TextAlignmentOptions.Center);
        etiqueta.text = "PUNTUACIÓN";
        Centrar(etiqueta.rectTransform, new Vector2(0, 150), new Vector2(800, 60));

        TextMeshProUGUI total = CrearTexto("Total", fondo.rectTransform, 140, TextAlignmentOptions.Center);
        total.text = "0";
        Centrar(total.rectTransform, new Vector2(0, 50), new Vector2(1200, 180));

        TextMeshProUGUI stats = CrearTexto("Stats", fondo.rectTransform, 38, TextAlignmentOptions.Center);
        stats.text = "";
        Centrar(stats.rectTransform, new Vector2(0, -165), new Vector2(1200, 260));

        TextMeshProUGUI continuar = CrearTexto("Continuar", fondo.rectTransform, 34, TextAlignmentOptions.Center);
        continuar.text = "Presiona E para continuar";
        continuar.gameObject.SetActive(false);
        Centrar(continuar.rectTransform, new Vector2(0, -330), new Vector2(1000, 60));

        titulo.gameObject.SetActive(false);
        etiqueta.gameObject.SetActive(false);
        total.gameObject.SetActive(false);

        // Oscurecer
        for (float e = 0f; e < 0.6f; e += Time.unscaledDeltaTime)
        {
            fondo.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.85f, e / 0.6f));
            yield return null;
        }
        fondo.color = new Color(0f, 0f, 0f, 0.85f);

        // Titulo con golpe
        titulo.gameObject.SetActive(true);
        Sonar(clipFinal, 1f, 0.7f);
        for (float e = 0f; e < 0.35f; e += Time.unscaledDeltaTime)
        {
            titulo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, e / 0.35f);
            yield return null;
        }
        titulo.rectTransform.localScale = Vector3.one;

        // Conteo de la puntuacion
        etiqueta.gameObject.SetActive(true);
        total.gameObject.SetActive(true);
        float duracion = Mathf.Clamp(puntos / 1500f, 0.8f, 2.5f);
        float siguienteTick = 0f;
        for (float e = 0f; e < duracion; e += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - e / duracion, 3f);
            total.text = Mathf.RoundToInt(puntos * k).ToString("N0");
            if (e >= siguienteTick) { Sonar(clipTick, 1f + k * 0.8f, 0.35f); siguienteTick = e + 0.06f; }
            yield return null;
        }
        total.text = puntos.ToString("N0");
        total.color = colorPuntos;
        Sonar(clipPunto, 1.3f, 0.8f);
        for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
        {
            total.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, e / 0.3f);
            yield return null;
        }
        total.rectTransform.localScale = Vector3.one;

        // Estadisticas una por una
        int min = Mathf.FloorToInt(tiempoNivel / 60f);
        int seg = Mathf.FloorToInt(tiempoNivel % 60f);
        string[] lineas =
        {
            "Enemigos eliminados: " + eliminados,
            "Combo máximo: x" + Mathf.Max(1, mejorCombo),
            "Muertes: " + muertes,
            "Tiempo: " + min.ToString("00") + ":" + seg.ToString("00")
        };
        foreach (string l in lineas)
        {
            stats.text += (stats.text.Length > 0 ? "\n" : "") + l;
            Sonar(clipTick, 0.8f, 0.4f);
            yield return new WaitForSecondsRealtime(0.35f);
        }

        // Esperar a que el jugador continue
        continuar.gameObject.SetActive(true);
        while (true)
        {
            continuar.alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            Keyboard kb = Keyboard.current;
            bool pulsado = (kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
                           (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
            if (pulsado) break;
            yield return null;
        }

        // Fundido a negro y continuar
        titulo.gameObject.SetActive(false);
        etiqueta.gameObject.SetActive(false);
        total.gameObject.SetActive(false);
        stats.gameObject.SetActive(false);
        continuar.gameObject.SetActive(false);
        for (float e = 0f; e < 0.4f; e += Time.unscaledDeltaTime)
        {
            fondo.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.85f, 1f, e / 0.4f));
            yield return null;
        }

        Time.timeScale = 1f;
        alTerminar?.Invoke();
    }

    // =============================================================== Construccion de la UI

    void CrearHUD()
    {
        GameObject go = new GameObject("PuntuacionUI", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        raiz = (RectTransform)go.transform;

        // Panel de puntos (esquina superior derecha)
        GameObject panel = new GameObject("Puntos", typeof(RectTransform));
        panel.transform.SetParent(raiz, false);
        panelPuntos = (RectTransform)panel.transform;
        panelPuntos.anchorMin = panelPuntos.anchorMax = new Vector2(1f, 1f);
        panelPuntos.pivot = new Vector2(1f, 1f);
        panelPuntos.anchoredPosition = posicionHUD;
        panelPuntos.sizeDelta = new Vector2(460, 230);

        TextMeshProUGUI etiqueta = CrearTexto("Etiqueta", panelPuntos, 28, TextAlignmentOptions.Right);
        etiqueta.text = "PUNTOS";
        etiqueta.color = colorPuntos;
        Arriba(etiqueta.rectTransform, 0, 36);

        txtPuntos = CrearTexto("Valor", panelPuntos, 76, TextAlignmentOptions.Right);
        txtPuntos.text = "0";
        txtPuntos.fontStyle = FontStyles.Bold;
        Arriba(txtPuntos.rectTransform, 34, 86);

        txtCombo = CrearTexto("Combo", panelPuntos, 40, TextAlignmentOptions.Right);
        txtCombo.color = new Color(1f, 0.55f, 0.15f);
        txtCombo.rectTransform.pivot = new Vector2(1f, 0.5f);
        Arriba(txtCombo.rectTransform, 120, 46);

        fondoBarraCombo = CrearImagen("BarraComboFondo", panelPuntos, new Color(0f, 0f, 0f, 0.5f));
        RectTransform fb = fondoBarraCombo.rectTransform;
        fb.anchorMin = fb.anchorMax = new Vector2(1f, 1f);
        fb.pivot = new Vector2(1f, 1f);
        fb.anchoredPosition = new Vector2(0, -170);
        fb.sizeDelta = new Vector2(220, 8);
        barraCombo = CrearImagen("BarraCombo", fb, new Color(1f, 0.55f, 0.15f));
        Estirar(barraCombo.rectTransform);
        barraCombo.type = Image.Type.Filled;
        barraCombo.fillMethod = Image.FillMethod.Horizontal;
        barraCombo.fillOrigin = (int)Image.OriginHorizontal.Right;

        // Aviso central (puntos perdidos)
        txtAviso = CrearTexto("Aviso", raiz, 44, TextAlignmentOptions.Center);
        RectTransform ra = txtAviso.rectTransform;
        ra.anchorMin = ra.anchorMax = new Vector2(0.5f, 1f);
        ra.pivot = new Vector2(0.5f, 1f);
        ra.anchoredPosition = new Vector2(0, -140);
        ra.sizeDelta = new Vector2(1200, 70);
        txtAviso.gameObject.SetActive(false);

        // Capa para los "+puntos"
        GameObject capa = new GameObject("Popups", typeof(RectTransform));
        capa.transform.SetParent(raiz, false);
        capaPopups = (RectTransform)capa.transform;
        Estirar(capaPopups);
    }

    static Sprite spriteBlanco;

    Image CrearImagen(string nombre, RectTransform padre, Color color)
    {
        if (spriteBlanco == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            spriteBlanco = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.sprite = spriteBlanco;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI CrearTexto(string nombre, RectTransform padre, float tamano, TextAlignmentOptions alineacion)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = Color.white;
        t.raycastTarget = false;
        t.richText = true;
        // Contorno solo con la fuente por defecto: en fuentes con poco "padding" en su atlas
        // el contorno rellena el recuadro de cada letra y se ve como un bloque de color.
        if (fuente == null)
        {
            t.outlineWidth = 0.18f;
            t.outlineColor = new Color32(0, 0, 0, 200);
        }
        return t;
    }

    static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void Arriba(RectTransform r, float y, float alto)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(0f, -y);
        r.sizeDelta = new Vector2(0f, alto);
    }

    static void Centrar(RectTransform r, Vector2 pos, Vector2 tamano)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = tamano;
    }

    // =============================================================== Sonidos (generados, no necesitan archivos)

    void CrearSonidos()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
        audioSrc.ignoreListenerPause = true;

        clipPunto = Tono("Punto", 880f, 1320f, 0.16f);
        clipTick = Tono("Tick", 1500f, 1500f, 0.035f);
        clipPerder = Tono("Perder", 320f, 140f, 0.4f);
        clipFinal = Tono("Final", 523f, 1046f, 0.45f);
    }

    void Sonar(AudioClip clip, float tono, float volumen)
    {
        if (clip == null || audioSrc == null) return;
        audioSrc.pitch = tono;
        audioSrc.PlayOneShot(clip, volumen);
    }

    static AudioClip Tono(string nombre, float freqInicio, float freqFin, float duracion)
    {
        int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * duracion));
        float[] datos = new float[n];
        float fase = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            float f = Mathf.Lerp(freqInicio, freqFin, k);
            fase += 2f * Mathf.PI * f / sr;
            float env = Mathf.Pow(1f - k, 2f) * Mathf.Clamp01(i / (sr * 0.004f));
            datos[i] = (Mathf.Sin(fase) * 0.7f + Mathf.Sin(fase * 2f) * 0.2f) * env * 0.5f;
        }
        AudioClip clip = AudioClip.Create(nombre, n, 1, sr, false);
        clip.SetData(datos, 0);
        return clip;
    }
}
