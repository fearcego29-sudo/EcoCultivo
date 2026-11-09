using TMPro;
using UnityEngine;

/// <summary>
/// Copia los textos del asset TextosApp a los TextMeshPro de Main_UI_Canvas al iniciar.
///
/// Uso: agregar este componente a Main_UI_Canvas y arrastrar el asset TextosApp al campo "Textos".
/// - Si "Textos" está vacío, no hace nada (la escena se queda como está).
/// - Localiza cada texto por su ruta dentro de Main_UI_Canvas UNA sola vez, en Awake
///   (nunca en Update). Las rutas usan los nombres actuales de la jerarquía, que no deben cambiar.
/// - Si una ruta no existe, avisa en la Consola y sigue con las demás.
/// - (Tarea 5) Las líneas que empiezan con "## " se convierten en subtítulos con el tamaño
///   y color de EstiloUI (por eso corre DESPUÉS de AplicadorEstilo).
/// </summary>
[DefaultExecutionOrder(-50)] // después de AplicadorEstilo (-100), antes de los demás scripts
public class AplicadorTextos : MonoBehaviour
{
    [Tooltip("Asset con todos los textos (Create > EcoCultivo > Textos de la App).")]
    public TextosApp textos;

    [Tooltip("Raíz donde buscar los textos. Si se deja vacío, se usa este mismo objeto (Main_UI_Canvas).")]
    public Transform raizCanvas;

    private void Awake()
    {
        if (textos != null) textos.Activar(); // para UIManager, MenuDesplegable y EstadoControlesAR
        Aplicar();
    }

    /// <summary>Vuelve a aplicar todos los textos (también desde el menú contextual del componente).</summary>
    [ContextMenu("Aplicar textos ahora")]
    public void Aplicar()
    {
        if (textos == null) return;

        Transform raiz = raizCanvas != null ? raizCanvas : transform;

        // Splash
        Poner(raiz, "Panel_Splash", "Text_TituloSplash", textos.tituloSplash);

        // Menú
        Poner(raiz, "Panel_Menu", "Text_TituloMenu", textos.tituloMenu);
        Poner(raiz, "Panel_Menu/Btn_AR", "Visualizador 3D / AR",textos.botonVisualizador);
        Poner(raiz, "Panel_Menu/Btn_Objetivo", "Objetivo del Proyecto", textos.botonObjetivo);
        Poner(raiz, "Panel_Menu/Btn_Creditos", "Créditos", textos.botonCreditos);

        // Objetivo
        Poner(raiz, "Panel_Objetivo", "Text_TituloObjetivo", textos.tituloObjetivo);
        Poner(raiz, "Panel_Objetivo/Tarjeta_Contenido", "Text_DescripcionObjetivo", Formatear(textos.descripcionObjetivo));
        Poner(raiz, "Panel_Objetivo/Btn_Volver_Objetivo", "Text (TMP)", textos.botonVolverObjetivo);

        // Créditos
        Poner(raiz, "Panel_Creditos", "Text_TituloCreditos", textos.tituloCreditos);
        Poner(raiz, "Panel_Creditos/Tarjeta_Creditos", "Text_DatosAlumno", Formatear(textos.datosAlumno));
        Poner(raiz, "Panel_Creditos/Btn_Volver_Creditos", "Text (TMP)", textos.botonVolverCreditos);

        // Vista 3D / AR
        Poner(raiz, "Panel_AR_UI/Btn_MenuAR", "Text (TMP)", textos.botonMenuAR);
        Poner(raiz, "Panel_AR_UI/Tarjeta_ControlesAR/Btn_Luces", "Text (TMP)", textos.botonDiaNoche);
        Poner(raiz, "Panel_AR_UI/Tarjeta_ControlesAR/Btn_FlujoAgua", "Text (TMP)", textos.botonFlujoAgua);
        Poner(raiz, "Panel_AR_UI/Tarjeta_ControlesAR/Btn_CamaraReset", "Text (TMP)", textos.botonResetCamara);
    }

    /// <summary>
    /// Convierte las líneas "## Subtítulo" en texto enriquecido de TMP:
    /// tamaño RELATIVO (porcentaje), para que el auto-ajuste del cuerpo también lo reduzca.
    /// Se ejecuta solo al iniciar (genera strings una vez, no por frame).
    /// </summary>
    private static string Formatear(string texto)
    {
        if (string.IsNullOrEmpty(texto) || !texto.Contains("## ")) return texto;

        EstiloUI e = EstiloUI.Activo;
        int porcentaje = e != null && e.tamanoCuerpo > 0f
            ? Mathf.RoundToInt(e.tamanoSubtitulo / e.tamanoCuerpo * 100f)
            : 118;
        string color = e != null ? ColorUtility.ToHtmlStringRGB(e.verdeHojaOscuro) : "1F3D2A";

        string[] lineas = texto.Split('\n');
        System.Text.StringBuilder sb = new System.Text.StringBuilder(texto.Length + 128);
        for (int i = 0; i < lineas.Length; i++)
        {
            string l = lineas[i];
            if (i > 0) sb.Append('\n');
            if (l.StartsWith("## "))
            {
                sb.Append("<size=").Append(porcentaje).Append("%><b><color=#").Append(color).Append('>')
                  .Append(l.Substring(3)).Append("</color></b></size>");
            }
            else
            {
                sb.Append(l);
            }
        }
        return sb.ToString();
    }

    /// <param name="rutaPadre">Ruta del padre dentro de Main_UI_Canvas (sus nombres no llevan "/").</param>
    /// <param name="nombreHijo">Nombre EXACTO del objeto de texto. Se compara por nombre y no con
    /// Find porque "Visualizador 3D / AR" contiene una barra, que Find tomaría como separador.</param>
    private static void Poner(Transform raiz, string rutaPadre, string nombreHijo, string valor)
    {
        if (valor == null) return;

        Transform padre = raiz.Find(rutaPadre); // Find encuentra también hijos inactivos
        Transform t = null;
        if (padre != null)
        {
            for (int i = 0; i < padre.childCount; i++)
            {
                Transform hijo = padre.GetChild(i);
                if (hijo.name == nombreHijo) { t = hijo; break; }
            }
        }

        if (t == null)
        {
            Debug.LogWarning("[AplicadorTextos] No se encontró: " + rutaPadre + "/" + nombreHijo, raiz);
            return;
        }

        TMP_Text tmp = t.GetComponent<TMP_Text>();
        if (tmp == null)
        {
            Debug.LogWarning("[AplicadorTextos] Sin componente TextMeshPro en: " + rutaPadre + "/" + nombreHijo, t);
            return;
        }

        if (tmp.text != valor) tmp.text = valor;
    }
}
