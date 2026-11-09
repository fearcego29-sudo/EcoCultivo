using UnityEngine;

/// <summary>
/// Todos los textos visibles de la app en un solo lugar.
/// Para cambiar un texto: seleccionar el asset "TextosApp" en Assets/Settings y editarlo
/// en el Inspector. No hace falta buscar el objeto en la escena.
///
/// Formato en textos largos (Objetivo, Créditos):
/// - Una línea que empieza con "## " es un SUBTÍTULO (tamaño y color del EstiloUI).
/// - Se puede usar texto enriquecido de TextMeshPro, por ejemplo &lt;b&gt;negrita&lt;/b&gt;.
/// - Párrafos cortos, uno por línea.
///
/// Crear el asset: Project > clic derecho en Assets/Settings > Create > EcoCultivo > Textos de la App.
/// </summary>
[CreateAssetMenu(fileName = "TextosApp", menuName = "EcoCultivo/Textos de la App")]
public class TextosApp : ScriptableObject
{
    /// <summary>Textos en uso (lo fija AplicadorTextos al iniciar). Puede ser null.</summary>
    public static TextosApp Activo { get; private set; }

    public void Activar()
    {
        Activo = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstatico()
    {
        Activo = null;
    }

    [Header("Splash")]
    public string tituloSplash = "EcoCultivo AR";

    [Header("Menú")]
    public string tituloMenu = "PROYECTO ECOCULTIVO";
    public string botonVisualizador = "Visualizador 3D / AR";
    public string botonObjetivo = "Objetivo del Proyecto";
    public string botonCreditos = "Créditos";

    [Header("Objetivo")]
    public string tituloObjetivo = "OBJETIVO DEL PROYECTO";
    [TextArea(4, 10)]
    public string descripcionObjetivo =
        "## ¿Qué es?\n" +
        "Un prototipo interactivo 3D y de Realidad Aumentada para visualizar y analizar el sistema de cultivo hidropónico vertical EcoCultivo.\n" +
        "## ¿Qué busca?\n" +
        "Optimizar el uso del espacio y de los recursos hídricos en invernaderos.\n" +
        "## ¿Qué puedes explorar?\n" +
        "La supervisión modular del flujo de agua y de la iluminación, pensada para una producción sustentable en la región.";
    public string botonVolverObjetivo = "Volver al Menú";

    [Header("Créditos")]
    public string tituloCreditos = "CRÉDITOS Y DATOS DEL PROYECTO";
    [TextArea(4, 10)]
    public string datosAlumno =
        "<b>Alumno:</b> Felipe Cervera\n" +
        "<b>Universidad:</b> Universidad Marista de Mérida\n" +
        "<b>Asignatura:</b> Gráficos por Computadora\n" +
        "<b>Profesor:</b> Luis de Jesús López Hernández\n" +
        "<b>Proyecto:</b> EcoCultivo AR (Sistema Hidropónico)";
    public string botonVolverCreditos = "← Volver al Menú";

    [Header("Vista 3D / AR")]
    public string botonMenuAR = "≡";
    public string botonDiaNoche = "Día / Noche";
    public string botonFlujoAgua = "Flujo de Agua";
    public string botonResetCamara = "Reset Cámara";

    [Header("Vista 3D / AR — etiquetas de estado")]
    public string estadoDia = "Modo: Día";
    public string estadoNoche = "Modo: Noche";
    public string estadoAguaEncendida = "Agua: Encendida";
    public string estadoAguaApagada = "Agua: Apagada";
    [Tooltip("Mientras se llena. {0:0} = porcentaje (lo pone el código).")]
    public string estadoAguaLlenando = "Llenando {0:0} %";
    [Tooltip("Mientras se vacía. {0:0} = porcentaje.")]
    public string estadoAguaVaciando = "Vaciando {0:0} %";

    [Header("Menú desplegable (botón ≡)")]
    public string menuPrincipal = "Menú principal";
    public string menuObjetivo = "Objetivo del proyecto";
    public string menuCreditos = "Créditos";
    public string calidadAlta = "Calidad gráfica: Alta";
    public string calidadBaja = "Calidad gráfica: Baja";

    [Header("Mensajes del sistema (Android)")]
    [Tooltip("Aviso al pulsar Atrás en el menú principal.")]
    public string mensajeSalir = "Presiona Atrás de nuevo para salir";
}
