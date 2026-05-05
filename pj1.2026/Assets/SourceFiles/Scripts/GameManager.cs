using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;




public class GameManager : MonoBehaviour
{
    // 1. Enum com os estados solicitados
    public enum GameState
    {
        Iniciando,
        MenuPrincipal,
        Gameplay
    }

    // 2. Implementação Singleton
    public static GameManager Instance { get; private set; }

    [Header("Configurações")]
    public GameState estadoAtual;
    
    // Referência para o Player que será buscado na cena de Gameplay
    private PlayerInput _playerInputNaCena;
    #region Singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("Destruindo instância duplicada do GameManager.");
            Destroy(this.gameObject); 
            return; // IMPORTANTE: Impede que o resto do código rode no objeto que vai morrer
        }

        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
    #endregion
    private void Start()
    {
        // Como estamos na _Boot, vamos para a Splash automaticamente
        CarregarCena("Cena_Splash");
    }

    // 3. Gerenciamento de Estados com Debug.Log
    public void MudarEstado(GameState novoEstado)
    {
        if (estadoAtual == novoEstado) return;

        estadoAtual = novoEstado;
        Debug.Log($"<color=cyan>[GameManager]</color> Estado: <b>{estadoAtual}</b>");

        switch (estadoAtual)
        {
            case GameState.Gameplay:
                // Verificação de segurança tripla antes de iniciar a Corrotina
                if (this != null && gameObject.activeInHierarchy && this.enabled)
                {
                    StopAllCoroutines(); // Limpa corrotinas anteriores para evitar conflitos
                    StartCoroutine(AlocarInputAposCarregamento());
                }
                else
                {
                    // Se ele estiver inativo agora, tentamos novamente em 0.1 segundos
                    // O 'Invoke' funciona mesmo que o script esteja momentaneamente desativado
                    Invoke(nameof(TentarReativarGameplay), 0.1f);
                }
                break;
        }
    }

    private void TentarReativarGameplay()
    {
        if (gameObject.activeInHierarchy)
            StartCoroutine(AlocarInputAposCarregamento());
    }

    // 4. Controle Único de Cenas
    // Somente o GameManager acessa o SceneManager
   public void CarregarCena(string nomeDaCena)
    {
        // Em vez de mudar o estado imediatamente, vamos usar um evento da Unity
        // que avisa quando a cena terminou de carregar.
        SceneManager.LoadScene(nomeDaCena);
        
        // Subscrevemos temporariamente a um evento da Unity
        SceneManager.sceneLoaded += AoTerminarDeCarregar;
    }

    private void AoTerminarDeCarregar(Scene cena, LoadSceneMode modo)
    {
        // 1. Removemos o evento para não disparar de novo na próxima cena
        SceneManager.sceneLoaded -= AoTerminarDeCarregar;

        // 2. Agora que a cena carregou, mudamos o estado com segurança
        if (cena.name == "Cena_MenuPrincipal") 
            MudarEstado(GameState.MenuPrincipal);
        else if (cena.name == "GetStarted_Scene") 
            MudarEstado(GameState.Gameplay);
    }

    // 5. Alocação de Inputs (Input System)
    private IEnumerator AlocarInputAposCarregamento()
    {
        // Espera um frame para garantir que os objetos da cena foram instanciados
        yield return new WaitForEndOfFrame();

        _playerInputNaCena = FindFirstObjectByType<PlayerInput>();

        if (_playerInputNaCena != null)
        {
            Debug.Log("<color=green>[GameManager]</color> Input alocado com sucesso ao Player.");
            // Feijão com farinha 
            // mas aqui você pode forçar esquemas de controle se necessário:
            _playerInputNaCena.ActivateInput();
        }
        else
        {
            Debug.LogWarning("[GameManager] PlayerInput não encontrado na cena de Gameplay.");
        }
    }

    // Função para o botão Sair
    public void SairDoJogo()
    {
        Debug.Log("Saindo do jogo...");
        Application.Quit();
    }
}