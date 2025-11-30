using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

// Clases para deserializar el JSON de Python
[Serializable]
public class BoidData
{
    public List<Vector3> items;
}

public class UnityTCPServer : MonoBehaviour
{
    [Header("Configuración del Servidor")]
    public string ipAddress = "127.0.0.1";
    public int port = 1101;

    [Header("Configuración de Agentes")]
    public GameObject boidPrefab; // Arrastra tu prefab aquí
    public int numBoids = 20;     // Debe coincidir con Python

    // Variables internas
    private Thread serverThread;
    private TcpListener listener;
    private List<GameObject> boids = new List<GameObject>();
    
    // Cola thread-safe para pasar datos del hilo del socket al hilo principal
    private string lastReceivedJSON = "";
    private object dataLock = new object();
    private bool isRunning = false;

    void Start()
    {
        // 1. Instanciar los prefabs al inicio
        for (int i = 0; i < numBoids; i++)
        {
            GameObject b = Instantiate(boidPrefab, Vector3.zero, Quaternion.identity);
            boids.Add(b);
        }

        // 2. Iniciar el servidor en otro hilo
        isRunning = true;
        serverThread = new Thread(ServerLoop);
        serverThread.IsBackground = true;
        serverThread.Start();
        Debug.Log($"Servidor TCP iniciado en {ipAddress}:{port}");
    }

    // --- LÓGICA DEL HILO SECUNDARIO (SOCKET) ---
    void ServerLoop()
    {
        try
        {
            IPAddress localAddr = IPAddress.Parse(ipAddress);
            listener = new TcpListener(localAddr, port);
            listener.Start();

            while (isRunning)
            {
                // Esperar conexión (bloqueante)
                Debug.Log("Esperando cliente Python...");
                TcpClient client = listener.AcceptTcpClient();
                Debug.Log("¡Cliente Python conectado!");

                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;

                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        // Convertir bytes a string
                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        
                        // NOTA: En un caso real robusto, deberías acumular el buffer 
                        // y buscar el caracter '\n' para separar mensajes. 
                        // Aquí asumimos que Python envía paquetes limpios o Unity procesa rápido.
                        
                        lock (dataLock)
                        {
                            lastReceivedJSON = data;
                        }
                    }
                }
                client.Close();
            }
        }
        catch (Exception e)
        {
            if (isRunning) Debug.Log("Error Socket: " + e.Message);
        }
    }

    // --- LÓGICA DEL HILO PRINCIPAL (UNITY UPDATE) ---
    void Update()
    {
        string jsonToProcess = "";

        // 1. Extraer el último mensaje recibido de forma segura
        lock (dataLock)
        {
            if (!string.IsNullOrEmpty(lastReceivedJSON))
            {
                jsonToProcess = lastReceivedJSON;
                lastReceivedJSON = ""; // Limpiar
            }
        }

        // 2. Si hay datos, mover los boids
        if (!string.IsNullOrEmpty(jsonToProcess))
        {
            try
            {
                // Limpieza básica por si llegan varios JSONs pegados (tomamos el último válido)
                // Esto es un parche simple para el problema de "framing"
                int lastClose = jsonToProcess.LastIndexOf('}');
                if (lastClose < jsonToProcess.Length - 1) 
                    jsonToProcess = jsonToProcess.Substring(0, lastClose + 1);

                BoidData data = JsonUtility.FromJson<BoidData>(jsonToProcess);

                if (data != null && data.items != null)
                {
                    for (int i = 0; i < boids.Count && i < data.items.Count; i++)
                    {
                        // Interpolación suave (Lerp) para evitar saltos
                        Vector3 currentPos = boids[i].transform.position;
                        Vector3 targetPos = data.items[i];
                        
                        // Python manda X,Y (2D). Unity es X,Z (plano) o X,Y (vertical).
                        // Ajusta aquí según tu preferencia. El script de python manda Z=0.
                        
                        boids[i].transform.position = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * 10);
                    }
                }
            }
            catch (Exception e)
            {
                // A veces llegan JSONs incompletos si la red es muy rápida, ignoramos el error de parseo
                // Debug.LogWarning("Error parseando JSON (frame droppeado): " + e.Message);
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        if (listener != null) listener.Stop();
        if (serverThread != null) serverThread.Abort();
    }
}