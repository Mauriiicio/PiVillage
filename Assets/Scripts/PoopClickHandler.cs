using UnityEngine;

// Colocado em cada cocô spawnado. Detecta clique e notifica o PetPoopManager.
public class PoopClickHandler : MonoBehaviour
{
    void OnMouseDown()
    {
        if (PetPoopManager.Instance != null)
            PetPoopManager.Instance.CollectPoop(gameObject);
    }
}
