using UnityEngine;

/// <summary>
/// Movimiento ambiental del entorno urbano (sin funcionalidad, solo "vida"):
/// - Nubes: el grupo gira MUY lento alrededor de la ciudad.
/// - Aves: una parvada vuela en círculo sobre la azotea, aleteando (de noche se ocultan).
/// - Turbina eólica de la azotea: el rotor gira.
/// - Ventanas iluminadas: se activan solo de noche (escuchando a InteractiveController).
///
/// Lo crea y conecta automáticamente el menú EcoCultivo > Crear entorno urbano (azotea).
///
/// Rendimiento: 3 rotaciones/escalas de Transform por frame (≈0.01 ms), cero GC.
/// Las ventanas nocturnas están desactivadas de día (no se dibujan).
/// </summary>
public class AmbienteUrbano : MonoBehaviour
{
    [Header("Nubes")]
    public Transform nubes;
    [Tooltip("Grados por segundo alrededor de la ciudad.")]
    public float velocidadNubes = 0.8f;

    [Header("Aves")]
    public Transform aves;
    public float velocidadAves = 14f;
    public float frecuenciaAleteo = 7f;

    [Header("Turbina eólica")]
    public Transform rotorTurbina;
    public float velocidadRotor = 120f;

    [Header("Noche")]
    public GameObject ventanasNoche;
    [Tooltip("Vacío = se busca solo.")]
    public InteractiveController controles;

    private Vector3 escalaAves = Vector3.one;
    private float alturaAves;

    private void Awake()
    {
        if (controles == null) controles = FindFirstObjectByType<InteractiveController>();
        if (aves != null)
        {
            escalaAves = aves.localScale;
            alturaAves = aves.localPosition.y;
        }
    }

    private void OnEnable()
    {
        if (controles != null) controles.EstadoCambiado += ActualizarNoche;
    }

    private void OnDisable()
    {
        if (controles != null) controles.EstadoCambiado -= ActualizarNoche;
    }

    private void Start()
    {
        ActualizarNoche();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time;

        if (nubes != null)
            nubes.Rotate(0f, velocidadNubes * dt, 0f, Space.World);

        if (aves != null && aves.gameObject.activeSelf)
        {
            aves.Rotate(0f, velocidadAves * dt, 0f, Space.World);
            // Aleteo: escalar Y sube y baja las puntas de las alas (forma de "V").
            float aleteo = 1f + 0.6f * Mathf.Sin(t * frecuenciaAleteo);
            aves.localScale = new Vector3(escalaAves.x, escalaAves.y * aleteo, escalaAves.z);
            Vector3 p = aves.localPosition;
            p.y = alturaAves + Mathf.Sin(t * 0.5f) * 1.5f;
            aves.localPosition = p;
        }

        if (rotorTurbina != null)
            rotorTurbina.Rotate(0f, 0f, velocidadRotor * dt, Space.Self);
    }

    private void ActualizarNoche()
    {
        bool noche = controles != null && controles.EsNoche;
        if (ventanasNoche != null && ventanasNoche.activeSelf != noche) ventanasNoche.SetActive(noche);
        if (aves != null && aves.gameObject.activeSelf == noche) aves.gameObject.SetActive(!noche);
    }
}
