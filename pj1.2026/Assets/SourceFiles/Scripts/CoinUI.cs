using UnityEngine;
using TMPro; // Use TextMeshPro

public class CoinUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private string prefixo = "Moedas: ";

    private void OnEnable()
    {
        CoinEventManager.OnTotalCoinsChanged += AtualizarUI;
    }

    private void OnDisable()
    {
        CoinEventManager.OnTotalCoinsChanged -= AtualizarUI;
    }

    private void Start()
    {
        CoinEventManager.ResetCoins(); // Reseta ao iniciar a partida
        coinText.color = Color.yellow; // Define a cor do texto para amarelo
        AtualizarUI(0);
    }

    private void AtualizarUI(int total)
    {
        if (coinText != null)
        {
            coinText.text = $"{prefixo}{total}";
        }
    }

    // Método público caso queira atualizar manualmente
    public void AtualizarManual(int valor)
    {
        AtualizarUI(valor);
    }
}