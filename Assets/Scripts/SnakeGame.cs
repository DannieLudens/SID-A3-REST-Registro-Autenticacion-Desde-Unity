using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq; 

public class SnakeGame : MonoBehaviour
{
    // ====================================================
    // CONFIGURACIÓN DEL JUEGO
    // ====================================================
    [Header("Configuración")]
    public int gridWidth = 20;       // Ancho de la grilla
    public int gridHeight = 15;      // Alto de la grilla
    public float moveInterval = 0.15f; // Segundos entre cada movimiento

    [Header("Colores")]
    public Color colorCabeza = new Color(0.2f, 0.8f, 0.2f);
    public Color colorCuerpo = new Color(0.3f, 0.6f, 0.3f);
    public Color colorComida = new Color(0.9f, 0.2f, 0.2f);
    public Color colorFondo = new Color(0.1f, 0.1f, 0.15f);
    public Color colorBorde = new Color(0.3f, 0.3f, 0.4f);

    [Header("UI")]
    public TextMeshProUGUI textScore;
    public TextMeshProUGUI textGameOver;
    public TextMeshProUGUI textEstado;  // Para mostrar "Guardando score..."
    public TextMeshProUGUI textRecord;
    public TextMeshProUGUI textPosicion;
    public Button btnReiniciar;
    public Button btnVolver;           // Volver al menú principal

    // ====================================================
    // VARIABLES INTERNAS
    // ====================================================
    private List<Vector2Int> snake = new List<Vector2Int>();
    private Vector2Int direction = Vector2Int.right;
    private Vector2Int nextDirection = Vector2Int.right;
    private Vector2Int food;
    private int score = 0;
    private bool gameOver = false;
    private bool gameStarted = false;
    private float timer = 0f;

    // Referencia al APIManager
    private APIManager apiManager;
    private APIManager.UserData[] usuariosCargados;

    // Renderizado
    private GameObject[,] grid;
    private GameObject foodObject;
    private Transform boardParent;

    // ====================================================
    // INICIALIZACIÓN
    // ====================================================
    void Start()
{
    apiManager = FindFirstObjectByType<APIManager>();

    CrearTablero();

    btnReiniciar.gameObject.SetActive(false);
    btnVolver.gameObject.SetActive(false);
    btnReiniciar.onClick.AddListener(IniciarJuego);
    btnVolver.onClick.AddListener(VolverAlMenu);
    textGameOver.gameObject.SetActive(false);
    textEstado.text = "";

    // Cargar info primero, luego iniciar el juego
    if (apiManager != null && apiManager.IsAuthenticated)
        StartCoroutine(CargarYIniciar());
    else
        IniciarJuego();
}

IEnumerator CargarYIniciar()
{
    // Primero cargamos los usuarios
    yield return StartCoroutine(CargarInfoJugador());
    // Una vez cargados, iniciamos el juego
    IniciarJuego();
}

    void CrearTablero()
{
    boardParent = new GameObject("Board").transform;
    boardParent.SetParent(this.transform);

    grid = new GameObject[gridWidth, gridHeight];

    // Tamaño fijo por celda en unidades de Unity
    float cellSize = 0.5f;

    float totalWidth = gridWidth * cellSize;
    float totalHeight = gridHeight * cellSize;

    float offsetX = -totalWidth / 2f + cellSize / 2f;
    float offsetY = -totalHeight / 2f + cellSize / 2f;

    for (int x = 0; x < gridWidth; x++)
    {
        for (int y = 0; y < gridHeight; y++)
        {
            GameObject cell = new GameObject($"Cell_{x}_{y}");
            cell.transform.SetParent(boardParent);

            SpriteRenderer sr = cell.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();

            bool esBorde = (x == 0 || x == gridWidth - 1 || 
                           y == 0 || y == gridHeight - 1);
            sr.color = esBorde ? colorBorde : colorFondo;

            cell.transform.position = new Vector3(
                offsetX + x * cellSize,
                offsetY + y * cellSize,
                0
            );
            cell.transform.localScale = new Vector3(
                cellSize * 0.95f, 
                cellSize * 0.95f, 
                1
            );

            // sortingOrder negativo: el tablero queda visualmente
            // detrás de la culebrita y la comida
            sr.sortingOrder = -10;

            grid[x, y] = cell;
        }
    }

    // Objeto para la comida
    foodObject = new GameObject("Food");
    foodObject.transform.SetParent(boardParent);
    SpriteRenderer foodSr = foodObject.AddComponent<SpriteRenderer>();
    foodSr.sprite = CreateSquareSprite();
    foodSr.color = colorComida;
    foodSr.sortingOrder = -8;
    foodObject.transform.localScale = new Vector3(
        cellSize * 0.75f, 
        cellSize * 0.75f, 
        1
    );
}

    void IniciarJuego()
    {
        // Reiniciar estado
        snake.Clear();
        score = 0;
        gameOver = false;
        gameStarted = true;
        timer = 0f;
        direction = Vector2Int.right;
        nextDirection = Vector2Int.right;

        btnReiniciar.gameObject.SetActive(false);
        btnVolver.gameObject.SetActive(false);

        // Posición inicial de la culebrita (centro del tablero)
        int startX = gridWidth / 2;
        int startY = gridHeight / 2;
        snake.Add(new Vector2Int(startX, startY));
        snake.Add(new Vector2Int(startX - 1, startY));
        snake.Add(new Vector2Int(startX - 2, startY));

        // Actualizar UI
        ActualizarScore();
        textGameOver.gameObject.SetActive(false);
        textEstado.text = "";

        // Generar primera comida
        GenerarComida();
        DibujarTablero();
    }

    // ====================================================
    // LOOP DEL JUEGO
    // ====================================================
    void Update()
    {
        if (gameOver || !gameStarted) return;

        // Capturar input del jugador
        CapturarInput();

        // Mover la culebrita a intervalos regulares
        timer += Time.deltaTime;
        if (timer >= moveInterval)
        {
            timer = 0f;
            direction = nextDirection;
            MoverCulebrita();
        }
    }

    void CapturarInput()
    {
        // Evitar que la culebrita se mueva en dirección opuesta
        if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && direction != Vector2Int.down)
            nextDirection = Vector2Int.up;
        else if ((Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) && direction != Vector2Int.up)
            nextDirection = Vector2Int.down;
        else if ((Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) && direction != Vector2Int.right)
            nextDirection = Vector2Int.left;
        else if ((Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) && direction != Vector2Int.left)
            nextDirection = Vector2Int.right;
    }

    void MoverCulebrita()
    {
        // Calcular nueva posición de la cabeza
        Vector2Int nuevaCabeza = snake[0] + direction;

        // Verificar colisión con paredes
        if (nuevaCabeza.x <= 0 || nuevaCabeza.x >= gridWidth - 1 ||
            nuevaCabeza.y <= 0 || nuevaCabeza.y >= gridHeight - 1)
        {
            GameOver();
            return;
        }

        // Verificar colisión con el cuerpo
        for (int i = 1; i < snake.Count; i++)
        {
            if (snake[i] == nuevaCabeza)
            {
                GameOver();
                return;
            }
        }

        // Mover: agregar nueva cabeza
        snake.Insert(0, nuevaCabeza);

        // Verificar si comió la comida
        if (nuevaCabeza == food)
        {
            score += 10;
            ActualizarScore();
            GenerarComida();
            // No quitamos la cola — la culebrita crece
        }
        else
        {
            // Quitar la cola — se mantiene el tamaño
            snake.RemoveAt(snake.Count - 1);
        }

        DibujarTablero();
    }

    void GameOver()
    {
        gameOver = true;
        gameStarted = false;

        textGameOver.gameObject.SetActive(true);
        btnReiniciar.gameObject.SetActive(true);
        btnVolver.gameObject.SetActive(true);

        textGameOver.text = $"¡Game Over!\nPuntaje: {score}";

        // Si el jugador está autenticado, guardar el score automáticamente
        if (apiManager != null && apiManager.IsAuthenticated)
        {
            StartCoroutine(GuardarScore());
        }
    }

IEnumerator GuardarScore()
{
    Debug.Log("=== GUARDANDO SCORE: " + score);
    textEstado.gameObject.SetActive(true);

    // Primero obtenemos el perfil actual del usuario
    // para ver cuál es su score actual
    yield return StartCoroutine(apiManager.GetUsers((success, message, usuarios) =>
    {
        if (success && usuarios != null)
        {
            // Buscar el usuario actual en la lista
            var usuarioActual = System.Array.Find(
                usuarios, 
                u => u.username == apiManager.CurrentUsername
            );

            int scoreActual = 0;
            if (usuarioActual != null && usuarioActual.data != null)
                scoreActual = usuarioActual.data.score;

            // Solo guardar si el nuevo score es mayor
            if (score > scoreActual)
            {
                textEstado.text = "¡Nuevo récord! Guardando...";
                StartCoroutine(apiManager.UpdateScore(score, (s, m) =>
                {
                    textEstado.text = s ? 
                        $"¡Récord guardado! {scoreActual} → {score}" : 
                        "No se pudo guardar";
                    
                    // Actualizar posición y récord tras guardar
                    if (s) StartCoroutine(CargarInfoJugador());
                }));
            }
            else
            {
                textEstado.text = $"Score: {score} | Récord: {scoreActual}";
            }
        }
        else
        {
            textEstado.text = "No se pudo verificar el récord";
        }
    }));
}

IEnumerator CargarInfoJugador()
{
    yield return StartCoroutine(apiManager.GetUsers((success, message, usuarios) =>
    {
        if (success && usuarios != null)
        {
            // Guardar la lista en memoria
            usuariosCargados = usuarios;

            var ordenados = usuarios
                .OrderByDescending(u => u.data != null ? u.data.score : 0)
                .ToArray();

            int posicion = 0;
            int record = 0;

            for (int i = 0; i < ordenados.Length; i++)
            {
                if (ordenados[i].username == apiManager.CurrentUsername)
                {
                    posicion = i + 1;
                    record = ordenados[i].data != null ? ordenados[i].data.score : 0;
                    break;
                }
            }

            if (textRecord != null)
                textRecord.text = $"Récord: {record}";

            if (textPosicion != null)
                textPosicion.text = $"Posición: #{posicion} de {ordenados.Length}";
        }
    }));
}

    void VolverAlMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
    } 

    // ====================================================
    // RENDERIZADO
    // ====================================================
    void DibujarTablero()
    {
        // Resetear todos los colores del tablero
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                bool esBorde = (x == 0 || x == gridWidth - 1 || y == 0 || y == gridHeight - 1);
                grid[x, y].GetComponent<SpriteRenderer>().color = esBorde ? colorBorde : colorFondo;
                grid[x, y].GetComponent<SpriteRenderer>().sortingOrder = -10;
            }
        }

        // Dibujar la culebrita
        for (int i = 0; i < snake.Count; i++)
        {
            Vector2Int pos = snake[i];
            if (pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight)
            {
                SpriteRenderer sr = grid[pos.x, pos.y].GetComponent<SpriteRenderer>();
                sr.color = (i == 0) ? colorCabeza : colorCuerpo;
                sr.sortingOrder = -9; // encima del fondo, detrás de la UI
            }
        }

        // Posicionar la comida
        if (food.x >= 0 && food.x < gridWidth && food.y >= 0 && food.y < gridHeight)
        {
            foodObject.transform.position = grid[food.x, food.y].transform.position;
            foodObject.SetActive(true);
        }
    }

    void GenerarComida()
    {
        List<Vector2Int> celdasLibres = new List<Vector2Int>();

        for (int x = 1; x < gridWidth - 1; x++)
            for (int y = 1; y < gridHeight - 1; y++)
                if (!snake.Contains(new Vector2Int(x, y)))
                    celdasLibres.Add(new Vector2Int(x, y));

        if (celdasLibres.Count > 0)
            food = celdasLibres[Random.Range(0, celdasLibres.Count)];
    }

void ActualizarScore()
{
    if (textScore != null)
        textScore.text = $"Score: {score}";

    // Calcular posición proyectada sin petición al servidor
    if (usuariosCargados != null && textPosicion != null)
    {
        int posicion = 1;
        foreach (var u in usuariosCargados)
        {
            if (u.username != apiManager.CurrentUsername &&
                u.data != null && u.data.score > score)
            {
                posicion++;
            }
        }
        textPosicion.text = $"Posición: # {posicion} de {usuariosCargados.Length}";
    }
}

    // Crear un sprite cuadrado programáticamente
    Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(10, 10);
        Color[] pixels = new Color[100];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 10, 10), new Vector2(0.5f, 0.5f), 10);
    }
}