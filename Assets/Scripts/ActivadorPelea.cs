using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Activador de pelea: al tocarlo el jugador, la puerta se cambia por la puerta cerrada y no se abre
// hasta que mueren todos los enemigos que son hijos de este objeto.
// Solo se activa una vez por vida: si el jugador muere, la puerta se abre y se puede volver a activar
// cuando reaparezca.
// Se agrega SOLO a los objetos llamados "ActivadorPelea" (no hace falta ponerlo a mano).
// Modo Auto: si "PuertaPelea" empieza DESACTIVADA, se activa al cerrar y se desactiva al abrir (Nivel2);
// si empieza activa, se le pone encima la puerta cerrada de las salas especiales (NIvel).
public class ActivadorPelea : MonoBehaviour
{
    public enum ModoPuerta { Auto, CrearPuertaCerrada, ActivarObjeto }
    public ModoPuerta modoPuerta = ModoPuerta.Auto;
    public GameObject puerta;                  // vacio = busca el hijo "PuertaPelea"
    public GameObject puertaCerradaPrefab;     // vacio = usa la de las salas especiales (Wall DoorClosed)
    public float retrasoCierre = 0.4f;         // para que el jugador termine de entrar
    public AudioClip sonidoCerrar;
    public AudioClip sonidoAbrir;
    public string playerTag = "Player";

    bool usadoEstaVida;      // ya se activo en esta vida
    bool cerrada;
    GameObject puertaCreada;
    PlayerMovement jugador;
    bool jugadorEstabaMuerto;
    AudioSource audioPuerta;
    readonly List<MonoBehaviour> enemigos = new List<MonoBehaviour>();

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
                if (t.name == "ActivadorPelea" && t.GetComponent<ActivadorPelea>() == null)
                    t.gameObject.AddComponent<ActivadorPelea>();
    }

    // ------------------------------------------------------------------ inicio

    void Awake()
    {
        if (puerta == null)
        {
            Transform p = BuscarHijo(transform, "PuertaPelea");
            if (p != null) puerta = p.gameObject;
        }
        if (modoPuerta == ModoPuerta.Auto)
            modoPuerta = (puerta != null && !puerta.activeSelf) ? ModoPuerta.ActivarObjeto : ModoPuerta.CrearPuertaCerrada;

        if (modoPuerta == ModoPuerta.CrearPuertaCerrada && puertaCerradaPrefab == null)
        {
            SpecialRoom sala = FindAnyObjectByType<SpecialRoom>(FindObjectsInactive.Include);
            if (sala != null) puertaCerradaPrefab = sala.closedDoorPrefab;
        }
        if (puerta == null) Debug.LogWarning("ActivadorPelea: no se encontro el hijo 'PuertaPelea'.", this);
        if (modoPuerta == ModoPuerta.CrearPuertaCerrada && puertaCerradaPrefab == null) Debug.LogWarning("ActivadorPelea: no hay prefab de puerta cerrada.", this);

        AsegurarTrigger();
        audioPuerta = gameObject.AddComponent<AudioSource>();
        audioPuerta.playOnAwake = false;
        audioPuerta.spatialBlend = 0f;
    }

    // Si el collider es solido se agrega encima un trigger del mismo tamano (un poco mas grande)
    // para no cambiar el original. Si no tiene collider se agrega uno de 4x3x4.
    void AsegurarTrigger()
    {
        Collider c = GetComponent<Collider>();
        if (c != null && c.isTrigger) return;
        BoxCollider t = gameObject.AddComponent<BoxCollider>();
        t.isTrigger = true;
        if (c != null)
        {
            Bounds b = c.bounds;
            Vector3 s = transform.lossyScale;
            t.center = transform.InverseTransformPoint(b.center);
            t.size = new Vector3(b.size.x / Mathf.Max(s.x, 0.0001f), b.size.y / Mathf.Max(s.y, 0.0001f), b.size.z / Mathf.Max(s.z, 0.0001f)) * 1.1f;
        }
        else t.size = new Vector3(4f, 3f, 4f);
    }

    // ------------------------------------------------------------------ activacion

    void OnTriggerEnter(Collider other)
    {
        if (usadoEstaVida) return;
        PlayerMovement pm = other.GetComponentInParent<PlayerMovement>();
        if (pm == null && !other.CompareTag(playerTag)) return;
        if (pm != null) { if (pm.IsDead) return; jugador = pm; }

        usadoEstaVida = true;
        if (ContarEnemigosVivos() == 0) return;   // ya estaban muertos: no se cierra
        StartCoroutine(CerrarConRetraso());
    }

    IEnumerator CerrarConRetraso()
    {
        yield return new WaitForSeconds(retrasoCierre);
        if (usadoEstaVida && (jugador == null || !jugador.IsDead)) Cerrar();
    }

    void Update()
    {
        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) jugador = p.GetComponentInParent<PlayerMovement>();
        }

        // Puerta cerrada: se abre cuando mueren todos los enemigos hijos
        if (cerrada && ContarEnemigosVivos() == 0) Abrir();

        // El jugador murio: se abre la puerta y el activador queda listo para la siguiente vida
        if (jugador != null)
        {
            bool muerto = jugador.IsDead;
            if (muerto && !jugadorEstabaMuerto)
            {
                StopAllCoroutines();
                if (cerrada) Abrir();
                usadoEstaVida = false;
            }
            jugadorEstabaMuerto = muerto;
        }
    }

    int ContarEnemigosVivos()
    {
        enemigos.Clear();
        foreach (MonoBehaviour m in GetComponentsInChildren<MonoBehaviour>(false))
        {
            if (m == null || !m.isActiveAndEnabled) continue;
            if (puerta != null && m.transform.IsChildOf(puerta.transform)) continue;
            if (m is SpiderEnemy || m is FlyingEnemy || m is BeamEnemy) enemigos.Add(m);
        }
        return enemigos.Count;
    }

    // ------------------------------------------------------------------ puerta

    void Cerrar()
    {
        if (cerrada || puerta == null) return;
        if (modoPuerta == ModoPuerta.ActivarObjeto)
        {
            puerta.SetActive(true);
            cerrada = true;
            if (sonidoCerrar != null) audioPuerta.PlayOneShot(sonidoCerrar);
            return;
        }
        if (puertaCerradaPrefab == null) return;
        puertaCreada = Instantiate(puertaCerradaPrefab, puerta.transform);
        puertaCreada.transform.localPosition = Vector3.zero;
        puertaCreada.transform.localRotation = Quaternion.identity;
        puertaCreada.transform.localScale = Vector3.one;
        cerrada = true;
        if (sonidoCerrar != null) audioPuerta.PlayOneShot(sonidoCerrar);
    }

    void Abrir()
    {
        if (modoPuerta == ModoPuerta.ActivarObjeto && puerta != null) puerta.SetActive(false);
        if (puertaCreada != null) Destroy(puertaCreada);
        puertaCreada = null;
        cerrada = false;
        if (sonidoAbrir != null) audioPuerta.PlayOneShot(sonidoAbrir);
    }

    static Transform BuscarHijo(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }
}
