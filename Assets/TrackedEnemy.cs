using System.Collections.Generic;
using UnityEngine;

// Agrégalo a la raíz de CADA enemigo (el mismo objeto que se destruye al morir).
// Al iniciar guarda una copia limpia del enemigo; al reaparecer el jugador, los enemigos que
// no estén "asegurados" por un checkpoint se vuelven a crear desde esa copia (vida completa,
// posición original, estado inicial). No hace falta tocar los scripts de los enemigos.
[DefaultExecutionOrder(-1000)]   // corre antes que los demás scripts para copiar el enemigo "limpio"
public class TrackedEnemy : MonoBehaviour
{
    public class EnemySlot
    {
        public GameObject template;      // copia limpia (desactivada)
        public Vector3 position;
        public Quaternion rotation;
        public Transform parent;
        public TrackedEnemy current;     // el enemigo vivo actualmente (null si está muerto)
        public bool deadPending;         // murió después del último checkpoint
        public bool deadCommitted;       // murió antes del último checkpoint: se queda muerto
    }

    private static readonly List<EnemySlot> Slots = new List<EnemySlot>();
    private static GameObject templateHolder;

    private EnemySlot slot;
    private bool suppressDeath;
    private bool quitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Slots.Clear();
        templateHolder = null;
    }

    void Awake()
    {
        // Si es una copia revivida, el slot ya viene asignado
        if (slot != null) return;

        // Primera vez (enemigo puesto en la escena): guarda una copia limpia
        if (templateHolder == null)
        {
            templateHolder = new GameObject("_EnemyTemplates");
            templateHolder.SetActive(false);   // todo lo que cuelgue de aquí no "despierta"
        }

        GameObject template = Instantiate(gameObject, templateHolder.transform);
        template.name = gameObject.name;
        template.SetActive(false);

        slot = new EnemySlot
        {
            template = template,
            position = transform.position,
            rotation = transform.rotation,
            parent = transform.parent,
            current = this
        };
        Slots.Add(slot);
    }

    void OnApplicationQuit()
    {
        quitting = true;
    }

    void OnDestroy()
    {
        if (slot == null || quitting || suppressDeath) return;

        // Murió de verdad (bala, explosión de la araña, etc.)
        slot.current = null;
        if (!slot.deadCommitted) slot.deadPending = true;
    }

    // ================================================================ API (la usa el CheckpointManager)

    // Al tocar un checkpoint: las muertes pendientes pasan a ser definitivas
    public static int CommitDeaths()
    {
        int count = 0;
        foreach (EnemySlot s in Slots)
        {
            if (s.deadPending)
            {
                s.deadPending = false;
                s.deadCommitted = true;
                count++;
            }
        }
        return count;
    }

    // Al reaparecer el jugador: vuelve a crear todos los enemigos que no estén muertos "definitivamente"
    public static int RespawnAll()
    {
        int recreated = 0;

        for (int i = Slots.Count - 1; i >= 0; i--)
        {
            EnemySlot s = Slots[i];

            if (s.template == null)              // copia de una escena anterior
            {
                Slots.RemoveAt(i);
                continue;
            }

            if (s.deadCommitted) continue;       // murió antes del último checkpoint: se queda muerto

            // Quita al enemigo actual (si sigue vivo) sin contarlo como muerte
            if (s.current != null)
            {
                s.current.suppressDeath = true;
                s.current.gameObject.SetActive(false);
                Destroy(s.current.gameObject);
                s.current = null;
            }

            s.deadPending = false;

            // Crea uno nuevo a partir de la copia limpia
            Transform parent = s.parent != null ? s.parent : null;
            GameObject go = Instantiate(s.template, s.position, s.rotation, parent);
            go.name = s.template.name;

            TrackedEnemy te = go.GetComponent<TrackedEnemy>();
            te.slot = s;                         // antes de activarlo, para que no cree otra copia
            s.current = te;

            go.SetActive(true);
            recreated++;
        }

        return recreated;
    }
}
