using UnityEngine;
using System.Collections;

public class SplashController : MonoBehaviour
{
    
   void Start()
    {
        StartCoroutine(EsperaSplash());
    }

    IEnumerator EsperaSplash()
    {
        yield return new WaitForSeconds(2f);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CarregarCena("MenuPrincipal");
    }
    }
}