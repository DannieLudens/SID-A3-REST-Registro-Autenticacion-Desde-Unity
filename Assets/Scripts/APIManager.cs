using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class APIManager : MonoBehaviour
{
    // ====================================================
    // URL BASE DE LA API
    // ====================================================
    // Esta es la dirección del servidor, igual que en Postman
    private const string BASE_URL = "https://sid-restapi.onrender.com";

    // ====================================================
    // VARIABLE PARA GUARDAR EL TOKEN
    // ====================================================
    // Cuando hacemos login, el servidor nos da un token.
    // Lo guardamos aquí para usarlo en las demás peticiones.
    // Es lo mismo que copiabas en Postman y pegabas en Headers.
    private string token = "";

    // Variable para guardar el nombre del usuario logueado
    private string currentUsername = "";

    // ====================================================
    // PROPIEDADES PÚBLICAS (para leer desde otros scripts)
    // ====================================================
    public string Token => token;
    public string CurrentUsername => currentUsername;
    public bool IsAuthenticated => !string.IsNullOrEmpty(token);

    // ====================================================
    // PERSISTENCIA DEL TOKEN (PlayerPrefs)
    // ====================================================
    // PlayerPrefs guarda datos en el navegador (localStorage en WebGL).
    // Así el token sobrevive cuando se recarga la página.

    private void SaveToken()
    {
        PlayerPrefs.SetString("auth_token", token);
        PlayerPrefs.SetString("auth_username", currentUsername);
        PlayerPrefs.Save();
    }

    public void LoadToken()
    {
        token = PlayerPrefs.GetString("auth_token", "");
        currentUsername = PlayerPrefs.GetString("auth_username", "");
    }

    private void ClearToken()
    {
        PlayerPrefs.DeleteKey("auth_token");
        PlayerPrefs.DeleteKey("auth_username");
        PlayerPrefs.Save();
    }

    // ====================================================
    // EVENTO DE TOKEN EXPIRADO
    // ====================================================
    // Cuando el servidor responde 401, significa que el token
    // ya no es válido. Este evento avisa al UIManager para
    // que mande al usuario de vuelta al login.
    public event Action OnTokenExpired;

    private bool CheckTokenExpired(UnityWebRequest request)
    {
        if (request.responseCode == 401)
        {
            Debug.LogWarning("Token expirado o inválido. Redirigiendo al login...");
            Logout();
            OnTokenExpired?.Invoke();
            return true;
        }
        return false;
    }

    // ====================================================
    // CLASES PARA CONVERTIR JSON ↔ OBJETOS C#
    // ====================================================
    // Cuando en Postman veías la respuesta JSON, Unity necesita
    // "traducir" ese JSON a objetos de C#. Estas clases definen
    // la estructura esperada.

    [Serializable]
    public class UserCredentials
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class LoginResponse
    {
        public string token;
        // El servidor también puede devolver datos del usuario
    }

    [Serializable]
    public class UserData
    {
        public string _id;
        public string username;
        public bool state;
        public UserDataFields data;
    }

    [Serializable]
    public class UserDataFields
    {
        public int score;
    }

    [Serializable]
    public class UpdateRequest
    {
        public string username;
        public UserDataFields data;
    }

    [Serializable]
    public class UserListResponse
    {
        public UserData[] usuarios;
    }

    // ====================================================
    // 1. REGISTRO — POST /api/usuarios
    // ====================================================
    // Esto es EXACTAMENTE lo que hiciste en Postman:
    // - Método: POST
    // - URL: /api/usuarios
    // - Body: { "username": "...", "password": "..." }
    // - Content-Type: application/json
    public IEnumerator Register(string username, string password, Action<bool, string> callback)
    {
        // Crear el objeto con los datos (igual que el JSON del Body en Postman)
        UserCredentials credentials = new UserCredentials
        {
            username = username,
            password = password
        };

        // Convertir el objeto a texto JSON
        string jsonBody = JsonUtility.ToJson(credentials);

        // Convertir el texto a bytes (para enviarlo por la red)
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        // Crear la petición (como cuando seleccionabas POST y ponías la URL en Postman)
        using (UnityWebRequest request = new UnityWebRequest(BASE_URL + "/api/usuarios", "POST"))
        {
            // Adjuntar el body (como cuando escribías el JSON en la pestaña Body)
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // Preparar para recibir la respuesta
            request.downloadHandler = new DownloadHandlerBuffer();

            // Poner el header Content-Type (en Postman se ponía automático al elegir JSON)
            request.SetRequestHeader("Content-Type", "application/json");

            // ¡Enviar! (como dar clic en SEND)
            yield return request.SendWebRequest();

            // Revisar la respuesta
            if (request.result == UnityWebRequest.Result.Success)
            {
                callback(true, "Registro exitoso. Ya puedes iniciar sesión.");
            }
            else
            {
                callback(false, "Error en registro: " + request.downloadHandler.text);
            }
        }
    }

    // ====================================================
    // 2. LOGIN — POST /api/auth/login
    // ====================================================
    // Igual que registro, pero a otra URL.
    // La diferencia: la respuesta trae un TOKEN que debemos guardar.
    public IEnumerator Login(string username, string password, Action<bool, string> callback)
    {
        UserCredentials credentials = new UserCredentials
        {
            username = username,
            password = password
        };

        string jsonBody = JsonUtility.ToJson(credentials);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(BASE_URL + "/api/auth/login", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // AQUÍ ESTÁ LA MAGIA:
                // Extraemos el token de la respuesta JSON
                // En Postman veías algo como: { "token": "eyJhbGci..." }
                // Ahora lo guardamos en nuestra variable
                LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
                token = response.token;
                currentUsername = username;

                // Guardar el token para que persista al recargar
                SaveToken();

                callback(true, "Login exitoso.");
            }
            else
            {
                callback(false, "Error en login: " + request.downloadHandler.text);
            }
        }
    }

    // ====================================================
    // 3. ACTUALIZAR SCORE — PATCH /api/usuarios
    // ====================================================
    // Aquí usamos PATCH (como en la Imagen 3 del profesor).
    // NOVEDAD: Además del Body, necesitamos enviar el token
    // en un HEADER llamado "x-token" (como hacías en Postman
    // en la pestaña Headers).
    public IEnumerator UpdateScore(int score, Action<bool, string> callback)
    {
        UpdateRequest updateData = new UpdateRequest
        {
            username = currentUsername,
            data = new UserDataFields { score = score }
        };

        string jsonBody = JsonUtility.ToJson(updateData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(BASE_URL + "/api/usuarios", "PATCH"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // ¡AQUÍ ESTÁ EL HEADER DEL TOKEN!
            // Es exactamente lo que hacías en Postman:
            // Key: x-token    Value: (tu token)
            request.SetRequestHeader("x-token", token);

            yield return request.SendWebRequest();

            // Verificar si el token expiró
            if (CheckTokenExpired(request)) yield break;

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback(true, "Score actualizado correctamente.");
            }
            else
            {
                callback(false, "Error actualizando score: " + request.downloadHandler.text);
            }
        }
    }

    // ====================================================
    // 4. LISTAR USUARIOS — GET /api/usuarios
    // ====================================================
    // Petición GET (solo pedir datos, no enviar body).
    // Necesita el header x-token.
    // Devuelve la lista de todos los usuarios con sus scores.
    public IEnumerator GetUsers(Action<bool, string, UserData[]> callback)
    {
        Debug.Log("=== INICIANDO GetUsers ===");
        Debug.Log("Token actual: " + (string.IsNullOrEmpty(token) ? "VACÍO" : token.Substring(0, 20) + "..."));

        using (UnityWebRequest request = UnityWebRequest.Get(BASE_URL + "/api/usuarios"))
        {
            request.SetRequestHeader("x-token", token);

            yield return request.SendWebRequest();

            Debug.Log("Status HTTP: " + request.responseCode);
            Debug.Log("Resultado: " + request.result);

            // Verificar si el token expiró
            if (CheckTokenExpired(request)) yield break;

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log("Respuesta del servidor: " + jsonResponse.Substring(0, Mathf.Min(500, jsonResponse.Length)));

                // Intentar parsear la respuesta
                try
                {
                    // Verificar si la respuesta ya es un objeto con "usuarios" o es un array
                    if (jsonResponse.TrimStart().StartsWith("["))
                    {
                        string wrappedJson = "{\"usuarios\":" + jsonResponse + "}";
                        Debug.Log("Respuesta es un array, envolviendo...");
                        UserListResponse response = JsonUtility.FromJson<UserListResponse>(wrappedJson);
                        Debug.Log("Usuarios encontrados: " + (response.usuarios != null ? response.usuarios.Length.ToString() : "NULL"));
                        callback(true, "Usuarios obtenidos.", response.usuarios);
                    }
                    else
                    {
                        Debug.Log("Respuesta es un objeto, intentando parsear directo...");
                        UserListResponse response = JsonUtility.FromJson<UserListResponse>(jsonResponse);
                        Debug.Log("Usuarios encontrados: " + (response.usuarios != null ? response.usuarios.Length.ToString() : "NULL"));
                        callback(true, "Usuarios obtenidos.", response.usuarios);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Error parseando JSON: " + e.Message);
                    callback(false, "Error parseando respuesta", null);
                }
            }
            else
            {
                Debug.LogError("Error HTTP: " + request.downloadHandler.text);
                callback(false, "Error obteniendo usuarios: " + request.downloadHandler.text, null);
            }
        }
    }

    // ====================================================
    // 5. LOGOUT
    // ====================================================
    // Simplemente borramos el token de memoria.
    // Sin token, el usuario ya no puede hacer peticiones protegidas.
    public void Logout()
    {
        token = "";
        currentUsername = "";
        ClearToken();
    }
}