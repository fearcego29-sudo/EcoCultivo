using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Ronda 9 — Sombras con malla simple.
///
/// Cada luz que proyecta sombra vuelve a dibujar los objetos para calcularla. Con 8 lámparas,
/// los maceteros (≈ 41 mil triángulos cada uno en LOD0) se dibujaban hasta 9 veces por frame.
///
/// Qué hace (una sola vez, al iniciar; sin costo por frame):
/// - Por cada LOD Group, crea una copia "solo sombra" (ShadowsOnly) del ÚLTIMO nivel de LOD
///   (la malla más simple, ≈ 8 mil triángulos) y la agrega a los niveles anteriores.
/// - Los renderers de LOD0/LOD1 dejan de proyectar sombra; la sombra la proyecta la copia simple.
/// - Resultado: el macetero se VE con todo su detalle, pero su sombra se calcula con ~5 veces
///   menos triángulos. La sombra se sigue viendo igual (misma silueta).
///
/// No modifica la escena guardada ni los modelos.
/// </summary>
public static class SombrasLOD
{
    public const string SUFIJO = "_SombraSimple";

    /// <summary>Aplica la sombra simple a todos los LOD Group de la escena. Devuelve cuántos cambió.</summary>
    public static int Aplicar(Scene escena)
    {
        List<LODGroup> grupos = new List<LODGroup>(8);
        GameObject[] raices = escena.GetRootGameObjects();
        for (int i = 0; i < raices.Length; i++)
            grupos.AddRange(raices[i].GetComponentsInChildren<LODGroup>(true));

        int cambiados = 0;
        for (int gi = 0; gi < grupos.Count; gi++)
        {
            LODGroup g = grupos[gi];
            LOD[] lods = g.GetLODs();
            if (lods.Length < 2) continue;

            // Malla más simple = último nivel.
            Renderer[] simples = lods[lods.Length - 1].renderers;
            if (simples == null) continue;

            List<Renderer> copias = new List<Renderer>(simples.Length);
            for (int r = 0; r < simples.Length; r++)
            {
                Renderer original = simples[r];
                if (original == null || !(original is MeshRenderer)) continue;
                if (original.shadowCastingMode == ShadowCastingMode.Off) continue;
                if (original.transform.Find(original.name + SUFIJO) != null) continue; // ya aplicado

                MeshFilter mf = original.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                GameObject go = new GameObject(original.name + SUFIJO);
                go.layer = original.gameObject.layer;
                go.transform.SetParent(original.transform, false); // misma posición, rotación y escala

                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = original.sharedMaterials;
                mr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                copias.Add(mr);
            }
            if (copias.Count == 0) continue;

            // Niveles detallados: no proyectan sombra; la copia simple lo hace por ellos.
            for (int l = 0; l < lods.Length - 1; l++)
            {
                Renderer[] actuales = lods[l].renderers ?? new Renderer[0];
                for (int r = 0; r < actuales.Length; r++)
                {
                    // Solo los del propio macetero (el agua de RiegoNFT también está en esta lista).
                    if (actuales[r] != null && actuales[r].transform.IsChildOf(g.transform))
                        actuales[r].shadowCastingMode = ShadowCastingMode.Off;
                }

                List<Renderer> nuevos = new List<Renderer>(actuales.Length + copias.Count);
                nuevos.AddRange(actuales);
                nuevos.AddRange(copias);
                lods[l].renderers = nuevos.ToArray();
            }

            g.SetLODs(lods);
            cambiados++;
        }
        return cambiados;
    }
}
