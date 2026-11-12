using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú del Editor: EcoCultivo > Crear depósitos de agua  /  Quitar depósitos de agua.
///
/// Ronda 11 — Coloca un depósito de solución nutritiva al pie de cada módulo (EstructuraMacetero),
/// en el extremo que da hacia el exterior del invernadero (el pasillo central queda libre).
/// Es SOLO VISUAL: no tiene script ni colisión.
///
/// Cada depósito (≈ 100 L):
/// - Tanque cilíndrico Ø 55 cm × 45 cm, con tapa.
/// - Tubo de subida (PVC Ø 5 cm): sale de la tapa y sube hasta el canal superior.
/// - Dos tubos de retorno desde los canales inferiores hasta el tanque.
/// - Coples verdes en los codos (material de las válvulas).
///
/// RENDIMIENTO: cilindros de Unity sin colisionador (≈ 80 triángulos c/u), ≈ 1.8 mil triángulos
/// por depósito (≈ 7 mil en total), 3 materiales ya usados en la escena + 1 nuevo (Mat_Deposito, misma variante URP Lit).
/// Proyectan sombra (se ven con las lámparas). Marcados como estáticos.
///
/// En la escena crea "Depositos_Agua" (Ctrl+Z lo deshace). Se pueden mover a mano después.
/// </summary>
public static class CrearDepositos
{
    private const string NOMBRE_RAIZ = "Depositos_Agua";
    private const string RUTA_MAT = "Assets/EcoCultivo/Materiales/Mat_Deposito.mat";

    // Medidas (m)
    private const float RADIO_TANQUE = 0.275f;   // Ø 55 cm
    private const float ALTO_TANQUE = 0.45f;     // ≈ 107 L
    private const float SEPARACION = 0.12f;      // entre el extremo del módulo y el borde del tanque
    private const float DIAM_TUBO = 0.05f;
    private const float DIAM_COPLE = 0.075f;

    [MenuItem("EcoCultivo/Crear depósitos de agua")]
    private static void Crear()
    {
        var escena = EditorSceneManager.GetActiveScene();
        List<LODGroup> modulos = BuscarModulos(escena);
        if (modulos.Count == 0)
        {
            EditorUtility.DisplayDialog("EcoCultivo", "No encontré ningún EstructuraMacetero (LOD Group) en la escena abierta.", "OK");
            return;
        }

        GameObject previo = GameObject.Find(NOMBRE_RAIZ);
        if (previo != null)
        {
            if (!EditorUtility.DisplayDialog("EcoCultivo", "Ya existe \"" + NOMBRE_RAIZ + "\". ¿Reemplazarlo?", "Reemplazar", "Cancelar")) return;
            Undo.DestroyObjectImmediate(previo);
        }

        Material matTanque = MaterialDeposito();
        Material matPVC = BuscarMaterial("Mat_TuboPVC");
        Material matCople = BuscarMaterial("Mat_Valvula");
        if (matPVC == null) matPVC = matTanque;
        if (matCople == null) matCople = matTanque;

        // Centro del conjunto: los depósitos van en el extremo de cada módulo que mira hacia afuera.
        Vector3 centroTodos = Vector3.zero;
        var bounds = new List<Bounds>(modulos.Count);
        foreach (LODGroup g in modulos)
        {
            Bounds b = BoundsLOD0(g);
            bounds.Add(b);
            centroTodos += b.center;
        }
        centroTodos /= modulos.Count;

        GameObject raiz = new GameObject(NOMBRE_RAIZ);
        Undo.RegisterCreatedObjectUndo(raiz, "Crear depósitos de agua");

        for (int i = 0; i < modulos.Count; i++)
        {
            Bounds b = bounds[i];
            // Eje largo del módulo (por donde corren los canales) y eje ancho.
            bool largoEnZ = b.size.z >= b.size.x;
            Vector3 L = largoEnZ ? Vector3.forward : Vector3.right;
            Vector3 W = largoEnZ ? Vector3.right : Vector3.forward;
            float medioLargo = (largoEnZ ? b.size.z : b.size.x) * 0.5f;
            float medioAncho = (largoEnZ ? b.size.x : b.size.z) * 0.5f;

            // Hacia qué extremo va el depósito: el más alejado del centro del invernadero.
            float s = Vector3.Dot(b.center - centroTodos, L) >= 0f ? 1f : -1f;
            Vector3 dir = L * s;

            float piso = b.min.y;
            Vector3 centroBase = new Vector3(b.center.x, piso, b.center.z);
            Vector3 extremo = centroBase + dir * medioLargo;                       // pie del módulo, en el extremo
            Vector3 tanque = extremo + dir * (SEPARACION + RADIO_TANQUE);          // centro de la base del tanque

            GameObject dep = new GameObject("Deposito_" + (i + 1));
            dep.transform.SetParent(raiz.transform, false);

            // Tanque y tapa
            Cilindro(dep, "Tanque", tanque, tanque + Vector3.up * ALTO_TANQUE, RADIO_TANQUE * 2f, matTanque);
            float yTapa = piso + ALTO_TANQUE;
            Cilindro(dep, "Tapa", new Vector3(tanque.x, yTapa, tanque.z), new Vector3(tanque.x, yTapa + 0.03f, tanque.z), RADIO_TANQUE * 2f + 0.03f, matCople);

            // Subida: de la tapa hacia el módulo y luego hasta el canal superior.
            float ySubida = yTapa + 0.07f;
            float yArriba = b.max.y - 0.10f;
            Vector3 a0 = new Vector3(tanque.x, yTapa + 0.03f, tanque.z);
            Vector3 a1 = new Vector3(tanque.x, ySubida, tanque.z);
            Vector3 a2 = new Vector3(extremo.x, ySubida, extremo.z) + dir * 0.06f;
            Vector3 a3 = new Vector3(a2.x, yArriba, a2.z);
            Vector3 a4 = new Vector3(extremo.x, yArriba, extremo.z) - dir * 0.15f;  // entra al canal superior
            Cilindro(dep, "Subida_1", a0, a1, DIAM_TUBO, matPVC);
            Cilindro(dep, "Subida_2", a1, a2, DIAM_TUBO, matPVC);
            Cilindro(dep, "Subida_3", a2, a3, DIAM_TUBO, matPVC);
            Cilindro(dep, "Subida_4", a3, a4, DIAM_TUBO, matPVC);
            Cople(dep, a1, matCople);
            Cople(dep, a2, matCople);
            Cople(dep, a3, matCople);

            // Retornos: de los dos canales inferiores (a los lados) al costado del tanque.
            float yCanal = piso + 0.20f;
            float yEntrada = piso + 0.12f;
            float lado = Mathf.Max(0.1f, medioAncho - 0.15f);
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 r0 = extremo + W * (k * lado) + Vector3.up * (yCanal - piso) - dir * 0.10f; // dentro del canal
                Vector3 r1 = extremo + W * (k * lado) + Vector3.up * (yCanal - piso) + dir * 0.05f;
                Vector3 r2 = new Vector3(r1.x, yEntrada, r1.z);
                Vector3 r3 = r2 + dir * (SEPARACION - 0.05f + RADIO_TANQUE);              // a la altura del centro del tanque
                Vector3 r4 = tanque + W * (k * (RADIO_TANQUE - 0.02f)) + Vector3.up * (yEntrada - piso);
                string n = k < 0 ? "Retorno_A" : "Retorno_B";
                Cilindro(dep, n + "_1", r0, r1, DIAM_TUBO, matPVC);
                Cilindro(dep, n + "_2", r1, r2, DIAM_TUBO, matPVC);
                Cilindro(dep, n + "_3", r2, r3, DIAM_TUBO, matPVC);
                Cilindro(dep, n + "_4", r3, r4, DIAM_TUBO, matPVC);
                Cople(dep, r1, matCople);
                Cople(dep, r2, matCople);
                Cople(dep, r3, matCople);
            }
        }

        // Estáticos para el batching (no se mueven).
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

        Selection.activeGameObject = raiz;
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log("[CrearDepositos] " + modulos.Count + " depósitos creados en \"" + NOMBRE_RAIZ + "\". Guarda la escena (Ctrl+S).");
    }

    [MenuItem("EcoCultivo/Quitar depósitos de agua")]
    private static void Quitar()
    {
        GameObject go = GameObject.Find(NOMBRE_RAIZ);
        if (go == null)
        {
            EditorUtility.DisplayDialog("EcoCultivo", "No hay ningún \"" + NOMBRE_RAIZ + "\" en la escena.", "OK");
            return;
        }
        var escena = go.scene;
        Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(escena);
    }

    // ---------------------------------------------------------------------

    private static List<LODGroup> BuscarModulos(UnityEngine.SceneManagement.Scene escena)
    {
        var lista = new List<LODGroup>(4);
        foreach (GameObject r in escena.GetRootGameObjects())
            lista.AddRange(r.GetComponentsInChildren<LODGroup>(true));
        return lista;
    }

    /// <summary>Bounds del nivel más detallado del LOD (el módulo completo).</summary>
    private static Bounds BoundsLOD0(LODGroup g)
    {
        LOD[] lods = g.GetLODs();
        bool hay = false;
        Bounds b = new Bounds(g.transform.position, Vector3.zero);
        if (lods.Length > 0 && lods[0].renderers != null)
        {
            foreach (Renderer r in lods[0].renderers)
            {
                if (r == null) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
        }
        if (!hay)
        {
            Vector3 c = g.transform.TransformPoint(g.localReferencePoint);
            b = new Bounds(c, new Vector3(1.41f, 1.99f, 2.65f));
        }
        return b;
    }

    private static GameObject Cilindro(GameObject padre, string nombre, Vector3 a, Vector3 b, float diametro, Material mat)
    {
        Vector3 d = b - a;
        float largo = d.magnitude;
        if (largo < 0.001f) return null;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // solo visual
        go.transform.SetParent(padre.transform, false);
        go.transform.position = (a + b) * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d / largo);
        go.transform.localScale = new Vector3(diametro, largo * 0.5f, diametro); // cilindro de Unity: alto 2
        Preparar(go, mat);
        return go;
    }

    /// <summary>Cople en un codo: cilindro corto y un poco más grueso que el tubo (≈ 80 triángulos).</summary>
    private static void Cople(GameObject padre, Vector3 p, Material mat)
    {
        GameObject go = Cilindro(padre, "Cople", p - Vector3.up * 0.04f, p + Vector3.up * 0.04f, DIAM_COPLE, mat);
        if (go != null) go.name = "Cople";
    }

    private static void Preparar(GameObject go, Material mat)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static Material BuscarMaterial(string nombre)
    {
        foreach (string guid in AssetDatabase.FindAssets(nombre + " t:Material"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(ruta) == nombre)
                return AssetDatabase.LoadAssetAtPath<Material>(ruta);
        }
        return null;
    }

    /// <summary>Material del tanque: gris oscuro mate, copia de Mat_Macetero (misma variante URP Lit).</summary>
    private static Material MaterialDeposito()
    {
        Material existente = AssetDatabase.LoadAssetAtPath<Material>(RUTA_MAT);
        if (existente != null) return existente;

        Material baseMat = BuscarMaterial("Mat_Macetero");
        Material m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "Mat_Deposito";
        m.SetTexture("_BaseMap", null);
        m.SetTexture("_MainTex", null);
        Color gris = new Color(0.20f, 0.25f, 0.23f);
        m.SetColor("_BaseColor", gris);
        m.SetColor("_Color", gris);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        m.enableInstancing = true;
        AssetDatabase.CreateAsset(m, RUTA_MAT);
        AssetDatabase.SaveAssets();
        return m;
    }
}
