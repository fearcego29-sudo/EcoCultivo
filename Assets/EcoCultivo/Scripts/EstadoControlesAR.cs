using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Resalta los botones de Tarjeta_ControlesAR según el estado de la simulación:
/// - Btn_FlujoAgua: azul agua + "Agua: Encendida" cuando el agua está activa.
/// - Btn_Luces: azul noche + "Modo: Noche" cuando es de noche.
/// - Btn_CamaraReset es una acción momentánea: solo tiene el estado "presionado".
/// - Durante el riego por etapas (RiegoNFT), Btn_FlujoAgua muestra "Llenando 45 %" /
///   "Vaciando 60 %" (TMP SetText con número: sin basura de memoria por frame).
///
/// Uso: agregar a UIManager_Object (junto a UIManager e InteractiveController).
/// Las referencias se encuentran solas en Awake; no hay que arrastrar nada.
///
/// Rendimiento: solo trabaja cuando cambia el estado (evento), nunca en Update.
/// </summary>
[DefaultExecutionOrder(10)] // después de AplicadorEstilo/AplicadorTextos: su color y texto mandan
public class EstadoControlesAR : MonoBehaviour
{
    public InteractiveController controles;
    public UIManager ui;

    private Image imgLuces, imgAgua;
    private TMP_Text txtLuces, txtAgua;
    private RiegoNFT riego;

    private void Awake()
    {
        if (controles == null) controles = GetComponent<InteractiveController>();
        if (ui == null) ui = GetComponent<UIManager>();

        if (ui != null && ui.panelAR != null)
        {
            Transform tarjeta = ui.panelAR.transform.Find("Tarjeta_ControlesAR");
            if (tarjeta != null)
            {
                Buscar(tarjeta, "Btn_Luces", out imgLuces, out txtLuces);
                Buscar(tarjeta, "Btn_FlujoAgua", out imgAgua, out txtAgua);
            }
        }

        if (imgLuces == null || imgAgua == null)
            Debug.LogWarning("[EstadoControlesAR] No se encontraron Btn_Luces / Btn_FlujoAgua en Panel_AR_UI/Tarjeta_ControlesAR.", this);
    }

    private void OnEnable()
    {
        if (controles != null) controles.EstadoCambiado += Actualizar;
    }

    private void OnDisable()
    {
        if (controles != null) controles.EstadoCambiado -= Actualizar;
    }

    private void Start()
    {
        riego = controles != null ? controles.Riego : null;
        if (riego != null) riego.ProgresoCambiado += AlProgresarRiego;
        Actualizar(); // estado inicial (incluye el modo Día/Noche recuperado de PlayerPrefs)
    }

    private void OnDestroy()
    {
        if (riego != null) riego.ProgresoCambiado -= AlProgresarRiego;
    }

    private void AlProgresarRiego(float progreso, bool bombeando)
    {
        if (txtAgua == null) return;
        if (riego.Animando)
        {
            TextosApp t = TextosApp.Activo;
            string formato = bombeando
                ? (t != null ? t.estadoAguaLlenando : "Llenando {0:0} %")
                : (t != null ? t.estadoAguaVaciando : "Vaciando {0:0} %");
            txtAgua.SetText(formato, progreso * 100f);
        }
        else
        {
            Actualizar(); // terminó: etiqueta final "Agua: Encendida" / "Agua: Apagada"
        }
    }

    public void Actualizar()
    {
        if (controles == null) return;

        EstiloUI e = EstiloUI.Activo;
        TextosApp t = TextosApp.Activo;

        Color blanco = e != null ? e.blancoCalido : new Color(0.98f, 0.965f, 0.933f);
        Color carbon = e != null ? e.grisCarbon : new Color(0.18f, 0.196f, 0.212f);
        Color agua = e != null ? e.azulAgua : new Color(0.165f, 0.624f, 0.839f);
        Color noche = e != null ? e.azulNoche : new Color(0.231f, 0.298f, 0.549f);

        bool esNoche = controles.EsNoche;
        bool aguaOn = controles.AguaActiva;

        Pintar(imgLuces, txtLuces, esNoche, noche, blanco, carbon,
               esNoche ? (t != null ? t.estadoNoche : "Modo: Noche") : (t != null ? t.estadoDia : "Modo: Día"));

        Pintar(imgAgua, txtAgua, aguaOn, agua, blanco, carbon,
               aguaOn ? (t != null ? t.estadoAguaEncendida : "Agua: Encendida") : (t != null ? t.estadoAguaApagada : "Agua: Apagada"));

        // Si el riego se está animando, el porcentaje (AlProgresarRiego) manda sobre la etiqueta.
        if (riego != null && riego.Animando) AlProgresarRiego(riego.Progreso, riego.Bombeando);
    }

    private static void Pintar(Image img, TMP_Text txt, bool activo, Color colorActivo, Color blanco, Color carbon, string etiqueta)
    {
        if (img != null) img.color = activo ? colorActivo : blanco;
        if (txt != null)
        {
            txt.color = activo ? blanco : carbon;
            if (txt.text != etiqueta) txt.text = etiqueta;
        }
    }

    private static void Buscar(Transform padre, string nombre, out Image img, out TMP_Text txt)
    {
        img = null;
        txt = null;
        Transform b = padre.Find(nombre);
        if (b == null) return;
        img = b.GetComponent<Image>();
        txt = b.GetComponentInChildren<TMP_Text>(true);
    }
}
