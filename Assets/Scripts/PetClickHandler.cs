using UnityEngine;

// Colocado dinamicamente no GameObject do pet ao ser spawnado.
// Detecta clique do jogador e notifica o PetStatusManager conforme o modo ativo.
public class PetClickHandler : MonoBehaviour
{
    public enum InteractionMode { None, Bath, WakeUp }

    public InteractionMode CurrentMode { get; set; } = InteractionMode.None;

    void OnMouseDown()
    {
        if (CurrentMode == InteractionMode.None) return;

        switch (CurrentMode)
        {
            case InteractionMode.Bath:
                PetStatusManager.Instance?.OnPetClickedForBath();
                break;
            case InteractionMode.WakeUp:
                PetStatusManager.Instance?.OnPetClickedToWakeUp();
                break;
        }

        CurrentMode = InteractionMode.None;
    }
}
