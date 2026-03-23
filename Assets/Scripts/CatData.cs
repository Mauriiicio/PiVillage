using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PiVillage/Cat Data")]
public class CatData : PetData
{
    [Header("Animacoes do Gato")]
    public Sprite[] itch;
    public Sprite[] laying;
    public Sprite[] meow;
    public Sprite[] sleeping1;
    public Sprite[] sleeping2;

    public override List<PetAnimation> GetActionAnimations()
    {
        var list = new List<PetAnimation>();
        AddIfValid(list, itch,       loop: true);
        AddIfValid(list, laying,     loop: false);
        AddIfValid(list, licking1,   loop: true);
        AddIfValid(list, licking2,   loop: true);
        AddIfValid(list, meow,       loop: true);
        AddIfValid(list, sitting,    loop: false);
        AddIfValid(list, sleeping1,  loop: false);
        AddIfValid(list, sleeping2,  loop: false);
        AddIfValid(list, stretching, loop: true);
        return list;
    }
}
