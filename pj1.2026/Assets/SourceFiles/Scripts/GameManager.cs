using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Iniciando,
        MenuPrincipal,
        Gameplay
    }

    public static GameManager Instance { get; private set; }

    [Header("Configurações de Cenas")]
    [SerializeField] private string cenaSplash = "CenaSplash";
    [SerializeField] private string cenaMenu = "MenuPrincipal";
    [SerializeField] private string cenaGameplay = "GetStarted_Scene";

    [Header("Status")]
    public GameState estadoAtual;

    private PlayerInput _playerInputNaCena;

    private void Awake()
    {
        ConfigurarSingleton();

        // Sempre começa como Iniciando
        estadoAtual = GameState.Iniciando;
        Debug.Log($"<color=cyan>[GameManager]</color> Estado inicial forçado: <b>{estadoAtual}</b>");

        SceneManager.sceneLoaded += OnSceneLoaded_General;
        GarantirAudioListenerUnico();
    }

    private void OnDestroy()
    {
        // Limpar inscrição para evitar leaks
        SceneManager.sceneLoaded -= OnSceneLoaded_General;
    }

    private void Start()
    {
        // Garante que o jogo comece carregando a primeira cena lógica (se essa for a intenção do projeto)
        CarregarCena(cenaSplash);
    }

    private void ConfigurarSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Altera o estado lógico do jogo e dispara comportamentos específicos.
    /// </summary>
    public void TrocarEstado(GameState novoEstado)
    {
        if (estadoAtual == novoEstado) return;

        estadoAtual = novoEstado;
        Debug.Log($"<color=cyan>[GameManager]</color> Novo Estado: <b>{estadoAtual}</b>");

        switch (estadoAtual)
        {
            case GameState.Iniciando:
                // Normalmente não há a necessidade de configurar nada aqui, pois a cena de splash é apenas um placeholder
                break;
            case GameState.Gameplay:
                IniciarConfiguracaoGameplay();
                break;
            case GameState.MenuPrincipal:
                Time.timeScale = 1f; // Garante que o jogo não esteja pausado no menu
                break;
        }
    }

    private void IniciarConfiguracaoGameplay()
    {
        if (gameObject.activeInHierarchy)
        {
            StopAllCoroutines();
            StartCoroutine(ConfigurarPlayerInputRoutine());
        }
    }

    public void CarregarCena(string nomeDaCena)
    {
        // Inscreve no evento antes de carregar
        SceneManager.sceneLoaded += AoTerminarDeCarregar;
        SceneManager.LoadScene(nomeDaCena);
    }

    private void AoTerminarDeCarregar(Scene cena, LoadSceneMode modo)
    {
        // Importante: Desinscrever sempre para evitar vazamento de memória
        SceneManager.sceneLoaded -= AoTerminarDeCarregar;

        // Lógica de troca de estado baseada na cena carregada
        if (cena.name == cenaSplash)
            TrocarEstado(GameState.Iniciando);

        if (cena.name == cenaMenu)
            TrocarEstado(GameState.MenuPrincipal);
        
    }

    // Handler geral para todas as cenas carregadas (não remove a inscrição)
    private void OnSceneLoaded_General(Scene cena, LoadSceneMode modo)
    {
        // Ajusta estado conforme a cena recém carregada (garante comportamento também quando a cena
        // já estava aberta no editor ao pressionar Play)
        AtualizarEstadoParaCena(cena.name);

        // Garante que apenas 1 AudioListener esteja ativo após um carregamento de cena
        GarantirAudioListenerUnico();
    }

    // Centraliza o mapeamento entre nome de cena e GameState
    private void AtualizarEstadoParaCena(string nomeCena)
    {
        if (string.IsNullOrEmpty(nomeCena)) return;

        if (nomeCena == cenaSplash)
            TrocarEstado(GameState.Iniciando);
        else if (nomeCena == cenaMenu)
            TrocarEstado(GameState.MenuPrincipal);
        else if (nomeCena == cenaGameplay)
            TrocarEstado(GameState.Gameplay);
    }

    private IEnumerator ConfigurarPlayerInputRoutine()
    {
        // Espera o fim do frame para garantir que os objetos de Awake/Start da cena carregada rodaram
        yield return new WaitForEndOfFrame();

        _playerInputNaCena = Object.FindFirstObjectByType<PlayerInput>();

        if (_playerInputNaCena != null)
        {
            _playerInputNaCena.ActivateInput();
            Debug.Log("<color=green>[GameManager]</color> Controle do Jogador Ativado.");
        }
        else
        {
            Debug.LogWarning("[GameManager] PlayerInput não encontrado na cena atual.");
        }
    }

    // Garante que exista apenas 1 AudioListener ativo na cena; desativa os demais.
    // Isso resolve o erro: "There are 2 audio listeners in the scene..."
    private void GarantirAudioListenerUnico()
    {
        var listeners = FindObjectsOfType<AudioListener>();
        if (listeners == null || listeners.Length <= 1) return;

        // Tentaremos manter preferencialmente um AudioListener habilitado que esteja em uma Camera ativa.
        AudioListener manter = null;

        foreach (var l in listeners)
        {
            if (l == null) continue;
            var cam = l.GetComponent<Camera>();
            if (cam != null && cam.isActiveAndEnabled)
            {
                manter = l;
                break;
            }
        }

        if (manter == null)
        {
            // se não encontramos por câmera, pegamos o primeiro habilitado
            foreach (var l in listeners)
            {
                if (l != null && l.enabled)
                {
                    manter = l;
                    break;
                }
            }
        }

        if (manter == null)
        {
            // fallback: escolher o primeiro disponível
            manter = listeners[0];
        }

        int desativados = 0;
        foreach (var l in listeners)
        {
            if (l == null) continue;
            if (l != manter && l.enabled)
            {
                l.enabled = false;
                desativados++;
            }
        }

        if (desativados > 0)
        {
            Debug.LogWarning($"[GameManager] Foram encontrados múltiplos AudioListeners. Desativei {desativados} deles para garantir exatamente 1 ativo. Mantido: {manter.gameObject.name}");
        }
    }

    public void SairDoJogo()
    {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}