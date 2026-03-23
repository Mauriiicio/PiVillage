using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PiVillage/Dog Data")]
public class DogData : PetData
{
    [Header("Animacoes do Cachorro")]
    public Sprite[] bark;
    public Sprite[] itching;
    public Sprite[] lyingDown;
    public Sprite[] sleeping;

    public override List<PetAnimation> GetActionAnimations()
    {
        var list = new List<PetAnimation>();
        AddIfValid(list, bark,       loop: true);
        AddIfValid(list, itching,    loop: true);
        AddIfValid(list, licking1,   loop: true);
        AddIfValid(list, licking2,   loop: true);
        AddIfValid(list, lyingDown,  loop: false);
        AddIfValid(list, sitting,    loop: false);
        AddIfValid(list, sleeping,   loop: false);
        AddIfValid(list, stretching, loop: true);
        return list;
    }
}
