using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Arregla las fuentes FA-1 / FA-2: se habian generado con Padding 0, por eso cada letra
// se veia como un bloque de color. Regenera su atlas SDF con padding correcto conservando
// el mismo archivo y el mismo material, asi todo lo que ya las usa (menu de pausa, puntos...)
// queda arreglado sin reasignar nada.
// Se ejecuta sola una vez; tambien puedes correrla desde Tools > Arreglar fuentes FA.
public static class RegenerarFuentesFA
{
    const string FuenteOrigen = "Assets/Fonts/FA-1-Regular.otf";
    static readonly string[] FontAssets =
    {
        "Assets/Fonts/FA-1-Regular SDF.asset",
        "Assets/Fonts/FA-2-Regular SDF.asset"
    };

    const int TamanoMuestreo = 90;
    const int Padding = 9;
    const int TamanoAtlas = 1024;
    const string ClaveHecho = "Reactor.FuentesFA.Regeneradas.v1";

    const string CaracteresBase =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        "ÁÉÍÓÚÜÑáéíóúüñ¿¡°";

    [InitializeOnLoadMethod]
    static void EjecutarUnaVez()
    {
        if (EditorPrefs.GetBool(ClaveHecho, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (Regenerar()) EditorPrefs.SetBool(ClaveHecho, true);
        };
    }

    [MenuItem("Tools/Arreglar fuentes FA")]
    static void DesdeMenu()
    {
        Regenerar();
    }

    static bool Regenerar()
    {
        Font fuente = AssetDatabase.LoadAssetAtPath<Font>(FuenteOrigen);
        if (fuente == null)
        {
            Debug.LogWarning("RegenerarFuentesFA: no encontre " + FuenteOrigen);
            return false;
        }

        bool ok = true;
        foreach (string ruta in FontAssets)
            ok &= RegenerarUno(fuente, ruta);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (ok) Debug.Log("RegenerarFuentesFA: fuentes FA regeneradas con padding " + Padding + ".");
        return ok;
    }

    static bool RegenerarUno(Font fuente, string ruta)
    {
        TMP_FontAsset viejo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ruta);
        if (viejo == null) return true;   // no existe, nada que hacer

        string nombre = viejo.name;
        Material materialViejo = viejo.material;

        // Mismos caracteres que tenia + los basicos (con acentos)
        string caracteres = CaracteresBase;
        if (viejo.characterTable != null)
            caracteres += new string(viejo.characterTable.Where(c => c.unicode < 0xFFFF).Select(c => (char)c.unicode).ToArray());

        TMP_FontAsset nuevo = TMP_FontAsset.CreateFontAsset(
            fuente, TamanoMuestreo, Padding, GlyphRenderMode.SDFAA,
            TamanoAtlas, TamanoAtlas, AtlasPopulationMode.Dynamic, false);
        if (nuevo == null)
        {
            Debug.LogWarning("RegenerarFuentesFA: no se pudo crear el atlas para " + ruta);
            return false;
        }
        nuevo.TryAddCharacters(caracteres, out string _);

        Texture2D texturaNueva = nuevo.atlasTexture;
        Material materialNuevo = nuevo.material;

        // Quitar el atlas viejo del archivo
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(ruta))
            if (o is Texture2D) Object.DestroyImmediate(o, true);

        // Copiar los datos nuevos (glifos, metricas, padding...) sobre el asset existente
        EditorUtility.CopySerialized(nuevo, viejo);
        viejo.name = nombre;

        texturaNueva.name = nombre + " Atlas";
        AssetDatabase.AddObjectToAsset(texturaNueva, viejo);
        viejo.atlasTextures = new[] { texturaNueva };

        if (materialViejo != null)
        {
            // Se conserva el mismo material (los textos de las escenas lo referencian)
            materialViejo.shader = materialNuevo.shader;
            materialViejo.CopyPropertiesFromMaterial(materialNuevo);
            materialViejo.SetTexture(ShaderUtilities.ID_MainTex, texturaNueva);
            viejo.material = materialViejo;
            EditorUtility.SetDirty(materialViejo);
            Object.DestroyImmediate(materialNuevo);
        }
        else
        {
            materialNuevo.name = nombre + " Material";
            AssetDatabase.AddObjectToAsset(materialNuevo, viejo);
            viejo.material = materialNuevo;
        }

        viejo.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(viejo);
        // (el asset temporal 'nuevo' no se destruye a mano para no arrastrar su atlas)
        return true;
    }
}
