using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Riego NFT (Nutrient Film Technique) por etapas, con fines educativos:
/// al encender la bomba, el agua entra por el canal SUPERIOR de cada macetero, recorre el
/// canal de un extremo al otro y baja por gravedad al nivel siguiente (en zigzag), hasta
/// llegar al canal inferior. Mientras avanza, el nivel del agua en cada canal sube poco a poco.
/// Al apagar la bomba, los canales se vacían en orden inverso.
///
/// Se agrega solo: InteractiveController lo crea en Grupo_Agua_NFT_ALL si no existe
/// (no hay que tocar la escena). Se puede agregar a mano para ajustar los tiempos.
///
/// Trabaja sobre los 36 objetos "Agua_Tuberia" existentes: solo cambia su escala y posición
/// local. No crea objetos, no cambia materiales ni nombres.
///
/// Rendimiento: solo hace trabajo durante la animación (~4 s): 36 transformaciones por frame
/// (≈0.02 ms), sin basura (GC). Fuera de la animación, Update no hace nada.
/// </summary>
public class RiegoNFT : MonoBehaviour
{
    [Tooltip("Segundos para llenar todos los canales (y para vaciarlos).")]
    public float duracion = 4f;

    [Tooltip("Fracción del tiempo total que tarda UN canal en llenarse. Más alto = más superposición entre niveles.")]
    [Range(0.2f, 0.8f)] public float ventanaPorCanal = 0.36f;

    [Tooltip("Nivel del agua (alto de la lámina) cuando el canal apenas empieza a llenarse.")]
    [Range(0.05f, 1f)] public float nivelInicial = 0.2f;

    /// <summary>0 = todo vacío, 1 = todo lleno.</summary>
    public float Progreso { get; private set; }

    /// <summary>true si la bomba está encendida (objetivo = lleno).</summary>
    public bool Bombeando { get; private set; }

    /// <summary>Si es true, la animación se pausa (modo ahorro de batería).</summary>
    public bool Congelado { get; set; }

    public bool Animando { get { return animando; } }

    /// <summary>Se dispara en cada frame de la animación y al terminar: (progreso 0..1, bombeando).</summary>
    public event Action<float, bool> ProgresoCambiado;

    private struct Canal
    {
        public Transform tr;
        public Renderer rend;
        public Vector3 pos;      // posición local original (lleno)
        public Vector3 escala;   // escala local original (lleno)
        public float inicio;     // momento (0..1) en que empieza a llenarse
        public float sentido;    // -1: entra por el extremo −Z; +1: entra por +Z (zigzag)
    }

    private Canal[] canales = new Canal[0];
    private bool animando;
    private bool preparado;

    private void Awake()
    {
        Preparar();
    }

    /// <summary>Encuentra los canales y calcula el orden de llenado (una sola vez).</summary>
    private void Preparar()
    {
        if (preparado) return;
        preparado = true;

        List<Canal> lista = new List<Canal>(40);

        // Un grupo por macetero (Grupo_Agua_NFT, (1), (2), (3)); dentro, los Agua_Tuberia.
        for (int g = 0; g < transform.childCount; g++)
        {
            Transform grupo = transform.GetChild(g);

            // Niveles distintos de este macetero (de arriba hacia abajo).
            List<float> niveles = new List<float>(6);
            for (int i = 0; i < grupo.childCount; i++)
            {
                Transform t = grupo.GetChild(i);
                if (!t.name.StartsWith("Agua_Tuberia")) continue;
                float y = Mathf.Round(t.localPosition.y * 20f) / 20f;
                if (!niveles.Contains(y)) niveles.Add(y);
            }
            niveles.Sort((a, b) => b.CompareTo(a)); // descendente

            int nNiveles = Mathf.Max(1, niveles.Count);
            float paso = nNiveles > 1 ? (1f - ventanaPorCanal) / (nNiveles - 1) : 0f;

            for (int i = 0; i < grupo.childCount; i++)
            {
                Transform t = grupo.GetChild(i);
                if (!t.name.StartsWith("Agua_Tuberia")) continue;

                int nivel = niveles.IndexOf(Mathf.Round(t.localPosition.y * 20f) / 20f);
                Canal c = new Canal
                {
                    tr = t,
                    rend = t.GetComponent<Renderer>(),
                    pos = t.localPosition,
                    escala = t.localScale,
                    inicio = nivel * paso,
                    sentido = nivel % 2 == 0 ? -1f : 1f
                };
                lista.Add(c);
            }
        }

        canales = lista.ToArray();
        if (canales.Length == 0)
            Debug.LogWarning("[RiegoNFT] No se encontraron objetos 'Agua_Tuberia' bajo " + name + ".", this);
    }

    /// <summary>Enciende (true) o apaga (false) la bomba con animación.</summary>
    public void Regar(bool encender)
    {
        Preparar();
        Bombeando = encender;
        animando = true;
    }

    /// <summary>Fija el estado sin animación (al iniciar la app).</summary>
    public void FijarInmediato(bool lleno)
    {
        Preparar();
        Bombeando = lleno;
        Progreso = lleno ? 1f : 0f;
        animando = false;
        Aplicar();
        Notificar();
    }

    private void Update()
    {
        if (!animando || Congelado) return;

        float objetivo = Bombeando ? 1f : 0f;
        float vel = 1f / Mathf.Max(0.1f, duracion);
        Progreso = Mathf.MoveTowards(Progreso, objetivo, vel * Time.deltaTime);
        Aplicar();

        if (Mathf.Approximately(Progreso, objetivo)) animando = false;
        Notificar();
    }

    private void Aplicar()
    {
        float ventana = Mathf.Max(0.01f, ventanaPorCanal);
        for (int i = 0; i < canales.Length; i++)
        {
            Canal c = canales[i];
            float q = Mathf.Clamp01((Progreso - c.inicio) / ventana);

            // 1) El frente de agua avanza a lo largo del canal.
            float largo = Suave(Mathf.Clamp01(q / 0.65f));
            // 2) Después, la lámina de agua sube de nivel.
            float nivel = Mathf.Lerp(nivelInicial, 1f, Suave(Mathf.Clamp01((q - 0.35f) / 0.65f)));

            bool visible = largo > 0.001f;
            if (c.rend != null && c.rend.enabled != visible) c.rend.enabled = visible;
            if (!visible) continue;

            Vector3 esc = c.escala;
            esc.y *= nivel;
            esc.z *= largo;
            c.tr.localScale = esc;

            // El extremo de entrada queda fijo; el fondo de la lámina también (sube desde abajo).
            // Malla de cilindro de Unity: alto = 2 × escala.y; largo en Z = 1 × escala.z.
            Vector3 p = c.pos;
            p.z = c.pos.z + c.sentido * (1f - largo) * c.escala.z * 0.5f;
            p.y = c.pos.y - (1f - nivel) * c.escala.y;
            c.tr.localPosition = p;
        }
    }

    private void Notificar()
    {
        if (ProgresoCambiado != null) ProgresoCambiado(Progreso, Bombeando);
    }

    private static float Suave(float x)
    {
        return x * x * (3f - 2f * x); // smoothstep
    }
}
