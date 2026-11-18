using System.Collections;
using UnityEngine;

// Va en el objeto "Actiador" de la sala del jefe.
// - Antes de tocarlo: el jefe no recibe dano ni empieza a pelear.
// - Al tocarlo: empieza la pelea, se cierran PuertaEntrada y PuertaSalida y (opcional) cambia la musica.
// - Al derrotar al jefe: se abren las puertas y vuelve la musica normal.
// - Si el jugador muere: se abren las puertas, el jefe se reinicia y el activador se puede volver a tocar.
public class ActivadorJefe : MonoBehaviour
{
    public enum ModoPuerta
    {
        CrearPuertaCerrada,   // pone una "Wall DoorClosed" encima de la puerta (como las salas especiales)
        ActivarObjeto         // la puerta es un objeto que se activa al cerrar y se desactiva al abrir
    }

    [Header("Jefe")]
    public BossSoldier jefe;                      // vacio = busca el BossSoldier de la escena

    [Header("Puertas")]
    public GameObject[] puertas;                  // vacio = busca "PuertaEntrada" y "PuertaSalida"
    public ModoPuerta modoPuerta = ModoPuerta.CrearPuertaCerrada;
    public GameObject puertaCerradaPrefab;        // vacio = usa el de las salas especiales (Wall DoorClosed)
    public float retrasoCierre = 0.4f;            // para que el jugador termine de entrar
    public AudioClip sonidoCerrar;
    public AudioClip sonidoAbrir;

    [Header("Musica (opcional)")]
    public AudioClip musicaJefe;                  // vacio = no cambia la musica
    public AudioSource fuenteMusica;              // vacio = busca "BackgroundMusic"
    public float duracionFundido = 1.5f;

    public string playerTag = "Player";

    private GameObject[] puertasCreadas;
    private bool peleaEnCurso, jefeDerrotado;
    private PlayerMovement jugador;
    private bool jugadorEstabaMuerto;
    private AudioClip musicaOriginal;
    private float volumenOriginal = 1f;
    private bool musicaCambiada;
    private Coroutine fundido;
    private AudioSource audioPuertas;

    // ================================================================== Inicio

    void Awake()
    {
        if (jefe == null) jefe = FindAnyObjectByType<BossSoldier>();

        if (puertas == null || puertas.Length == 0)
        {
            GameObject a = BuscarObjeto("PuertaEntrada");
            GameObject b = BuscarObjeto("PuertaSalida");
            puertas = new[] { a, b };
        }
        puertasCreadas = new GameObject[puertas.Length];

        if (puertaCerradaPrefab == null)
        {
            SpecialRoom sala = FindAnyObjectByType<SpecialRoom>(FindObjectsInactive.Include);
            if (sala != null) puertaCerradaPrefab = sala.closedDoorPrefab;
        }

        if (fuenteMusica == null)
        {
            GameObject bm = BuscarObjeto("BackgroundMusic");
            if (bm != null) fuenteMusica = bm.GetComponent<AudioSource>();
        }

        AsegurarTrigger();
        audioPuertas = gameObject.AddComponent<AudioSource>();
        audioPuertas.playOnAwake = false;
        audioPuertas.spatialBlend = 0f;
    }

    void Start()
    {
        if (jefe == null)
        {
            Debug.LogWarning("ActivadorJefe: no hay ningun BossSoldier en la escena.", this);
            return;
        }
        jefe.BloquearPelea();
        jefe.alMorir.AddListener(AlMorirJefe);
    }

    // Asegura que se pueda "tocar": si no tiene collider se agrega uno; si es solido se agrega
    // un trigger un poco mas grande encima para no cambiar el original.
    void AsegurarTrigger()
    {
        Collider c = GetComponent<Collider>();
        if (c != null && c.isTrigger) return;

        BoxCollider t = gameObject.AddComponent<BoxCollider>();
        t.isTrigger = true;
        if (c != null)
        {
            Bounds b = c.bounds;
            t.center = transform.InverseTransformPoint(b.center);
            Vector3 s = transform.lossyScale;
            t.size = new Vector3(b.size.x / Mathf.Max(s.x, 0.0001f), b.size.y / Mathf.Max(s.y, 0.0001f), b.size.z / Mathf.Max(s.z, 0.0001f)) * 1.2f;
        }
    }

    // ================================================================== Activacion

    void OnTriggerEnter(Collider other)
    {
        if (peleaEnCurso || jefeDerrotado || jefe == null || jefe.Muerto) return;

        PlayerMovement pm = other.GetComponentInParent<PlayerMovement>();
        if (pm == null && !other.CompareTag(playerTag)) return;
        if (pm != null && pm.IsDead) return;
        if (pm != null) jugador = pm;

        peleaEnCurso = true;
        jefe.IniciarPelea();
        StartCoroutine(CerrarConRetraso());
        CambiarMusica(musicaJefe, true);
    }

    IEnumerator CerrarConRetraso()
    {
        yield return new WaitForSeconds(retrasoCierre);
        if (peleaEnCurso) CerrarPuertas();
    }

    void Update()
    {
        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) jugador = p.GetComponentInParent<PlayerMovement>();
            if (jugador == null) return;
        }

        // El jugador murio durante la pelea: se abre todo y se puede volver a intentar
        bool muerto = jugador.IsDead;
        if (muerto && !jugadorEstabaMuerto && peleaEnCurso && !jefeDerrotado)
        {
            peleaEnCurso = false;
            StopAllCoroutines();
            AbrirPuertas();
            jefe.BloquearPelea();
            CambiarMusica(musicaOriginal, false);
        }
        jugadorEstabaMuerto = muerto;
    }

    void AlMorirJefe()
    {
        jefeDerrotado = true;
        peleaEnCurso = false;
        AbrirPuertas();
        CambiarMusica(musicaOriginal, false);
    }

    // ================================================================== Puertas

    void CerrarPuertas()
    {
        for (int i = 0; i < puertas.Length; i++)
        {
            GameObject p = puertas[i];
            if (p == null) continue;

            if (modoPuerta == ModoPuerta.ActivarObjeto)
            {
                p.SetActive(true);
            }
            else if (puertaCerradaPrefab != null && puertasCreadas[i] == null)
            {
                puertasCreadas[i] = Instantiate(puertaCerradaPrefab, p.transform);
                puertasCreadas[i].transform.localPosition = Vector3.zero;
                puertasCreadas[i].transform.localRotation = Quaternion.identity;
                puertasCreadas[i].transform.localScale = Vector3.one;
            }
        }
        if (sonidoCerrar != null) audioPuertas.PlayOneShot(sonidoCerrar);
    }

    void AbrirPuertas()
    {
        for (int i = 0; i < puertas.Length; i++)
        {
            if (modoPuerta == ModoPuerta.ActivarObjeto)
            {
                if (puertas[i] != null) puertas[i].SetActive(false);
            }
            else if (puertasCreadas[i] != null)
            {
                Destroy(puertasCreadas[i]);
                puertasCreadas[i] = null;
            }
        }
        if (sonidoAbrir != null) audioPuertas.PlayOneShot(sonidoAbrir);
    }

    // ================================================================== Musica

    void CambiarMusica(AudioClip clip, bool aJefe)
    {
        if (fuenteMusica == null) return;
        if (aJefe)
        {
            if (clip == null || musicaCambiada) return;
            musicaOriginal = fuenteMusica.clip;
            volumenOriginal = fuenteMusica.volume;
            musicaCambiada = true;
        }
        else
        {
            if (!musicaCambiada) return;
            musicaCambiada = false;
            clip = musicaOriginal;
        }
        if (fundido != null) StopCoroutine(fundido);
        fundido = StartCoroutine(Fundido(clip));
    }

    IEnumerator Fundido(AudioClip nuevo)
    {
        float mitad = Mathf.Max(0.01f, duracionFundido * 0.5f);
        float inicio = fuenteMusica.volume;
        for (float t = 0f; t < mitad; t += Time.unscaledDeltaTime)
        {
            fuenteMusica.volume = Mathf.Lerp(inicio, 0f, t / mitad);
            yield return null;
        }
        fuenteMusica.clip = nuevo;
        if (nuevo != null) fuenteMusica.Play();
        for (float t = 0f; t < mitad; t += Time.unscaledDeltaTime)
        {
            fuenteMusica.volume = Mathf.Lerp(0f, volumenOriginal, t / mitad);
            yield return null;
        }
        fuenteMusica.volume = volumenOriginal;
        fundido = null;
    }

    // ================================================================== Utilidades

    static GameObject BuscarObjeto(string nombre)
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == nombre && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }
}
