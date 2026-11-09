using UnityEngine;

/// <summary>
/// Hoja de estilo central de EcoCultivo: paleta, radios de borde, sombras, duraciones
/// de animación y tamaños de fuente. TODOS los paneles se pintan con estos valores
/// (a través de AplicadorEstilo), así que un cambio aquí cambia toda la app.
///
/// Crear el asset: Project > clic derecho en Assets/Settings > Create > EcoCultivo > Estilo de UI.
///
/// Nota técnica: los colores se declaran con Color32 (no con ColorUtility) porque los
/// inicializadores de un ScriptableObject no pueden llamar a la API de Unity.
/// </summary>
[CreateAssetMenu(fileName = "EstiloUI", menuName = "EcoCultivo/Estilo de UI")]
public class EstiloUI : ScriptableObject
{
    /// <summary>Estilo en uso. Lo fija AplicadorEstilo al iniciar; otros scripts lo leen
    /// (UIManager, BotonFeedback, MenuDesplegable...). Puede ser null si no hay AplicadorEstilo.</summary>
    public static EstiloUI Activo { get; private set; }

    public void Activar()
    {
        Activo = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstatico()
    {
        Activo = null;
    }

    // ------------------------------------------------------------------ Paleta
    [Header("Paleta")]
    [Tooltip("Verde hoja: acción principal, acentos.")]
    public Color verdeHoja = new Color32(0x3F, 0x8F, 0x4A, 0xFF);
    [Tooltip("Verde hoja oscuro: fondos de panel y subtítulos.")]
    public Color verdeHojaOscuro = new Color32(0x1F, 0x3D, 0x2A, 0xFF);
    [Tooltip("Azul agua: estado activo del flujo de agua.")]
    public Color azulAgua = new Color32(0x2A, 0x9F, 0xD6, 0xFF);
    [Tooltip("Blanco cálido: tarjetas y botones secundarios.")]
    public Color blancoCalido = new Color32(0xFA, 0xF6, 0xEE, 0xFF);
    [Tooltip("Gris carbón: texto sobre fondos claros.")]
    public Color grisCarbon = new Color32(0x2E, 0x32, 0x36, 0xFF);
    [Tooltip("Azul noche: estado activo del modo Noche.")]
    public Color azulNoche = new Color32(0x3B, 0x4C, 0x8C, 0xFF);
    [Tooltip("Fondo translúcido de la barra de controles 3D.")]
    public Color fondoControles = new Color32(0x2E, 0x32, 0x36, 0xC8);
    [Tooltip("Color de las sombras suaves.")]
    public Color sombra = new Color(0f, 0f, 0f, 0.28f);

    // ------------------------------------------------------------------ Formas
    [Header("Radios de borde (unidades del Canvas)")]
    public float radioTarjeta = 28f;
    [Tooltip("Botones de 70-80 de alto: 35 = forma de píldora.")]
    public float radioBoton = 35f;
    [Tooltip("Botón ≡ (90×90): 45 = círculo.")]
    public float radioBotonCircular = 45f;

    [Header("Sombras")]
    public Vector2 desplazamientoSombra = new Vector2(0f, -6f);

    // ------------------------------------------------------------------ Animación
    [Header("Duraciones (segundos)")]
    public float duracionTransicion = 0.25f;
    public float duracionEntradaSplash = 0.5f;
    public float duracionSalidaSplash = 0.45f;
    public float duracionPresion = 0.1f;
    public float duracionMenuDesplegable = 0.18f;
    public float duracionColorBoton = 0.08f;

    // ------------------------------------------------------------------ Tipografía
    [Header("Tipografía (tamaños en puntos del Canvas)")]
    public float tamanoPortada = 80f;
    public float tamanoTitulo = 60f;
    public float tamanoSubtitulo = 40f;
    public float tamanoCuerpo = 34f;
    [Tooltip("Tamaño mínimo al que se reduce el cuerpo para no cortarse en pantallas pequeñas.")]
    public float tamanoCuerpoMinimo = 24f;
    public float tamanoBoton = 32f;
    public float espaciadoParrafo = 12f;

    // ------------------------------------------------------------------ Botones
    [Header("Estados de botón")]
    [Range(0.8f, 1f)] public float escalaPresionado = 0.95f;
    [Tooltip("Multiplicador de color al presionar (1 = sin cambio).")]
    [Range(0.5f, 1f)] public float factorColorPresionado = 0.82f;
    [Tooltip("Opacidad de un botón deshabilitado.")]
    [Range(0.1f, 1f)] public float alfaDeshabilitado = 0.45f;
}
