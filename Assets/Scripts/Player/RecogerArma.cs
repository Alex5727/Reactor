using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Va en el objeto recogible (fusilItem).
// - Al acercarte aparece en pantalla "[E] Recoger fusil".
// - Al presionar E, el arma que tiene el jugador (la del script Weapon) baja fuera de la vista,
//   se cambia por el prefab indicado y la nueva sube a su lugar (sin cambio brusco).
public class RecogerArma : MonoBehaviour
{
    [Header("Arma nueva")]
    public GameObject armaPrefab;                 // arrastra aqui el prefab "Fusil"

    [Header("Interaccion")]
    public float distanciaInteraccion = 2.5f;     // que tan cerca debe estar el jugador
    public string playerTag = "Player";
    public bool destruirAlRecoger = true;         // el fusilItem desaparece al recogerlo

    [Header("Aviso en pantalla")]
    public string textoAccion = "Recoger fusil";
    public TMP_FontAsset fuente;                  // opcional

    [Header("Posicion del arma nueva (relativa a la camara)")]
    public bool usarPosicionDeLaPistola = true;   // la coloca donde estaba la pistola
    public Vector3 posicionLocal;                 // se usa si lo de arriba esta desactivado
    public Vector3 rotacionLocal;

    [Header("Animacion de cambio de arma")]
    public float duracionBajar = 0.25f;
    public float duracionSubir = 0.35f;
    public Vector3 desplazamientoBajada = new Vector3(0f, -0.6f, -0.1f);  // hacia donde se esconde el arma
    public Vector3 giroBajada = new Vector3(40f, 0f, -15f);               // grados que gira al esconderse
    public AudioClip sonidoCambio;                                        // opcional

    private Transform jugador;
    private bool cambiando;

    void Update()
    {
        if (cambiando) return;

        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p == null) return;
            jugador = p.transform;
        }

        bool cerca = Vector3.Distance(jugador.position, transform.position) <= distanciaInteraccion;

        if (cerca) AvisoInteraccion.Mostrar(this, textoAccion, fuente);
        else AvisoInteraccion.Ocultar(this);

        if (cerca && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            CambiarArma();
    }

    void OnDisable()
    {
        AvisoInteraccion.Ocultar(this);
    }

    void CambiarArma()
    {
        if (armaPrefab == null)
        {
            Debug.LogWarning("RecogerArma: falta asignar el prefab del arma.", this);
            return;
        }

        Weapon actual = jugador.GetComponentInChildren<Weapon>();
        if (actual == null)
        {
            Debug.LogWarning("RecogerArma: el jugador no tiene ningun arma con el script Weapon.", this);
            return;
        }

        StartCoroutine(AnimacionCambio(actual));
    }

    IEnumerator AnimacionCambio(Weapon actual)
    {
        cambiando = true;
        AvisoInteraccion.Ocultar(this);
        OcultarObjeto();

        // Contenedor que se anima (asi no choca con el retroceso que Weapon aplica al arma)
        Transform porta = ObtenerPortaArma(actual.transform);

        if (sonidoCambio != null) AudioSource.PlayClipAtPoint(sonidoCambio, porta.position, 1f);

        // 1) Baja el arma actual (y deja de disparar)
        actual.enabled = false;
        Quaternion rotBajada = Quaternion.Euler(giroBajada);
        for (float t = 0f; t < duracionBajar; t += Time.deltaTime)
        {
            float k = t / duracionBajar;
            k = k * k;                                        // acelera al esconderse
            porta.localPosition = Vector3.Lerp(Vector3.zero, desplazamientoBajada, k);
            porta.localRotation = Quaternion.Slerp(Quaternion.identity, rotBajada, k);
            yield return null;
        }
        porta.localPosition = desplazamientoBajada;
        porta.localRotation = rotBajada;

        // 2) Cambia el modelo (fuera de la vista)
        Weapon nueva = CrearArmaNueva(actual, porta);
        Destroy(actual.gameObject);
        if (nueva != null) nueva.enabled = false;            // no dispara hasta terminar de subir

        // 3) Sube la nueva con un pequeno rebote
        for (float t = 0f; t < duracionSubir; t += Time.deltaTime)
        {
            float k = t / duracionSubir;
            float s = 1.70158f;
            float e = 1f + (s + 1f) * Mathf.Pow(k - 1f, 3f) + s * Mathf.Pow(k - 1f, 2f);   // ease-out-back
            porta.localPosition = Vector3.LerpUnclamped(desplazamientoBajada, Vector3.zero, e);
            porta.localRotation = Quaternion.SlerpUnclamped(rotBajada, Quaternion.identity, e);
            yield return null;
        }
        porta.localPosition = Vector3.zero;
        porta.localRotation = Quaternion.identity;

        if (nueva != null) nueva.enabled = true;

        if (destruirAlRecoger) Destroy(gameObject);
        else { cambiando = false; enabled = false; }
    }

    // Inserta un objeto vacio "PortaArma" entre la camara y el arma (solo la primera vez)
    static Transform ObtenerPortaArma(Transform arma)
    {
        Transform padre = arma.parent;
        if (padre != null && padre.name == "PortaArma") return padre;

        GameObject go = new GameObject("PortaArma");
        Transform porta = go.transform;
        porta.SetParent(padre, false);
        porta.localPosition = Vector3.zero;
        porta.localRotation = Quaternion.identity;
        porta.localScale = Vector3.one;
        go.layer = arma.gameObject.layer;
        arma.SetParent(porta, false);       // misma posicion local -> misma posicion en pantalla
        return porta;
    }

    // Oculta el objeto del suelo de inmediato (se destruye al terminar la animacion)
    void OcultarObjeto()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    Weapon CrearArmaNueva(Weapon actual, Transform padre)
    {
        GameObject nueva = Instantiate(armaPrefab, padre);
        nueva.name = armaPrefab.name;

        if (usarPosicionDeLaPistola)
        {
            nueva.transform.localPosition = actual.transform.localPosition;
            nueva.transform.localRotation = actual.transform.localRotation;
        }
        else
        {
            nueva.transform.localPosition = posicionLocal;
            nueva.transform.localRotation = Quaternion.Euler(rotacionLocal);
        }

        SetLayer(nueva.transform, actual.gameObject.layer);

        // Pasar las referencias de escena que el prefab no puede guardar
        Weapon w = nueva.GetComponent<Weapon>();
        if (w == null) w = nueva.GetComponentInChildren<Weapon>();
        if (w != null)
        {
            w.playerCamera = actual.playerCamera;
            w.barraRecarga = actual.barraRecarga;
            w.barraRecarga2 = actual.barraRecarga2;
            w.textoContadorBalas = actual.textoContadorBalas;

            if (w.weaponTransform == null) w.weaponTransform = w.transform;

            if (w.muzzle == null)
            {
                Transform boquilla = BuscarHijo(nueva.transform, "boquilla");
                w.muzzle = boquilla != null ? boquilla : w.transform;
            }

            if (w.audioSource == null) w.audioSource = CopiarAudioSource(actual.audioSource, w.gameObject);
        }
        return w;
    }

    static AudioSource CopiarAudioSource(AudioSource original, GameObject destino)
    {
        AudioSource a = destino.AddComponent<AudioSource>();
        a.playOnAwake = false;
        if (original != null)
        {
            a.outputAudioMixerGroup = original.outputAudioMixerGroup;
            a.volume = original.volume;
            a.spatialBlend = original.spatialBlend;
            a.priority = original.priority;
        }
        return a;
    }

    static Transform BuscarHijo(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name.ToLower() == nombre) return t;
        return null;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform hijo in t) SetLayer(hijo, layer);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaInteraccion);
    }
}

// Aviso "[E] Accion" en la parte baja de la pantalla. Lo comparten todos los objetos recogibles.
public class AvisoInteraccion : MonoBehaviour
{
    static AvisoInteraccion instancia;

    Object duenio;
    CanvasGroup grupo;
    RectTransform panel, tecla;
    TextMeshProUGUI texto, letra;
    float alfaObjetivo;
    float aparicion;

    static Sprite spriteBlanco;

    public static void Mostrar(Object quien, string accion, TMP_FontAsset fuente)
    {
        if (instancia == null) Crear(fuente);
        if (instancia.duenio != quien || instancia.alfaObjetivo < 1f) instancia.aparicion = 0f;
        instancia.duenio = quien;
        instancia.texto.text = accion;
        instancia.alfaObjetivo = 1f;
    }

    public static void Ocultar(Object quien)
    {
        if (instancia == null || instancia.duenio != quien) return;
        instancia.alfaObjetivo = 0f;
    }

    static void Crear(TMP_FontAsset fuente)
    {
        GameObject go = new GameObject("AvisoInteraccion", typeof(RectTransform));
        instancia = go.AddComponent<AvisoInteraccion>();

        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 40;
        CanvasScaler s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        instancia.grupo = go.AddComponent<CanvasGroup>();
        instancia.grupo.alpha = 0f;
        instancia.grupo.blocksRaycasts = false;

        // Panel
        Image fondo = Imagen("Panel", (RectTransform)go.transform, new Color(0f, 0f, 0f, 0.55f));
        instancia.panel = fondo.rectTransform;
        instancia.panel.anchorMin = instancia.panel.anchorMax = new Vector2(0.5f, 0f);
        instancia.panel.pivot = new Vector2(0.5f, 0.5f);
        instancia.panel.anchoredPosition = new Vector2(0f, 230f);
        instancia.panel.sizeDelta = new Vector2(420f, 70f);

        // Tecla "E"
        Image borde = Imagen("Tecla", instancia.panel, Color.white);
        instancia.tecla = borde.rectTransform;
        instancia.tecla.anchorMin = instancia.tecla.anchorMax = new Vector2(0f, 0.5f);
        instancia.tecla.pivot = new Vector2(0.5f, 0.5f);
        instancia.tecla.anchoredPosition = new Vector2(48f, 0f);
        instancia.tecla.sizeDelta = new Vector2(48f, 48f);
        Image interior = Imagen("Interior", instancia.tecla, new Color(0.1f, 0.1f, 0.1f, 1f));
        RectTransform ri = interior.rectTransform;
        ri.anchorMin = Vector2.zero; ri.anchorMax = Vector2.one;
        ri.offsetMin = new Vector2(3f, 3f); ri.offsetMax = new Vector2(-3f, -3f);

        instancia.letra = Texto("E", instancia.tecla, 32f, fuente);
        RectTransform rl = instancia.letra.rectTransform;
        rl.anchorMin = Vector2.zero; rl.anchorMax = Vector2.one;
        rl.offsetMin = rl.offsetMax = Vector2.zero;
        instancia.letra.alignment = TextAlignmentOptions.Center;
        instancia.letra.text = "E";

        instancia.texto = Texto("Accion", instancia.panel, 30f, fuente);
        RectTransform rt = instancia.texto.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(90f, 0f); rt.offsetMax = new Vector2(-16f, 0f);
        instancia.texto.alignment = TextAlignmentOptions.Left;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo, dt * 6f);

        // Pequeno "pop" al aparecer y la tecla late suavemente
        aparicion = Mathf.MoveTowards(aparicion, 1f, dt * 5f);
        float pop = 1f + 0.15f * Mathf.Sin(aparicion * Mathf.PI) * (1f - aparicion * 0.5f);
        panel.localScale = Vector3.one * pop;
        tecla.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f));

        // Ajusta el ancho al texto
        float ancho = Mathf.Clamp(texto.preferredWidth + 120f, 220f, 900f);
        panel.sizeDelta = new Vector2(ancho, 70f);
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

    static TextMeshProUGUI Texto(string n, RectTransform padre, float tam, TMP_FontAsset fuente)
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
