using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Hacer siempre visible (seleccion)
// Selecciona el objeto que desaparece (el escalon) y usa este menu.
public static class SiempreVisibleEditor
{
    const string Menu = "Tools/Hacer siempre visible (seleccion)";

    [MenuItem(Menu)]
    static void HacerSiempreVisible()
    {
        GameObject[] seleccion = Selection.gameObjects;
        int piezas = 0;

        foreach (GameObject go in seleccion)
        {
            if (go.GetComponent<SiempreVisible>() == null) Undo.AddComponent<SiempreVisible>(go);
            piezas += QuitarMarcasOcclusion(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        bool hornear = EditorUtility.DisplayDialog("Siempre visible",
            seleccion.Length + " objeto(s) marcados como siempre visibles (" + piezas + " piezas ajustadas).\n\n" +
            "Para que el cambio tenga efecto hay que volver a hornear el Occlusion Culling. ¿Hornearlo ahora?\n" +
            "(puede tardar uno o dos minutos)", "Hornear ahora", "Despues");

        if (hornear)
        {
            bool ok = StaticOcclusionCulling.Compute();
            foreach (GameObject go in seleccion) EditorSceneManager.MarkSceneDirty(go.scene);
            EditorUtility.DisplayDialog("Siempre visible",
                ok ? "Listo. Guarda la escena (Ctrl+S)." : "No se pudo hornear; revisa la consola.", "OK");
        }
    }

    [MenuItem(Menu, true)]
    static bool Validar() => Selection.gameObjects.Length > 0;

    // Quita Occluder/Occludee Static del objeto y sus hijos (lo usa tambien OptimizarEscena)
    public static int QuitarMarcasOcclusion(GameObject raiz)
    {
        int n = 0;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            StaticEditorFlags f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            StaticEditorFlags nuevo = f & ~(StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            if (nuevo == f) continue;
            Undo.RecordObject(t.gameObject, "Siempre visible");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, nuevo);
            n++;
        }
        return n;
    }
}
