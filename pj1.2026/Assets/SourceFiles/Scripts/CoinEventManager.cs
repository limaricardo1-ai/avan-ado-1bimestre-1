using UnityEngine;
using System;

public class CoinEventManager
{
    // Evento principal - disparado quando o jogador coleta uma moeda
    public static event Action<int> OnCoinCollected;

    // Evento para quando o total de moedas mudar (útil para UI)
    public static event Action<int> OnTotalCoinsChanged;

    /// <summary>
    /// Chame este método quando uma moeda for coletada
    /// </summary>
    public static void CollectCoin(int amount = 1)
    {
        OnCoinCollected?.Invoke(amount);
        OnTotalCoinsChanged?.Invoke(GetCurrentTotal()); // Atualiza UI
    }

    // Variável estática para guardar o total durante a partida
    private static int _totalCoins = 0;

    public static int GetCurrentTotal() => _totalCoins;

    public static void AddCoins(int amount)
    {
        _totalCoins += amount;
        OnTotalCoinsChanged?.Invoke(_totalCoins);
        Debug.Log($"<color=yellow>[CoinEventManager]</color> +{amount} moedas | Total: {_totalCoins}");
    }

    public static void ResetCoins()
    {
        _totalCoins = 0;
        OnTotalCoinsChanged?.Invoke(0);
    }
}