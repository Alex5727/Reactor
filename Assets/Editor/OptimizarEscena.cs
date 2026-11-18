using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Tools > Optimizar escena abierta
// 1) Luces: las luces puntuales/spot pequenas (rango <= 15) dejan de proyectar sombra
//    (casi no se notan y cada una vuelve a dibujar la escena 6 veces). Las grandes conservan
//    su sombra con resolucion media.
// 2) Occlusion Culling: marca las paredes/pisos/techos/pasillos del kit como Occluder/Occludee
//    y hornea el occlusion culling, para que Unity no dibuje las salas que estan detras de las paredes.
// Despues de ejecutarla, guarda la escena (Ctrl+S). Hazlo en NIvel y en Nivel2.
public static class OptimizarEscena
{
    const float RangoLuzPequena = 15f;

    [MenuItem("Tools/Optimizar escena abierta")]
    static void Optimizar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog("Optimizar escena",
            "Se optimizaran las luces y se hornearan los datos de Occlusion Culling de la escena '" + escena.name +
            "'.\n\nEl horneado puede tardar uno o dos minutos. Al terminar, guarda la escena.", "Optimizar", "Cancelar"))
            return;

        int lucesSinSombra = OptimizarLuces(escena);
        int occluders = MarcarOcclusion(escena);

        EditorSceneManager.MarkSceneDirty(escena);

        StaticOcclusionCulling.smallestOccluder = 3f;
        StaticOcclusionCulling.smallestHole = 0.25f;
        StaticOcclusionCulling.backfaceThreshold = 100f;
        bool horneado = StaticOcclusionCulling.Compute();

        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log("OptimizarEscena '" + escena.name + "': " + lucesSinSombra + " luces pequenas sin sombra, " +
                  occluders + " piezas marcadas para occlusion culling, horneado " + (horneado ? "correcto" : "FALLO") +
                  ". Guarda la escena.");
        EditorUtility.DisplayDialog("Optimizar escena",
            "Listo.\n\nLuces pequenas sin sombra: " + lucesSinSombra + "\nPiezas para occlusion culling: " + occluders +
            "\nOcclusion culling: " + (horneado ? "horneado" : "no se pudo hornear (revisa la consola)") +
            "\n\nGuarda la escena con Ctrl+S.", "OK");
    }

    static int OptimizarLuces(UnityEngine.SceneManagement.Scene escena)
    {
        int sinSombra = 0;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.gameObject.scene != escena) continue;
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            if (l.shadows == LightShadows.None) continue;

            Undo.RecordObject(l, "Optimizar luces");
            if (l.range <= RangoLuzPequena)
            {
                l.shadows = LightShadows.None;
                sinSombra++;
            }
            else
            {
                var datos = l.GetComponent<UniversalAdditionalLightData>();
                if (datos != null)
                {
                    // Resolucion de sombra "Medium" (0 = Low, 1 = Medium, 2 = High)
                    var so = new SerializedObject(datos);
                    var tier = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
                    if (tier != null && tier.intValue > 1)
                    {
                        tier.intValue = 1;
                        so.ApplyModifiedProperties();
                    }
                }
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(l);
        }
        return sinSombra;
    }

    static int MarcarOcclusion(UnityEngine.SceneManagement.Scene escena)
    {
        int cuenta = 0;
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject go = r.gameObject;
            if (go.scene != escena) continue;

            GameObject origen = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            string ruta = origen != null ? AssetDatabase.GetAssetPath(origen) : "";
            if (!ruta.Contains("/Prefabs/Structures/")) continue;

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            flags |= StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

            // Solo las piezas solidas tapan la vista; rejillas, ventanas y tragaluces no.
            string nombre = (ruta + "/" + go.name).ToLower();
            bool transparente = nombre.Contains("window") || nombre.Contains("skylight") || nombre.Contains("catwalk") ||
                                nombre.Contains("stairs") || nombre.Contains("rail") || nombre.Contains("structure props") ||
                                nombre.Contains("glass");
            if (!transparente) flags |= StaticEditorFlags.OccluderStatic;
            else flags &= ~StaticEditorFlags.OccluderStatic;

            Undo.RecordObject(go, "Occlusion culling");
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            cuenta++;
        }
        return cuenta;
    }
}
