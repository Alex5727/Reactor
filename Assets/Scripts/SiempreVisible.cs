using UnityEngine;

// Pon este componente en un objeto que el Occlusion Culling esconde por error
// (por ejemplo un escalon bajo que no se ve hasta que saltas hacia el).
// - En el juego: sus renderers nunca se ocultan por occlusion culling.
// - En el editor: Tools > Hacer siempre visible (seleccion) le quita las marcas Occluder/Occludee
//   y vuelve a hornear; la herramienta "Optimizar escena abierta" tambien lo respeta.
[DisallowMultipleComponent]
public class SiempreVisible : MonoBehaviour
{
    void Awake()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            r.allowOcclusionWhenDynamic = false;
    }
}
