using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Control de cámara único de la app (teclado/mouse en PC y táctil en Android).
/// - Mitad izquierda: el dedo traslada la cámara.
/// - Mitad derecha: el dedo rota la cámara (pitch limitado a ±80°).
///
/// CAMBIOS (Tarea 1):
/// - Cada dedo conserva el rol de la mitad donde EMPEZÓ. Antes, un dedo que empezaba
///   a la izquierda y cruzaba a la derecha dejaba de mover la cámara.
/// - Los toques que empiezan sobre un botón/panel de UI se ignoran (ya no mueven la cámara
///   al pulsar botones).
/// - Solo actúa en la vista 3D/AR (consulta UIManager.PermitirControlCamara).
/// - SincronizarRotacion(): lo llama InteractiveController al resetear la cámara para que
///   el siguiente giro no "salte" a la orientación anterior.
/// - El pitch inicial se normaliza a -180..180 (antes, una cámara inclinada hacia arriba
///   leía 350° y el primer giro la forzaba de golpe a +80°).
///
/// CAMBIOS (joystick virtual):
/// - Mitad izquierda = JOYSTICK FLOTANTE: donde se apoya el dedo nace el centro; desplazar el
///   dedo mueve la cámara de forma continua (aunque el dedo quede quieto), con velocidad
///   proporcional a la distancia. Antes solo se movía mientras el dedo se arrastraba.
/// - Mitad derecha = arrastrar para girar la vista (sin cambios).
/// - Expone el estado (JoystickActivo, JoystickOrigen, JoystickVector, VistaActiva, VistaPosicion)
///   para que JoystickVisual lo dibuje en pantalla.
///
/// Rendimiento: cero allocations por frame. El raycast contra la UI solo se hace en
/// TouchPhase.Began, reutilizando la misma lista y el mismo PointerEventData.
/// </summary>
public class SimpleTouchMove : MonoBehaviour
{
    [Header("Velocidades")]
    public float moveSpeed = 4.0f;
    public float rotateSpeed = 0.15f;

    [Header("Comportamiento")]
    [Tooltip("Si está activo, la cámara solo se mueve cuando la vista 3D/AR está abierta (no en menú, créditos, etc.).")]
    public bool soloEnVistaAR = true;

    [Tooltip("Ignorar los toques que empiezan sobre un elemento de UI (botones, tarjetas).")]
    public bool ignorarToquesSobreUI = true;

    [Header("Joystick virtual (mitad izquierda)")]
    [Tooltip("Radio del joystick como fracción del alto de la pantalla (0.12 ≈ 130 px en 1080p).")]
    [Range(0.05f, 0.25f)] public float radioJoystick = 0.12f;
    [Tooltip("Zona muerta: desplazamientos menores no mueven la cámara.")]
    [Range(0f, 0.5f)] public float zonaMuerta = 0.12f;

    // ---------- Estado público (lectura) para JoystickVisual ----------
    public bool JoystickActivo { get { return leftTouchId != -1; } }
    /// <summary>Centro del joystick en píxeles de pantalla.</summary>
    public Vector2 JoystickOrigen { get; private set; }
    /// <summary>Dirección del joystick, magnitud 0..1.</summary>
    public Vector2 JoystickVector { get; private set; }
    public bool VistaActiva { get { return rightTouchId != -1; } }
    /// <summary>Posición actual del dedo que gira la vista (píxeles de pantalla).</summary>
    public Vector2 VistaPosicion { get; private set; }
    /// <summary>Radio del joystick en píxeles de pantalla.</summary>
    public float RadioJoystickPx { get { return Screen.height * radioJoystick; } }

    private float pitch = 0f;
    private float yaw = 0f;

    // Dedo asignado a cada mitad (-1 = libre). El rol se fija en TouchPhase.Began.
    private int leftTouchId = -1;
    private int rightTouchId = -1;

    // Caché para no generar basura (GC) en runtime.
    private Transform tr;
    private PointerEventData pointerData;
    private readonly List<RaycastResult> resultadosUI = new List<RaycastResult>(8);

    void Awake()
    {
        tr = transform;
    }

    void Start()
    {
        SincronizarRotacion();
    }

    void OnDisable()
    {
        LiberarDedos();
    }

    void Update()
    {
        // Fuera de la vista 3D/AR no se mueve la cámara (y se sueltan los dedos asignados).
        if (soloEnVistaAR && !UIManager.PermitirControlCamara)
        {
            LiberarDedos();
            return;
        }

        // 1. MOVIMIENTO TECLADO (WASD / Flechas para PC)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (h != 0f || v != 0f)
        {
            Vector3 moveDir = tr.right * h + tr.forward * v;
            tr.position += moveDir * moveSpeed * Time.deltaTime;
        }

        // 2. MOVIMIENTO TÁCTIL Y ROTACIÓN (Móvil / Tablet)
        int touchCount = Input.touchCount;

        // Seguridad: si no hay dedos en pantalla, ningún rol puede quedar "atascado"
        // (por ejemplo, tras volver de segundo plano sin recibir TouchPhase.Ended).
        if (touchCount == 0 && (leftTouchId != -1 || rightTouchId != -1))
        {
            LiberarDedos();
        }

        for (int i = 0; i < touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    AsignarRol(touch);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (touch.fingerId == leftTouchId)
                    {
                        // Joystick: vector desde el centro, limitado al radio (funciona aunque el dedo esté quieto).
                        float radio = Mathf.Max(1f, RadioJoystickPx);
                        JoystickVector = Vector2.ClampMagnitude((touch.position - JoystickOrigen) / radio, 1f);
                    }
                    else if (touch.fingerId == rightTouchId && touch.phase == TouchPhase.Moved)
                    {
                        // Dedo que empezó a la derecha: rota, aunque ya esté a la izquierda.
                        Rotar(touch.deltaPosition.x * rotateSpeed, touch.deltaPosition.y * rotateSpeed);
                        VistaPosicion = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.fingerId == leftTouchId) { leftTouchId = -1; JoystickVector = Vector2.zero; }
                    if (touch.fingerId == rightTouchId) rightTouchId = -1;
                    break;
            }
        }

        // Movimiento continuo según el joystick (velocidad proporcional a la inclinación).
        if (leftTouchId != -1)
        {
            Vector2 j = JoystickVector;
            float m = j.magnitude;
            if (m > zonaMuerta)
            {
                float fuerza = (m - zonaMuerta) / (1f - zonaMuerta); // 0..1 después de la zona muerta
                Vector2 dir = j / m * fuerza;
                tr.position += (tr.right * dir.x + tr.forward * dir.y) * moveSpeed * Time.deltaTime;
            }
        }

        // 3. ROTACIÓN MOUSE (Para probar giros en PC haciendo clic derecho y arrastrando)
        if (Input.GetMouseButton(1))
        {
            Rotar(Input.GetAxis("Mouse X") * 2f, Input.GetAxis("Mouse Y") * 2f);
        }
    }

    /// <summary>
    /// Vuelve a leer la rotación actual del Transform. Llamar después de mover/rotar la
    /// cámara desde otro script (por ejemplo, el botón "Reset Cámara").
    /// </summary>
    public void SincronizarRotacion()
    {
        if (tr == null) tr = transform;

        Vector3 angles = tr.eulerAngles;
        pitch = NormalizarAngulo(angles.x);
        yaw = angles.y;
        LiberarDedos();
    }

    private void AsignarRol(Touch touch)
    {
        if (ignorarToquesSobreUI && EstaSobreUI(touch.position))
            return;

        if (touch.position.x < Screen.width * 0.5f)
        {
            if (leftTouchId == -1)
            {
                leftTouchId = touch.fingerId;
                JoystickOrigen = touch.position; // joystick flotante: nace donde se apoya el dedo
                JoystickVector = Vector2.zero;
            }
        }
        else
        {
            if (rightTouchId == -1)
            {
                rightTouchId = touch.fingerId;
                VistaPosicion = touch.position;
            }
        }
    }

    private void Rotar(float deltaYaw, float deltaPitch)
    {
        yaw += deltaYaw;
        pitch -= deltaPitch;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        tr.eulerAngles = new Vector3(pitch, yaw, 0f);
    }

    private void LiberarDedos()
    {
        leftTouchId = -1;
        rightTouchId = -1;
        JoystickVector = Vector2.zero;
    }

    /// <summary>
    /// Raycast manual contra la UI. Funciona igual con StandaloneInputModule o
    /// InputSystemUIInputModule (no depende de que coincidan los IDs de dedo).
    /// Solo se llama al iniciar un toque, no en cada frame.
    /// </summary>
    private bool EstaSobreUI(Vector2 posicion)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        if (pointerData == null) pointerData = new PointerEventData(es);
        pointerData.position = posicion;

        resultadosUI.Clear();
        es.RaycastAll(pointerData, resultadosUI);

        // Panel_AR_UI tiene una Image transparente de pantalla completa (Raycast Target activo).
        // Ese fondo NO cuenta como "UI": si contara, ningún toque movería la cámara.
        // Solo cuentan los hijos del panel (botones, Tarjeta_ControlesAR, etc.).
        GameObject fondoPanel = UIManager.PanelActual;
        for (int i = 0; i < resultadosUI.Count; i++)
        {
            if (resultadosUI[i].gameObject != fondoPanel)
                return true;
        }
        return false;
    }

    private static float NormalizarAngulo(float angulo)
    {
        return angulo > 180f ? angulo - 360f : angulo;
    }
}
