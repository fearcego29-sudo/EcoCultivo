using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menú del Editor: EcoCultivo > Crear entorno urbano (azotea)  /  Quitar entorno urbano.
///
/// Coloca el invernadero sobre la AZOTEA de un edificio (~6 pisos) en una ciudad low-poly:
/// - Azotea: impermeabilizante terracota, pretil, andadores desde ambas puertas, paneles solares,
///   2 tinacos, cubo de escalera, equipos de A/C, antena, jardineras de huerto urbano y una
///   mini turbina eólica (con rotor animado).
/// - Ciudad: calles, banquetas, manzanas con edificios de alturas variadas (franjas de ventanas en
///   los cercanos), un parque y un skyline lejano que cierra el horizonte.
/// - Movimiento (script AmbienteUrbano): nubes girando lento, parvada de aves, rotor de la turbina.
/// - Noche: malla de ventanas iluminadas (material Unlit), activa solo de noche. No es una luz.
///
/// RENDIMIENTO:
/// - Ciudad estática = 1 malla + 1 material (copia de Mat_Macetero: misma variante URP Lit → SRP Batcher).
/// - Nubes, aves y rotor = 1 malla cada uno (se mueven).  → Día: +4 draw calls.
/// - Ventanas nocturnas = +1 draw call y +1 SetPass (shader Unlit) SOLO de noche.
/// - ≈13k triángulos en total. Nada proyecta sombras. No agrega luces.
///
/// Archivos nuevos en Assets/EcoCultivo/EntornoUrbano. En la escena crea "Entorno_Urbano" (Ctrl+Z lo deshace).
/// Si existe "Jardin_Exterior", lo quita de la escena (sus archivos en Assets/Jardin se conservan).
/// Semilla fija: regenerar da siempre el mismo resultado.
/// </summary>
public static class CrearEntornoUrbano
{
    private const string CARPETA = "Assets/EcoCultivo/EntornoUrbano"; // ubicación tras "Organizar carpetas"
    private const string RUTA_PALETA = CARPETA + "/Urbano_Paleta.png";
    private const string RUTA_MAT = CARPETA + "/Mat_Urbano.mat";
    private const string RUTA_MAT_NOCHE = CARPETA + "/Mat_VentanasNoche.mat";
    private const string NOMBRE_MATERIAL_BASE = "Mat_Macetero"; // se busca por nombre (funciona antes y después de organizar)
    private const string NOMBRE_RAIZ = "Entorno_Urbano";
    private const string NOMBRE_JARDIN = "Jardin_Exterior";
    private const string NOMBRE_INVERNADERO = "invernadero_EcoCultivo";
    private const int SEMILLA = 7;

    private const float ALTURA_EDIFICIO = 18f; // ~6 pisos
    private const float PASO_MANZANA = 48f;     // manzana 34 m + calle 14 m
    private const float MEDIA_MANZANA = 17f;

    // ------------------------------------------------------------------ Paleta
    private enum C
    {
        Impermeabilizante, Concreto, ConcretoClaro, ConcretoOscuro, Pretil, Tinaco, PanelSolar, Metal,
        Edificio1, Edificio2, Edificio3, Edificio4, Ladrillo, Ventana, Asfalto, Banqueta,
        Pasto, Follaje, Tronco, Nube, Ave, Aspa, EdificioLejano, Puerta
    }

    private static readonly Color32[] PALETA =
    {
        new Color32(0xB8, 0x5C, 0x42, 255), // Impermeabilizante (terracota)
        new Color32(0xA9, 0xA6, 0x9F, 255), // Concreto
        new Color32(0xCF, 0xCB, 0xC2, 255), // ConcretoClaro (andadores)
        new Color32(0x7D, 0x7A, 0x75, 255), // ConcretoOscuro
        new Color32(0xE6, 0xE0, 0xD3, 255), // Pretil
        new Color32(0x2C, 0x2E, 0x30, 255), // Tinaco
        new Color32(0x24, 0x3B, 0x6B, 255), // PanelSolar
        new Color32(0x9A, 0xA0, 0xA6, 255), // Metal
        new Color32(0xE3, 0xD3, 0xB5, 255), // Edificio1 (beige)
        new Color32(0x9E, 0xB3, 0xC2, 255), // Edificio2 (gris azulado)
        new Color32(0xF0, 0xE9, 0xD8, 255), // Edificio3 (crema)
        new Color32(0xD9, 0x9A, 0x7E, 255), // Edificio4 (salmón)
        new Color32(0xA5, 0x57, 0x3E, 255), // Ladrillo
        new Color32(0x3A, 0x4E, 0x5E, 255), // Ventana (vidrio)
        new Color32(0x4A, 0x4D, 0x52, 255), // Asfalto
        new Color32(0xB5, 0xB0, 0xA6, 255), // Banqueta
        new Color32(0x6F, 0xA8, 0x4B, 255), // Pasto (parque)
        new Color32(0x3E, 0x7D, 0x3A, 255), // Follaje
        new Color32(0x6B, 0x4A, 0x2E, 255), // Tronco
        new Color32(0xF7, 0xF8, 0xFA, 255), // Nube
        new Color32(0x33, 0x35, 0x3A, 255), // Ave
        new Color32(0xF2, 0xF2, 0xF0, 255), // Aspa (turbina)
        new Color32(0xA7, 0xB4, 0xC0, 255), // EdificioLejano
        new Color32(0x4B, 0x3A, 0x2E, 255), // Puerta
    };

    // =====================================================================
    // Menús
    // =====================================================================

    [MenuItem("EcoCultivo/Crear entorno urbano (azotea)")]
    private static void Crear()
    {
        GameObject jardin = GameObject.Find(NOMBRE_JARDIN);
        GameObject previo = GameObject.Find(NOMBRE_RAIZ);
        if (jardin != null || previo != null)
        {
            string msg = (jardin != null ? "Se quitará \"" + NOMBRE_JARDIN + "\" de la escena (sus archivos se conservan).\n" : "") +
                         (previo != null ? "Se regenerará \"" + NOMBRE_RAIZ + "\".\n" : "") + "¿Continuar?";
            if (!EditorUtility.DisplayDialog("EcoCultivo", msg, "Continuar", "Cancelar")) return;
            if (jardin != null) Undo.DestroyObjectImmediate(jardin);
            if (previo != null) Undo.DestroyObjectImmediate(previo);
        }

        Bounds inv = BoundsInvernadero();
        OrganizarProyecto.CrearCarpeta(CARPETA);

        Texture2D paleta = CrearPaleta();
        Material mat = CrearMaterialLit(paleta);
        Material matNoche = CrearMaterialNoche();

        // ---------------- Marco local alineado con el invernadero
        bool largoEnZ = inv.extents.z >= inv.extents.x;
        Constructor ciudad = new Constructor();
        ciudad.L = largoEnZ ? Vector3.forward : Vector3.right;
        ciudad.T = largoEnZ ? Vector3.right : Vector3.forward;
        float hL = (largoEnZ ? inv.extents.z : inv.extents.x) + 0.3f;
        float hT = (largoEnZ ? inv.extents.x : inv.extents.z) + 0.3f;
        float y0 = Mathf.Clamp(inv.min.y, -0.5f, 0.2f) - 0.02f; // nivel de la azotea
        ciudad.C0 = new Vector3(inv.center.x, y0, inv.center.z);

        Constructor noche = ciudad.Clonar();
        Constructor nubes = ciudad.Clonar();
        Constructor aves = ciudad.Clonar();
        Constructor rotor = new Constructor(); // en coordenadas locales del rotor

        System.Random rng = new System.Random(SEMILLA);
        PrepararIcosferas();

        float RT = hT + 6f, RL = hL + 6f;           // medio ancho/largo de la azotea
        Vector3 hub, pivotNubes, pivotAves;
        Azotea(ciudad, rng, hT, hL, RT, RL, out hub);
        Ciudad(ciudad, noche, rng, RT, RL);
        Nubes(nubes, rng, out pivotNubes);
        Aves(aves, rng, out pivotAves);
        Rotor(rotor);

        // ---------------- Escena
        GameObject raiz = new GameObject(NOMBRE_RAIZ);
        Undo.RegisterCreatedObjectUndo(raiz, "Crear entorno urbano");

        Crear(raiz, "Ciudad", ciudad.Malla(Vector3.zero, Quaternion.identity, "Urbano_Ciudad"), mat, Vector3.zero, Quaternion.identity, true);
        GameObject goNoche = Crear(raiz, "Ventanas_Noche", noche.Malla(Vector3.zero, Quaternion.identity, "Urbano_VentanasNoche"), matNoche, Vector3.zero, Quaternion.identity, false);
        goNoche.SetActive(false);
        GameObject goNubes = Crear(raiz, "Nubes", nubes.Malla(pivotNubes, Quaternion.identity, "Urbano_Nubes"), mat, pivotNubes, Quaternion.identity, false);
        GameObject goAves = Crear(raiz, "Aves", aves.Malla(pivotAves, Quaternion.identity, "Urbano_Aves"), mat, pivotAves, Quaternion.identity, false);
        Quaternion rotRotor = Quaternion.LookRotation(ciudad.L, Vector3.up);
        GameObject goRotor = Crear(raiz, "Turbina_Rotor", rotor.Malla(Vector3.zero, Quaternion.identity, "Urbano_Rotor"), mat, hub, rotRotor, false);

        AmbienteUrbano amb = raiz.AddComponent<AmbienteUrbano>();
        amb.nubes = goNubes.transform;
        amb.aves = goAves.transform;
        amb.rotorTurbina = goRotor.transform;
        amb.ventanasNoche = goNoche;
        EditorUtility.SetDirty(amb);

        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeGameObject = raiz;
        Debug.Log("[EcoCultivo] Entorno urbano creado. Triángulos: ciudad " + ciudad.Triangulos + ", nubes " + nubes.Triangulos +
                  ", aves " + aves.Triangulos + ", rotor " + rotor.Triangulos + ", ventanas de noche " + noche.Triangulos +
                  ". Guarda la escena con Ctrl+S.");
    }

    [MenuItem("EcoCultivo/Quitar entorno urbano")]
    private static void Quitar()
    {
        GameObject go = GameObject.Find(NOMBRE_RAIZ);
        if (go == null)
        {
            EditorUtility.DisplayDialog("EcoCultivo", "No hay ningún \"" + NOMBRE_RAIZ + "\" en la escena.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("EcoCultivo",
                "¿Quitar \"" + NOMBRE_RAIZ + "\" de la escena?\n(Los archivos de Assets/EcoCultivo/EntornoUrbano se conservan; Ctrl+Z lo deshace.)",
                "Quitar", "Cancelar"))
            return;
        UnityEngine.SceneManagement.Scene escena = go.scene;
        Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(escena);
    }

    private static GameObject Crear(GameObject padre, string nombre, Mesh malla, Material mat, Vector3 pos, Quaternion rot, bool recibeSombras)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre.transform, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = recibeSombras;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return go;
    }

    // =====================================================================
    // Azotea
    // =====================================================================

    private static void Azotea(Constructor k, System.Random rng, float hT, float hL, float RT, float RL, out Vector3 hub)
    {
        float ys = -ALTURA_EDIFICIO;

        // Cuerpo del edificio (de la calle a la azotea) + franjas de ventanas
        Edificio(k, null, rng, 0f, 0f, RT, RL, ys, ALTURA_EDIFICIO, C.Edificio3, true, false);

        // Piso de la azotea (impermeabilizante) ligeramente encima de la tapa del edificio
        k.Quad(k.P(-RT, -RL, 0.01f), k.P(-RT, RL, 0.01f), k.P(RT, RL, 0.01f), k.P(RT, -RL, 0.01f), C.Impermeabilizante, k.P(0, 0, -1f));

        // Pretil perimetral (1 m)
        const float G = 0.25f, H = 1.0f;
        Caja(k, -RT + G * 0.5f, 0f, G * 0.5f, RL, 0f, H, C.Pretil);
        Caja(k, RT - G * 0.5f, 0f, G * 0.5f, RL, 0f, H, C.Pretil);
        Caja(k, 0f, -RL + G * 0.5f, RT, G * 0.5f, 0f, H, C.Pretil);
        Caja(k, 0f, RL - G * 0.5f, RT, G * 0.5f, 0f, H, C.Pretil);

        // Andadores desde ambas puertas
        k.Quad(k.P(-0.8f, hL, 0.02f), k.P(-0.8f, RL - G, 0.02f), k.P(0.8f, RL - G, 0.02f), k.P(0.8f, hL, 0.02f), C.ConcretoClaro, k.P(0, 0, -1f));
        k.Quad(k.P(-0.8f, -hL, 0.02f), k.P(-0.8f, -RL + G, 0.02f), k.P(0.8f, -RL + G, 0.02f), k.P(0.8f, -hL, 0.02f), C.ConcretoClaro, k.P(0, 0, -1f));

        // Paneles solares (lado +T): 2 filas inclinadas
        for (int fila = 0; fila < 2; fila++)
        {
            float t0 = hT + 1.3f + fila * 2.1f, t1 = t0 + 1.6f;
            for (float l = -hL + 0.5f; l + 2f <= hL - 0.3f; l += 2.2f)
            {
                k.Quad(k.P(t0, l, 0.4f), k.P(t0, l + 2f, 0.4f), k.P(t1, l + 2f, 1.0f), k.P(t1, l, 1.0f), C.PanelSolar, k.P((t0 + t1) * 0.5f, l + 1f, -1f));
                Caja(k, t1 - 0.05f, l + 1f, 0.04f, 0.95f, 0f, 1.0f, C.Metal); // soporte trasero
                Caja(k, t0 + 0.05f, l + 1f, 0.04f, 0.95f, 0f, 0.4f, C.Metal); // soporte delantero
            }
        }

        // Tinacos (lado −T, cerca de una puerta): base + cilindro + tapa
        for (int i = 0; i < 2; i++)
        {
            float tc = -(hT + 3f), lc = hL + 2.2f + i * 1.7f;
            Caja(k, tc, lc, 0.75f, 0.75f, 0f, 0.25f, C.ConcretoOscuro);
            Prisma(k, k.P(tc, lc, 0.25f), 0.6f, 0.6f, 1.4f, 10, C.Tinaco, false);
            Cono(k, k.P(tc, lc, 1.65f), 0.62f, 0.25f, 10, C.Tinaco);
        }

        // Cubo de escalera con puerta (lado −T, extremo −L)
        {
            float tc = -(hT + 3f), lc = -(RL - 2.2f);
            Caja(k, tc, lc, 1.5f, 1.3f, 0f, 2.6f, C.Concreto);
            float lp = lc + 1.3f + 0.02f;
            k.Quad(k.P(tc - 0.45f, lp, 0f), k.P(tc + 0.45f, lp, 0f), k.P(tc + 0.45f, lp, 2.0f), k.P(tc - 0.45f, lp, 2.0f), C.Puerta, k.P(tc, lc, 1f));
        }

        // Equipos de aire acondicionado con ventilador
        for (int i = 0; i < 3; i++)
        {
            float tc = -(hT + 3.2f), lc = -2f + i * 1.6f;
            Caja(k, tc, lc, 0.45f, 0.55f, 0f, 0.8f, C.Metal);
            Disco(k, k.P(tc, lc, 0.81f), 0.32f, 8, C.Tinaco, k.P(tc, lc, 0f));
        }

        // Jardineras de huerto urbano junto al pretil (lado −T)
        for (float l = -RL + 2.5f; l <= RL - 2.5f; l += 2.6f)
        {
            float tc = -(RT - 0.65f);
            Caja(k, tc, l, 0.3f, 0.9f, 0f, 0.45f, C.Ladrillo);
            for (int p = -1; p <= 1; p += 2)
                Icosaedro(k, rng, k.P(tc, l + p * 0.45f, 0.62f), new Vector3(0.32f, 0.28f, 0.32f), C.Follaje);
        }

        // Antena
        {
            Vector3 b = k.P(-(RT - 1.0f), RL - 1.0f, 0f);
            Prisma(k, b, 0.05f, 0.03f, 4f, 4, C.Metal, true);
            Caja(k, -(RT - 1.0f), RL - 1.0f, 0.5f, 0.03f, 3.2f, 3.26f, C.Metal);
        }

        // Mini turbina eólica: mástil + góndola (el rotor es otra malla animada)
        {
            float tc = RT - 1.2f, lc = RL - 1.2f;
            Prisma(k, k.P(tc, lc, 0f), 0.09f, 0.07f, 5f, 6, C.Metal, true);
            Caja(k, tc, lc + 0.1f, 0.15f, 0.35f, 4.9f, 5.2f, C.Aspa);
            hub = k.P(tc, lc + 0.5f, 5.05f);
        }
    }

    // =====================================================================
    // Ciudad
    // =====================================================================

    private static void Ciudad(Constructor k, Constructor noche, System.Random rng, float RT, float RL)
    {
        float ys = -ALTURA_EDIFICIO;

        // Asfalto general (700 × 700 m)
        k.Quad(k.P(-350f, -350f, ys), k.P(-350f, 350f, ys), k.P(350f, 350f, ys), k.P(350f, -350f, ys), C.Asfalto, k.P(0, 0, ys - 5f));

        C[] fachadas = { C.Edificio1, C.Edificio2, C.Edificio3, C.Edificio4, C.Ladrillo };

        for (int i = -4; i <= 4; i++)
        {
            for (int j = -4; j <= 4; j++)
            {
                int anillo = Mathf.Max(Mathf.Abs(i), Mathf.Abs(j));
                float tc = i * PASO_MANZANA, lc = j * PASO_MANZANA;

                // Banqueta de la manzana
                k.Quad(k.P(tc - MEDIA_MANZANA, lc - MEDIA_MANZANA, ys + 0.15f), k.P(tc - MEDIA_MANZANA, lc + MEDIA_MANZANA, ys + 0.15f),
                       k.P(tc + MEDIA_MANZANA, lc + MEDIA_MANZANA, ys + 0.15f), k.P(tc + MEDIA_MANZANA, lc - MEDIA_MANZANA, ys + 0.15f),
                       C.Banqueta, k.P(tc, lc, ys - 5f));

                if (i == 0 && j == 0) continue; // nuestra manzana (el edificio ya está creado)

                if (i == -1 && j == 1) { Parque(k, rng, tc, lc, ys); continue; } // un parque cerca

                bool ventanas = anillo <= 2;
                int patron = rng.Next(3);
                if (patron == 0)
                {
                    Lote(k, noche, rng, tc, lc, 14f, 14f, ys, anillo, fachadas, ventanas);
                }
                else if (patron == 1)
                {
                    Lote(k, noche, rng, tc, lc - 8f, 14f, 6.5f, ys, anillo, fachadas, ventanas);
                    Lote(k, noche, rng, tc, lc + 8f, 14f, 6.5f, ys, anillo, fachadas, ventanas);
                }
                else
                {
                    for (int a = -1; a <= 1; a += 2)
                        for (int b = -1; b <= 1; b += 2)
                            Lote(k, noche, rng, tc + a * 8f, lc + b * 8f, 6.5f, 6.5f, ys, anillo, fachadas, ventanas);
                }
            }
        }

        // Skyline lejano (cajas simples que cierran el horizonte)
        for (int i = 0; i < 36; i++)
        {
            float ang = (i + (float)rng.NextDouble() * 0.7f) / 36f * Mathf.PI * 2f;
            float rad = 250f + (float)rng.NextDouble() * 70f;
            float ht = 6f + (float)rng.NextDouble() * 7f, hl = 6f + (float)rng.NextDouble() * 7f;
            float h = 20f + (float)rng.NextDouble() * 70f;
            Caja(k, Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, ht, hl, ys, ys + h, C.EdificioLejano);
        }
    }

    private static void Lote(Constructor k, Constructor noche, System.Random rng, float tc, float lc, float ht, float hl,
                             float ys, int anillo, C[] fachadas, bool ventanas)
    {
        float r = (float)rng.NextDouble();
        float h = r < 0.12f ? 55f + (float)rng.NextDouble() * 35f    // torre ocasional
                            : (anillo <= 1 ? 9f : 8f) + (float)rng.NextDouble() * (anillo <= 1 ? 30f : 45f);
        C fachada = fachadas[rng.Next(fachadas.Length)];
        Edificio(k, noche, rng, tc, lc, ht, hl, ys, h, fachada, ventanas, true);
    }

    /// <summary>Edificio: caja + franjas de ventanas por piso (+ copia iluminada en la malla nocturna).</summary>
    private static void Edificio(Constructor k, Constructor noche, System.Random rng, float tc, float lc, float ht, float hl,
                                 float ys, float h, C fachada, bool ventanas, bool detalleTecho)
    {
        Caja(k, tc, lc, ht, hl, ys, ys + h, fachada);

        if (ventanas)
        {
            for (float y = ys + 3f; y + 1.6f < ys + h - 0.8f; y += 3.2f)
            {
                for (int cara = 0; cara < 4; cara++)
                {
                    Franja(k, tc, lc, ht, hl, y, cara, 0.03f);
                    if (noche != null && rng.NextDouble() < 0.4) Franja(noche, tc, lc, ht, hl, y, cara, 0.06f);
                }
            }
        }

        if (detalleTecho && rng.NextDouble() < 0.5)
        {
            float dt = ((float)rng.NextDouble() - 0.5f) * ht, dl = ((float)rng.NextDouble() - 0.5f) * hl;
            Caja(k, tc + dt * 0.6f, lc + dl * 0.6f, 1.4f, 1.6f, ys + h, ys + h + 2.4f, C.ConcretoOscuro);
        }
    }

    private static void Franja(Constructor k, float tc, float lc, float ht, float hl, float y, int cara, float sep)
    {
        const float MARGEN = 0.6f, ALTO = 1.4f;
        Vector3 dentro = k.P(tc, lc, y);
        switch (cara)
        {
            case 0: { float t = tc + ht + sep; k.Quad(k.P(t, lc - hl + MARGEN, y), k.P(t, lc + hl - MARGEN, y), k.P(t, lc + hl - MARGEN, y + ALTO), k.P(t, lc - hl + MARGEN, y + ALTO), C.Ventana, dentro); break; }
            case 1: { float t = tc - ht - sep; k.Quad(k.P(t, lc - hl + MARGEN, y), k.P(t, lc + hl - MARGEN, y), k.P(t, lc + hl - MARGEN, y + ALTO), k.P(t, lc - hl + MARGEN, y + ALTO), C.Ventana, dentro); break; }
            case 2: { float l = lc + hl + sep; k.Quad(k.P(tc - ht + MARGEN, l, y), k.P(tc + ht - MARGEN, l, y), k.P(tc + ht - MARGEN, l, y + ALTO), k.P(tc - ht + MARGEN, l, y + ALTO), C.Ventana, dentro); break; }
            default: { float l = lc - hl - sep; k.Quad(k.P(tc - ht + MARGEN, l, y), k.P(tc + ht - MARGEN, l, y), k.P(tc + ht - MARGEN, l, y + ALTO), k.P(tc - ht + MARGEN, l, y + ALTO), C.Ventana, dentro); break; }
        }
    }

    private static void Parque(Constructor k, System.Random rng, float tc, float lc, float ys)
    {
        float y = ys + 0.17f;
        k.Quad(k.P(tc - 15f, lc - 15f, y), k.P(tc - 15f, lc + 15f, y), k.P(tc + 15f, lc + 15f, y), k.P(tc + 15f, lc - 15f, y), C.Pasto, k.P(tc, lc, ys - 5f));
        for (int i = 0; i < 9; i++)
        {
            float t = tc + ((float)rng.NextDouble() - 0.5f) * 24f, l = lc + ((float)rng.NextDouble() - 0.5f) * 24f;
            float hTr = 2f + (float)rng.NextDouble();
            Prisma(k, k.P(t, l, y), 0.25f, 0.18f, hTr, 6, C.Tronco, true);
            float r = 2f + (float)rng.NextDouble() * 1.2f;
            Icosfera(k, rng, k.P(t, l, y + hTr + r * 0.6f), new Vector3(r, r * 0.85f, r), 0.1f, C.Follaje);
        }
    }

    // =====================================================================
    // Elementos móviles
    // =====================================================================

    private static void Nubes(Constructor k, System.Random rng, out Vector3 pivot)
    {
        pivot = k.C0;
        for (int i = 0; i < 9; i++)
        {
            float ang = (i + (float)rng.NextDouble() * 0.5f) / 9f * Mathf.PI * 2f;
            float rad = 70f + (float)rng.NextDouble() * 100f;
            Vector3 c = k.C0 + new Vector3(Mathf.Cos(ang) * rad, 80f + (float)rng.NextDouble() * 25f, Mathf.Sin(ang) * rad); // sobre las torres más altas
            int bolas = 3 + rng.Next(3);
            for (int b = 0; b < bolas; b++)
            {
                Vector3 off = new Vector3(((float)rng.NextDouble() - 0.5f) * 14f, ((float)rng.NextDouble() - 0.3f) * 2f, ((float)rng.NextDouble() - 0.5f) * 8f);
                Vector3 esc = new Vector3(5f + (float)rng.NextDouble() * 4f, 2.2f + (float)rng.NextDouble() * 1.5f, 4f + (float)rng.NextDouble() * 3f);
                Icosfera(k, rng, c + off, esc, 0.08f, C.Nube);
            }
        }
    }

    private static void Aves(Constructor k, System.Random rng, out Vector3 pivot)
    {
        pivot = k.C0 + Vector3.up * 14f;
        for (int i = 0; i < 6; i++)
        {
            Vector3 c = pivot + new Vector3(20f + ((float)rng.NextDouble() - 0.5f) * 5f,
                                            ((float)rng.NextDouble() - 0.5f) * 2f,
                                            ((float)rng.NextDouble() - 0.5f) * 5f);
            // Cuerpo (octaedro alargado hacia −Z, la dirección de vuelo) + alas en "V" de doble cara
            Vector3 frente = c + new Vector3(0f, 0f, -0.28f), atras = c + new Vector3(0f, 0f, 0.22f);
            Vector3 izq = c + new Vector3(-0.07f, 0f, 0f), der = c + new Vector3(0.07f, 0f, 0f);
            Vector3 arriba = c + new Vector3(0f, 0.06f, 0f), abajo = c + new Vector3(0f, -0.06f, 0f);
            k.Tri(frente, izq, arriba, C.Ave, c); k.Tri(frente, der, arriba, C.Ave, c); k.Tri(atras, izq, arriba, C.Ave, c); k.Tri(atras, der, arriba, C.Ave, c);
            k.Tri(frente, izq, abajo, C.Ave, c); k.Tri(frente, der, abajo, C.Ave, c); k.Tri(atras, izq, abajo, C.Ave, c); k.Tri(atras, der, abajo, C.Ave, c);

            Vector3 punta1 = c + new Vector3(-0.55f, 0.2f, 0.05f), punta2 = c + new Vector3(0.55f, 0.2f, 0.05f);
            Vector3 a1 = c + new Vector3(0f, 0.01f, -0.1f), a2 = c + new Vector3(0f, 0.01f, 0.1f);
            DobleCara(k, a1, a2, punta1, C.Ave);
            DobleCara(k, a1, a2, punta2, C.Ave);
        }
    }

    /// <summary>Rotor de 3 aspas en coordenadas locales (gira alrededor de su eje Z).</summary>
    private static void Rotor(Constructor k)
    {
        k.C0 = Vector3.zero; k.T = Vector3.right; k.L = Vector3.forward;
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 lado = new Vector3(-dir.y, dir.x, 0f) * 0.07f;
            Vector3 b0 = dir * 0.1f - lado, b1 = dir * 0.1f + lado, p = dir * 1.3f;
            DobleCara(k, b0, b1, p, C.Aspa);
        }
        // Buje
        float r = 0.1f;
        Vector3 c = Vector3.zero;
        Vector3 xp = c + Vector3.right * r, xn = c + Vector3.left * r, yp = c + Vector3.up * r, yn = c + Vector3.down * r, zf = c + Vector3.forward * 0.18f, zb = c + Vector3.back * 0.05f;
        k.Tri(xp, yp, zf, C.Aspa, c); k.Tri(yp, xn, zf, C.Aspa, c); k.Tri(xn, yn, zf, C.Aspa, c); k.Tri(yn, xp, zf, C.Aspa, c);
        k.Tri(xp, yp, zb, C.Aspa, c); k.Tri(yp, xn, zb, C.Aspa, c); k.Tri(xn, yn, zb, C.Aspa, c); k.Tri(yn, xp, zb, C.Aspa, c);
    }

    // =====================================================================
    // Recursos
    // =====================================================================

    private static Bounds BoundsInvernadero()
    {
        GameObject inv = GameObject.Find(NOMBRE_INVERNADERO);
        Renderer[] rs = inv != null ? inv.GetComponentsInChildren<Renderer>() : new Renderer[0];
        if (rs.Length == 0)
        {
            Debug.LogWarning("[EcoCultivo] No se encontró '" + NOMBRE_INVERNADERO + "'; se usan medidas aproximadas.");
            return new Bounds(new Vector3(-25.12f, 1.5f, -29.49f), new Vector3(10f, 3f, 14f));
        }
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static Texture2D CrearPaleta()
    {
        int ancho = 32; // potencia de 2; sobran espacios para colores futuros
        Color32[] px = new Color32[ancho];
        for (int i = 0; i < ancho; i++) px[i] = i < PALETA.Length ? PALETA[i] : new Color32(255, 0, 255, 255);
        Texture2D t = new Texture2D(ancho, 1, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        t.Apply();
        File.WriteAllBytes(RUTA_PALETA, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(RUTA_PALETA, ImportAssetOptions.ForceUpdate);

        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(RUTA_PALETA);
        imp.textureType = TextureImporterType.Default;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.sRGBTexture = true;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(RUTA_PALETA);
    }

    private static Material CrearMaterialLit(Texture2D paleta)
    {
        Material baseMat = null;
        foreach (string guid in AssetDatabase.FindAssets(NOMBRE_MATERIAL_BASE + " t:Material"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(ruta) == NOMBRE_MATERIAL_BASE) { baseMat = AssetDatabase.LoadAssetAtPath<Material>(ruta); break; }
        }
        Material m;
        if (baseMat != null)
        {
            m = new Material(baseMat); // misma variante de shader que Mat_Macetero
        }
        else
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetFloat("_Smoothness", 0.1f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }
        m.name = "Mat_Urbano";
        m.SetTexture("_BaseMap", paleta);
        m.SetTexture("_MainTex", paleta);
        m.SetColor("_BaseColor", Color.white);
        m.SetColor("_Color", Color.white);
        m.enableInstancing = true;
        return GuardarMaterial(m, RUTA_MAT);
    }

    private static Material CrearMaterialNoche()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material m = new Material(unlit != null ? unlit : Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "Mat_VentanasNoche";
        Color calido = new Color(1f, 0.83f, 0.45f);
        m.SetColor("_BaseColor", calido);
        m.SetColor("_Color", calido);
        return GuardarMaterial(m, RUTA_MAT_NOCHE);
    }

    private static Material GuardarMaterial(Material m, string ruta)
    {
        Material existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (existente != null)
        {
            existente.shader = m.shader;
            existente.CopyPropertiesFromMaterial(m);
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(existente);
            AssetDatabase.SaveAssets();
            return existente;
        }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // =====================================================================
    // Constructor de mallas low-poly (sombreado plano, color por paleta)
    // =====================================================================

    private class Constructor
    {
        public Vector3 C0, T = Vector3.right, L = Vector3.forward;
        private readonly List<Vector3> v = new List<Vector3>(30000);
        private readonly List<Vector3> n = new List<Vector3>(30000);
        private readonly List<Vector2> uv = new List<Vector2>(30000);
        private readonly List<int> tr = new List<int>(30000);

        public int Triangulos { get { return tr.Count / 3; } }

        public Constructor Clonar()
        {
            return new Constructor { C0 = C0, T = T, L = L };
        }

        public Vector3 P(float t, float l, float y)
        {
            return C0 + T * t + L * l + Vector3.up * y;
        }

        /// <summary>Triángulo plano; "fuera" es un punto del lado opuesto a la cara visible.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, C color, Vector3 fuera)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 centro = (a + b + c) / 3f;
            if (Vector3.Dot(normal, centro - fuera) < 0f)
            {
                Vector3 tmp = b; b = c; c = tmp;
                normal = -normal;
            }
            normal.Normalize();
            Vector2 uvc = new Vector2(((int)color + 0.5f) / 32f, 0.5f);
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(uvc); uv.Add(uvc); uv.Add(uvc);
            tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, C color, Vector3 fuera)
        {
            Tri(a, b, c, color, fuera);
            Tri(a, c, d, color, fuera);
        }

        /// <summary>Crea (o actualiza) el asset de malla, con vértices relativos al pivote.</summary>
        public Mesh Malla(Vector3 pivote, Quaternion rotacionPivote, string nombre)
        {
            Quaternion inv = Quaternion.Inverse(rotacionPivote);
            List<Vector3> vl = new List<Vector3>(v.Count);
            List<Vector3> nl = new List<Vector3>(n.Count);
            for (int i = 0; i < v.Count; i++)
            {
                vl.Add(inv * (v[i] - pivote));
                nl.Add(inv * n[i]);
            }

            Mesh m = new Mesh { name = nombre };
            if (vl.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(vl);
            m.SetNormals(nl);
            m.SetUVs(0, uv);
            m.SetTriangles(tr, 0);
            m.RecalculateBounds();

            string ruta = CARPETA + "/" + nombre + ".asset";
            Mesh existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (existente != null)
            {
                EditorUtility.CopySerialized(m, existente);
                existente.name = nombre;
                Object.DestroyImmediate(m);
                EditorUtility.SetDirty(existente);
                AssetDatabase.SaveAssets();
                return existente;
            }
            AssetDatabase.CreateAsset(m, ruta);
            AssetDatabase.SaveAssets();
            return m;
        }
    }

    // ---------------------------------------------------------------- Formas

    /// <summary>Caja sin fondo, centrada en (tc, lc) del marco local, de yb a yt.</summary>
    private static void Caja(Constructor k, float tc, float lc, float ht, float hl, float yb, float yt, C color)
    {
        Vector3 centro = k.P(tc, lc, (yb + yt) * 0.5f);
        float t0 = tc - ht, t1 = tc + ht, l0 = lc - hl, l1 = lc + hl;
        k.Quad(k.P(t0, l0, yt), k.P(t0, l1, yt), k.P(t1, l1, yt), k.P(t1, l0, yt), color, centro);
        k.Quad(k.P(t0, l0, yb), k.P(t0, l1, yb), k.P(t0, l1, yt), k.P(t0, l0, yt), color, centro);
        k.Quad(k.P(t1, l0, yb), k.P(t1, l1, yb), k.P(t1, l1, yt), k.P(t1, l0, yt), color, centro);
        k.Quad(k.P(t0, l0, yb), k.P(t1, l0, yb), k.P(t1, l0, yt), k.P(t0, l0, yt), color, centro);
        k.Quad(k.P(t0, l1, yb), k.P(t1, l1, yb), k.P(t1, l1, yt), k.P(t0, l1, yt), color, centro);
    }

    private static void Prisma(Constructor k, Vector3 baseC, float rBase, float rTop, float alto, int lados, C color, bool tapa)
    {
        Vector3 top = baseC + Vector3.up * alto;
        Vector3 eje = baseC + Vector3.up * (alto * 0.5f);
        for (int i = 0; i < lados; i++)
        {
            float a0 = i / (float)lados * Mathf.PI * 2f, a1 = (i + 1) / (float)lados * Mathf.PI * 2f;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            k.Quad(baseC + d0 * rBase, baseC + d1 * rBase, top + d1 * rTop, top + d0 * rTop, color, eje);
            if (tapa) k.Tri(top, top + d0 * rTop, top + d1 * rTop, color, top + Vector3.down);
        }
    }

    private static void Cono(Constructor k, Vector3 baseC, float radio, float alto, int lados, C color)
    {
        Vector3 punta = baseC + Vector3.up * alto;
        Vector3 eje = baseC + Vector3.up * (alto * 0.3f);
        for (int i = 0; i < lados; i++)
        {
            float a0 = i / (float)lados * Mathf.PI * 2f, a1 = (i + 1) / (float)lados * Mathf.PI * 2f;
            Vector3 p0 = baseC + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radio;
            Vector3 p1 = baseC + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radio;
            k.Tri(p0, p1, punta, color, eje);
        }
    }

    private static void Disco(Constructor k, Vector3 c, float r, int lados, C color, Vector3 fuera)
    {
        for (int i = 0; i < lados; i++)
        {
            float a0 = i / (float)lados * Mathf.PI * 2f, a1 = (i + 1) / (float)lados * Mathf.PI * 2f;
            k.Tri(c, c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r, color, fuera);
        }
    }

    /// <summary>Triángulo visible por ambos lados (alas, aspas).</summary>
    private static void DobleCara(Constructor k, Vector3 a, Vector3 b, Vector3 c, C color)
    {
        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
        Vector3 centro = (a + b + c) / 3f;
        k.Tri(a, b, c, color, centro - n);
        k.Tri(a, b, c, color, centro + n);
    }

    // Icosfera nivel 1 (80 caras) e icosaedro (20 caras), calculados una vez.
    private static Vector3[] icoV, ico0V;
    private static int[] icoT, ico0T;

    private static void Icosfera(Constructor k, System.Random rng, Vector3 c, Vector3 esc, float rugosidad, C color)
    {
        Vector3[] p = new Vector3[icoV.Length];
        for (int i = 0; i < icoV.Length; i++)
            p[i] = c + Vector3.Scale(icoV[i] * (1f + ((float)rng.NextDouble() - 0.5f) * 2f * rugosidad), esc);
        for (int i = 0; i < icoT.Length; i += 3)
            k.Tri(p[icoT[i]], p[icoT[i + 1]], p[icoT[i + 2]], color, c);
    }

    private static void Icosaedro(Constructor k, System.Random rng, Vector3 c, Vector3 esc, C color)
    {
        Vector3[] p = new Vector3[ico0V.Length];
        for (int i = 0; i < ico0V.Length; i++)
            p[i] = c + Vector3.Scale(ico0V[i] * (1f + ((float)rng.NextDouble() - 0.5f) * 0.3f), esc);
        for (int i = 0; i < ico0T.Length; i += 3)
            k.Tri(p[ico0T[i]], p[ico0T[i + 1]], p[ico0T[i + 2]], color, c);
    }

    private static void PrepararIcosferas()
    {
        if (icoV != null) return;
        float f = (1f + Mathf.Sqrt(5f)) / 2f;
        List<Vector3> v = new List<Vector3>
        {
            new Vector3(-1, f, 0), new Vector3(1, f, 0), new Vector3(-1, -f, 0), new Vector3(1, -f, 0),
            new Vector3(0, -1, f), new Vector3(0, 1, f), new Vector3(0, -1, -f), new Vector3(0, 1, -f),
            new Vector3(f, 0, -1), new Vector3(f, 0, 1), new Vector3(-f, 0, -1), new Vector3(-f, 0, 1)
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
        int[] caras =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        ico0V = v.ToArray();
        ico0T = caras;

        Dictionary<long, int> medios = new Dictionary<long, int>();
        List<int> t = new List<int>(240);
        System.Func<int, int, int> Medio = (ia, ib) =>
        {
            long clave = ia < ib ? ((long)ia << 32) | (uint)ib : ((long)ib << 32) | (uint)ia;
            int idx;
            if (medios.TryGetValue(clave, out idx)) return idx;
            v.Add(((v[ia] + v[ib]) * 0.5f).normalized);
            idx = v.Count - 1;
            medios.Add(clave, idx);
            return idx;
        };
        for (int i = 0; i < caras.Length; i += 3)
        {
            int a = caras[i], b = caras[i + 1], c = caras[i + 2];
            int ab = Medio(a, b), bc = Medio(b, c), ca = Medio(c, a);
            t.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        icoV = v.ToArray();
        icoT = t.ToArray();
    }
}
