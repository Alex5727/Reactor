using System.Collections.Generic;
using UnityEngine;

// Guarda un "rastro" de las ultimas posiciones del jugador. Los enemigos voladores lo usan para
// seguirlo cuando lo pierden de vista (dan la vuelta por puertas y pasillos en vez de quedarse
// pegados a la pared). Se crea solo la primera vez que un enemigo lo necesita.
public class RastroJugador : MonoBehaviour
{
    const int MaxPuntos = 80;
    const float Separacion = 1.5f;     // metros entre puntos
    const float Teletransporte = 12f;  // si el jugador "salta" mas que esto (reaparecer), se borra el rastro
    const int MaxRevisiones = 30;      // puntos que revisa cada busqueda (del mas nuevo al mas viejo)

    static RastroJugador instancia;
    static readonly RaycastHit[] buffer = new RaycastHit[16];

    readonly List<Vector3> puntos = new List<Vector3>();
    Transform jugador;

    static void Asegurar()
    {
        if (instancia != null) return;
        instancia = new GameObject("_RastroJugador").AddComponent<RastroJugador>();
    }

    void Update()
    {
        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            jugador = p.transform;
        }
        Vector3 pos = jugador.position + Vector3.up;
        if (puntos.Count == 0) { puntos.Add(pos); return; }

        float d = Vector3.Distance(pos, puntos[puntos.Count - 1]);
        if (d > Teletransporte) { puntos.Clear(); puntos.Add(pos); return; }
        if (d < Separacion) return;
        puntos.Add(pos);
        if (puntos.Count > MaxPuntos) puntos.RemoveAt(0);
    }

    void OnDestroy() { if (instancia == this) instancia = null; }

    // Devuelve el punto mas reciente del rastro que se ve libre desde "desde".
    public static bool PuntoVisible(Vector3 desde, float radio, LayerMask mascara, Transform yo, out Vector3 punto)
    {
        Asegurar();
        punto = Vector3.zero;
        List<Vector3> p = instancia.puntos;
        int revisados = 0;
        for (int i = p.Count - 1; i >= 0 && revisados < MaxRevisiones; i--, revisados++)
        {
            if (LineaLibre(desde, p[i], radio, mascara, yo))
            {
                punto = p[i];
                return true;
            }
        }
        return false;
    }

    // Hay espacio libre (sin paredes) entre los dos puntos para algo de este radio?
    // Ignora triggers, al propio enemigo y al jugador.
    public static bool LineaLibre(Vector3 desde, Vector3 hasta, float radio, LayerMask mascara, Transform yo)
    {
        Vector3 d = hasta - desde;
        float dist = d.magnitude;
        if (dist < 0.01f) return true;
        int n = Physics.SphereCastNonAlloc(desde, Mathf.Max(0.05f, radio * 0.8f), d / dist, buffer, dist, mascara, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = buffer[i].collider;
            if (c == null) continue;
            if (yo != null && c.transform.IsChildOf(yo)) continue;
            if (c.CompareTag("Player") || c.transform.root.CompareTag("Player")) continue;
            if (c.GetComponentInParent<IDamageable>() != null) continue;   // otros enemigos no tapan el camino
            return false;
        }
        return true;
    }
}
