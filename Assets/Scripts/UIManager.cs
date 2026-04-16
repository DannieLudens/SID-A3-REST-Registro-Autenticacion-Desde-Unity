using System;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // ====================================================
    // REFERENCIAS A LOS PANELES
    // ====================================================
    // Estos son los 3 paneles que creaste en el Canvas.
    // Los arrastramos desde el Inspector.
    [Header("Paneles")]
    public GameObject panelLogin;
    public GameObject panelRegistro;
    public GameObject panelJuego;

    // ====================================================
    // REFERENCIAS DEL PANEL LOGIN
    // ====================================================
    [Header("Login")]
    public TMP_InputField inputUsuarioLogin;
    public TMP_InputField inputPasswordLogin;
    public TextMeshProUGUI textMensajeLogin;

    // ====================================================
    // REFERENCIAS DEL PANEL REGISTRO
    // ====================================================
    [Header("Registro")]
    public TMP_InputField inputUsuarioRegistro;
    public TMP_InputField inputPasswordRegistro;
    public TextMeshProUGUI textMensajeRegistro;

    // ====================================================
    // REFERENCIAS DEL PANEL JUEGO
    // ====================================================
    [Header("Juego")]
    public TextMeshProUGUI textBienvenida;
    public TMP_InputField inputScore;
    public TextMeshProUGUI textMiNombre;
    public Transform contentTabla; // El "Content" dentro del ScrollView

    // ====================================================
    // PREFAB PARA FILAS DE LA TABLA (lo crearemos después)
    // ====================================================
    [Header("Tabla de Puntajes")]
    public GameObject filaPrefab;

    // ====================================================
    // REFERENCIA AL API MANAGER
    // ====================================================
    private APIManager apiManager;

    // ====================================================
    // INICIALIZACIÓN
    // ====================================================
    void Start()
    {
        // Buscar el APIManager en la escena
        apiManager = FindFirstObjectByType<APIManager>();

        // Suscribirse al evento de token expirado
        // Si en cualquier momento el servidor dice que el token ya no sirve,
        // automáticamente volvemos al login
        apiManager.OnTokenExpired += HandleTokenExpired;

        // Intentar cargar un token guardado previamente
        apiManager.LoadToken();

        // Si ya hay un token guardado, ir directo al panel de juego
        if (apiManager.IsAuthenticated)
        {
            textBienvenida.text = "Bienvenido, " + apiManager.CurrentUsername;
            MostrarPanel("juego");
            OnClickRefrescarTabla();
        }
        else
        {
            // Si no hay token, mostrar el login
            MostrarPanel("login");
        }
    }

    // Se ejecuta cuando el token expira
    void HandleTokenExpired()
    {
        textMensajeLogin.text = "Tu sesión expiró. Por favor inicia sesión de nuevo.";
        inputUsuarioLogin.text = "";
        inputPasswordLogin.text = "";
        MostrarPanel("login");
    }

    // ====================================================
    // NAVEGACIÓN ENTRE PANELES
    // ====================================================
    // Activar un panel y desactivar los demás
    public void MostrarPanel(string panel)
    {
        panelLogin.SetActive(panel == "login");
        panelRegistro.SetActive(panel == "registro");
        panelJuego.SetActive(panel == "juego");
    }

    // Método público para que SnakeGame pueda volver al menú
    public void MostrarPanelJuego()
    {
        MostrarPanel("juego");
        OnClickRefrescarTabla();
    }

    // Botón "Ir a Registro"
    public void OnClickIrARegistro()
    {
        textMensajeRegistro.text = "";
        MostrarPanel("registro");
    }

    // Botón "Volver a Login"
    public void OnClickVolverALogin()
    {
        textMensajeLogin.text = "";
        MostrarPanel("login");
    }

    // ====================================================
    // REGISTRO
    // ====================================================
    // Se llama cuando el usuario hace clic en "Registrarse"
    public void OnClickRegistrar()
    {
        string username = inputUsuarioRegistro.text;
        string password = inputPasswordRegistro.text;

        // Validar que no estén vacíos
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            textMensajeRegistro.text = "Por favor llena todos los campos.";
            return;
        }

        textMensajeRegistro.text = "Registrando...";

        // Llamar a la función de registro del APIManager
        // StartCoroutine es necesario porque las peticiones HTTP
        // son asíncronas (tardan un momento en ir y volver del servidor)
        StartCoroutine(apiManager.Register(username, password, (success, message) =>
        {
            textMensajeRegistro.text = message;
            if (success)
            {
                // Esperar un momento y volver al login
                Invoke(nameof(OnClickVolverALogin), 2f);
            }
        }));
    }

    // ====================================================
    // LOGIN
    // ====================================================
    // Se llama cuando el usuario hace clic en "Iniciar sesión"
    public void OnClickLogin()
    {
        string username = inputUsuarioLogin.text;
        string password = inputPasswordLogin.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            textMensajeLogin.text = "Por favor llena todos los campos.";
            return;
        }

        textMensajeLogin.text = "Iniciando sesión...";

        StartCoroutine(apiManager.Login(username, password, (success, message) =>
        {
            if (success)
            {
                // ¡Login exitoso! Mostrar el panel de juego
                textBienvenida.text = "Bienvenido, " + apiManager.CurrentUsername;
                MostrarPanel("juego");

                // Cargar la tabla de puntajes automáticamente
                OnClickRefrescarTabla();
            }
            else
            {
                textMensajeLogin.text = message;
            }
        }));
    }

    // ====================================================
    // ACTUALIZAR SCORE
    // ====================================================
    public void OnClickActualizarScore()
    {
        string scoreText = inputScore.text;

        if (string.IsNullOrEmpty(scoreText))
        {
            textBienvenida.text = "Escribe un puntaje.";
            return;
        }

        // Intentar convertir el texto a número
        if (!int.TryParse(scoreText, out int score))
        {
            textBienvenida.text = "El puntaje debe ser un número.";
            return;
        }

        textBienvenida.text = "Actualizando score...";

        StartCoroutine(apiManager.UpdateScore(score, (success, message) =>
        {
            textBienvenida.text = message;
            if (success)
            {
                // Refrescar la tabla para ver el cambio
                OnClickRefrescarTabla();
                inputScore.text = "";
            }
        }));
    }

    // ====================================================
    // REFRESCAR TABLA DE PUNTAJES
    // ====================================================
    public void OnClickRefrescarTabla()
    {
        StartCoroutine(apiManager.GetUsers((success, message, usuarios) =>
        {
            if (success && usuarios != null)
            {
                // Limpiar filas anteriores
                foreach (Transform child in contentTabla)
                {
                    Destroy(child.gameObject);
                }

                // Ordenar de mayor a menor score
                var ordenados = usuarios
                    .Where(u => u.data != null)
                    .OrderByDescending(u => u.data.score)
                    .ToArray();

                // Crear una fila por cada usuario
                for (int i = 0; i < ordenados.Length; i++)
                {
                    GameObject fila = Instantiate(filaPrefab, contentTabla);
                    TextMeshProUGUI textoFila = fila.GetComponentInChildren<TextMeshProUGUI>();
                    if (textoFila != null)
                    {
                        textoFila.text = $"#{i + 1}  {ordenados[i].username} — {ordenados[i].data.score} pts";
                    }
                }
            }
            else
            {
                textBienvenida.text = message;
            }
        }));
    }

    // ====================================================
    // LOGOUT
    // ====================================================
    public void OnClickLogout()
    {
        apiManager.Logout();
        textMensajeLogin.text = "";
        inputUsuarioLogin.text = "";
        inputPasswordLogin.text = "";
        MostrarPanel("login");
    }
}