using UnityEngine;

public class GUIManager : MonoBehaviour
{
    public static GUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Não usa DontDestroyOnLoad porque a cena é Additive e será descarregada junto
        Debug.Log("<color=magenta>[GUIManager]</color> Inicializado com sucesso!");
    }

    // Exemplo de métodos que você pode chamar de outros scripts
    public void MostrarPainelPause(bool mostrar)
    {
        // Implementar aqui
        Debug.Log($"Pause panel {(mostrar ? "mostrado" : "escondido")}");
    }

    public void AtualizarVidaUI(float vidaAtual, float vidaMax)
    {
        // Atualizar barra de vida, etc.
    }
}