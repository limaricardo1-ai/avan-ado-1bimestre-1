using UnityEngine;

public class PlayerOM : MonoBehaviour
{
    // Referência para o script principal do Jogador
    private Player player;
    private int _lastTotalCoins = 0;

    private void Awake()
    {
        // Caso esqueça de arrastar no Inspector, tenta pegar no mesmo GameObject
        if (player == null)
        {
            player = GetComponent<Player>();
        }
    }

    private void OnEnable()
    {
        // Pegamos o valor atual antes de começar a ouvir os eventos
        _lastTotalCoins = CoinEventManager.GetCurrentTotal();

        // Nos inscrevemos no evento que o AddCoins() dispara
        CoinEventManager.OnTotalCoinsChanged += HandleTotalCoinsChanged;
    }

    private void OnDisable()
    {
        CoinEventManager.OnTotalCoinsChanged -= HandleTotalCoinsChanged;
    }

    private void HandleTotalCoinsChanged(int newTotalCoins)
    {
        // Calcula a diferença entre o novo total e o total antigo (ex: 5 - 4 = 1)
        int amountCollected = newTotalCoins - _lastTotalCoins;

        // Atualiza o histórico com o novo total
        _lastTotalCoins = newTotalCoins;

        // Se o valor aumentou (foi uma coleta), notifica o jogador
        if (amountCollected > 0 && player != null)
        {
            player.NotifyCoinPickup(amountCollected);
        }
    }

}