using UnityEngine;
using UnityEngine.InputSystem;

// Va en el objeto recogible (fusilItem).
// Cuando el jugador esta cerca y presiona E, el arma que tiene en las manos
// (la que lleva el script Weapon, hija del jugador) se cambia por el prefab indicado.
public class RecogerArma : MonoBehaviour
{
    [Header("Arma nueva")]
    public GameObject armaPrefab;                 // arrastra aqui el prefab "Fusil"

    [Header("Interaccion")]
    public float distanciaInteraccion = 2.5f;     // que tan cerca debe estar el jugador
    public string playerTag = "Player";
    public bool destruirAlRecoger = true;         // el fusilItem desaparece al recogerlo

    [Header("Posicion del arma nueva (relativa a la camara)")]
    public bool usarPosicionDeLaPistola = true;   // la coloca donde estaba la pistola
    public Vector3 posicionLocal;                 // se usa si lo de arriba esta desactivado
    public Vector3 rotacionLocal;

    private Transform jugador;

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p == null) return;
            jugador = p.transform;
        }

        if (Vector3.Distance(jugador.position, transform.position) > distanciaInteraccion) return;

        CambiarArma();
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

        // Crear la nueva arma en el mismo lugar de la jerarquia que la pistola
        Transform padre = actual.transform.parent;
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
        // (se hace antes de su Start, que corre hasta el siguiente frame)
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

        // Quitar la pistola
        Destroy(actual.gameObject);

        if (destruirAlRecoger) Destroy(gameObject);
        else enabled = false;
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
