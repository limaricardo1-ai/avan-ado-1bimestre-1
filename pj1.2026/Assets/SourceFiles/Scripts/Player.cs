using UnityEngine;

public class Player : MonoBehaviour
{
    public void NotifyCoinPickup(int amount)
    {
        // Aqui está o Debug notificando a coleta através do script do jogador
        Debug.Log($"<color=green>[Player]</color> Fui notificado pelo PlayerOM! Coletei {amount} moeda(s).");

        // Exemplo de lógica interna do jogador:
        // PlayCoinSound();
        // TriggerCoinAnimation();
    }
}