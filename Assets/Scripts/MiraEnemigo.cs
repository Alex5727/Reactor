using UnityEngine;
using UnityEngine.UI;

// Pinta la mira de rojo mientras el centro de la pantalla apunta a un enemigo.
[RequireComponent(typeof(Image))]
public class MiraEnemigo : MonoBehaviour
{
    public Color colorNormal = Color.black;
    public Color colorEnemigo = Color.red;
    public float distanciaMaxima = 200f;

    Image mira;
    Camera cam;
    int mascara;
    readonly RaycastHit[] hits = new RaycastHit[16];

    void Awake()
    {
        mira = GetComponent<Image>();
        mira.color = colorNormal;
        mascara = ~LayerMask.GetMask("Bullet", "Ignore Raycast");
    }

    void Update()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }
        mira.color = ApuntandoAEnemigo() ? colorEnemigo : colorNormal;
    }

    void OnDisable()
    {
        if (mira != null) mira.color = colorNormal;
    }

    bool ApuntandoAEnemigo()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int n = Physics.RaycastNonAlloc(ray, hits, distanciaMaxima, mascara, QueryTriggerInteraction.Ignore);

        // Lo primero que toca el rayo (ignorando al jugador y su arma)
        Transform jugador = cam.transform.root;
        Collider primero = null;
        float menor = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].collider.transform.root == jugador) continue;
            if (hits[i].distance < menor)
            {
                menor = hits[i].distance;
                primero = hits[i].collider;
            }
        }
        if (primero == null) return false;

        return primero.GetComponentInParent<SpiderEnemy>() != null
            || primero.GetComponentInParent<FlyingEnemy>() != null
            || primero.GetComponentInParent<BeamEnemy>() != null
            || primero.GetComponentInParent<BossSoldier>() != null;
    }
}
