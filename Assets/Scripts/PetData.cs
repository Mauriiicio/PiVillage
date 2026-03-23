using System.Collections.Generic;
using UnityEngine;

public enum PetType { Cat, Dog }

[System.Serializable]
public class PetAnimation
{
    public Sprite[] frames;
    public bool     loop; // true = loopeia por duracao, false = toca uma vez e segura
}

public abstract class PetData : ScriptableObject
{
    public PetType type;

    [Header("Animacoes Comuns")]
    public Sprite[] idle;
    public Sprite[] licking1;
    public Sprite[] licking2;
    public Sprite[] run;
    public Sprite[] sitting;
    public Sprite[] stretching;
    public Sprite[] walk;

    // Retorna as animacoes de acao disponiveis para sortear
    public abstract List<PetAnimation> GetActionAnimations();

    protected void AddIfValid(List<PetAnimation> list, Sprite[] frames, bool loop)
    {
        if (frames != null && frames.Length > 0)
            list.Add(new PetAnimation { frames = frames, loop = loop });
    }
}
