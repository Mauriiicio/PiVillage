using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PetAnimator : MonoBehaviour
{
    public enum BehaviorState { Normal, Fleeing, Sick, Angry, Sleeping }

    private PetData        data;
    private SpriteRenderer sr;

    [Header("Animacao")]
    public float framesPerSecond     = 10f;
    public float fleeSpeedMultiplier = 2f;

    private float moveSpeed;
    private float minWalkTime;
    private float maxWalkTime;
    private float minRestTime;
    private float maxRestTime;
    private float minActionHold;
    private float maxActionHold;
    private float boundX;
    private float boundY;

    private Coroutine     animCoroutine;
    private BehaviorState currentState = BehaviorState.Normal;

    public void Init(PetData petData, float bX, float bY, float speed,
                     float minWalk, float maxWalk, float minRest, float maxRest,
                     float minHold, float maxHold)
    {
        data          = petData;
        boundX        = bX;
        boundY        = bY;
        moveSpeed     = speed;
        minWalkTime   = minWalk;
        maxWalkTime   = maxWalk;
        minRestTime   = minRest;
        maxRestTime   = maxRest;
        minActionHold = minHold;
        maxActionHold = maxHold;
        sr            = GetComponent<SpriteRenderer>();
        StartCoroutine(BehaviorLoop());
    }

    // -------------------------------------------------------
    // API pública de estado
    // -------------------------------------------------------

    // Chamado pelo PetStatusManager sempre que o estado do pet mudar
    public void UpdateFromPetState(PetState state)
    {
        if (state.isSick)  { SetBehaviorState(BehaviorState.Sick);   return; }
        if (state.isAngry) { SetBehaviorState(BehaviorState.Angry);  return; }
        // Só volta ao normal se não estava no meio de um banho
        if (currentState != BehaviorState.Fleeing)
            SetBehaviorState(BehaviorState.Normal);
    }

    // Usados pela mecânica de banho
    public void StartFleeing() => SetBehaviorState(BehaviorState.Fleeing);
    public void StopFleeing()  => SetBehaviorState(BehaviorState.Normal);

    // Usado pelo PetClickHandler para acordar o pet
    public void WakeUp()
    {
        if (currentState == BehaviorState.Sleeping)
            SetBehaviorState(BehaviorState.Normal);
    }

    void SetBehaviorState(BehaviorState newState)
    {
        if (currentState == newState) return;

        bool wasSleeping = currentState == BehaviorState.Sleeping;
        currentState = newState;

        StopAllCoroutines();
        animCoroutine = null;

        if (wasSleeping)
            PetStatusManager.Instance?.OnPetSleepEnd();

        switch (newState)
        {
            case BehaviorState.Normal:   StartCoroutine(BehaviorLoop()); break;
            case BehaviorState.Fleeing:  StartCoroutine(FleeLoop());     break;
            case BehaviorState.Sick:     StartCoroutine(SickLoop());     break;
            case BehaviorState.Angry:    StartCoroutine(AngryLoop());    break;
            case BehaviorState.Sleeping: StartCoroutine(SleepLoop());    break;
        }
    }

    // -------------------------------------------------------
    // Loops de comportamento
    // -------------------------------------------------------

    IEnumerator BehaviorLoop()
    {
        while (true)
        {
            float walkTime = Random.Range(minWalkTime, maxWalkTime);
            Vector2 dir    = RandomDirection();
            FlipSprite(dir.x);
            yield return StartCoroutine(WalkFor(dir, walkTime));

            FlipSprite(0f);
            yield return StartCoroutine(PlayOnce(data.sitting));

            float restTime = Random.Range(minRestTime, maxRestTime);
            float actionAt = Random.Range(restTime * 0.2f, restTime * 0.6f);
            yield return new WaitForSeconds(actionAt);

            PetAnimation action = PickRandom();
            if (action != null)
            {
                float holdTime = Random.Range(minActionHold, maxActionHold);
                if (action.loop)
                    yield return StartCoroutine(PlayLoop(action.frames, holdTime));
                else
                {
                    yield return StartCoroutine(PlayOnce(action.frames));
                    yield return new WaitForSeconds(holdTime);
                }
                yield return StartCoroutine(PlayOnce(data.sitting));
            }

            float remaining = restTime - actionAt;
            if (remaining > 0f) yield return new WaitForSeconds(remaining);

            // 30% de chance de dormir após o descanso
            if (Random.value < 0.3f)
            {
                SetBehaviorState(BehaviorState.Sleeping);
                yield break;
            }
        }
    }

    IEnumerator WalkFor(Vector2 dir, float duration)
    {
        SetAnimation(data.walk);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 next = transform.position + (Vector3)(dir * moveSpeed * Time.deltaTime);
            if (Mathf.Abs(next.x) > boundX) { dir.x = -dir.x; FlipSprite(dir.x); }
            if (Mathf.Abs(next.y) > boundY) { dir.y = -dir.y; }
            transform.position = new Vector3(
                Mathf.Clamp(next.x, -boundX, boundX),
                Mathf.Clamp(next.y, -boundY, boundY), 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        StopAnimation();
    }

    // Pet foge correndo (banho)
    IEnumerator FleeLoop()
    {
        Sprite[] runFrames = (data.run != null && data.run.Length > 0) ? data.run : data.walk;
        SetAnimation(runFrames);

        Vector2 dir = RandomDirection();
        FlipSprite(dir.x);

        while (currentState == BehaviorState.Fleeing)
        {
            Vector3 next = transform.position + (Vector3)(dir * moveSpeed * fleeSpeedMultiplier * Time.deltaTime);
            if (Mathf.Abs(next.x) > boundX) { dir.x = -dir.x; FlipSprite(dir.x); }
            if (Mathf.Abs(next.y) > boundY) { dir.y = -dir.y; }
            transform.position = new Vector3(
                Mathf.Clamp(next.x, -boundX, boundX),
                Mathf.Clamp(next.y, -boundY, boundY), 0f);
            yield return null;
        }
    }

    // Pet fica deitado enquanto doente — toca uma vez e segura o último frame
    IEnumerator SickLoop()
    {
        Sprite[] frames = GetLyingFrames();

        if (frames != null && frames.Length > 0)
        {
            yield return StartCoroutine(PlayOnce(frames));
            // Segura o último frame parado
            sr.sprite = frames[frames.Length - 1];
        }

        while (currentState == BehaviorState.Sick) yield return null;
    }

    // Pet late/mia repetidamente enquanto bravo
    IEnumerator AngryLoop()
    {
        Sprite[] frames = GetBarkFrames();

        while (currentState == BehaviorState.Angry)
        {
            if (frames != null && frames.Length > 0)
                yield return StartCoroutine(PlayOnce(frames));
            else
                yield return new WaitForSeconds(1f);

            yield return new WaitForSeconds(0.6f); // pausa entre latidos
        }
    }

    // Pet dorme — toca animação uma vez, segura último frame por até 60s
    IEnumerator SleepLoop()
    {
        PetStatusManager.Instance?.OnPetSleepStart();

        Sprite[] frames = GetSleepFrames();
        if (frames != null && frames.Length > 0)
        {
            yield return StartCoroutine(PlayOnce(frames));
            sr.sprite = frames[frames.Length - 1]; // segura último frame
        }

        float elapsed = 0f;
        while (currentState == BehaviorState.Sleeping && elapsed < 60f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (currentState == BehaviorState.Sleeping)
            SetBehaviorState(BehaviorState.Normal);
    }

    // -------------------------------------------------------
    // Helpers de animação por tipo de pet
    // -------------------------------------------------------

    Sprite[] GetLyingFrames()
    {
        var cat = data as CatData;
        if (cat != null && cat.laying   != null && cat.laying.Length   > 0) return cat.laying;
        var dog = data as DogData;
        if (dog != null && dog.lyingDown != null && dog.lyingDown.Length > 0) return dog.lyingDown;
        return data.idle;
    }

    Sprite[] GetBarkFrames()
    {
        var dog = data as DogData;
        if (dog != null && dog.bark != null && dog.bark.Length > 0) return dog.bark;
        var cat = data as CatData;
        if (cat != null && cat.meow != null && cat.meow.Length > 0) return cat.meow;
        return data.idle;
    }

    Sprite[] GetSleepFrames()
    {
        var dog = data as DogData;
        if (dog != null && dog.sleeping  != null && dog.sleeping.Length  > 0) return dog.sleeping;
        var cat = data as CatData;
        if (cat != null && cat.sleeping1 != null && cat.sleeping1.Length > 0) return cat.sleeping1;
        return data.idle;
    }

    // -------------------------------------------------------
    // Controle de animação
    // -------------------------------------------------------

    void SetAnimation(Sprite[] frames)
    {
        StopAnimation();
        if (frames != null && frames.Length > 0)
            animCoroutine = StartCoroutine(LoopFrames(frames));
    }

    void StopAnimation()
    {
        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
    }

    IEnumerator LoopFrames(Sprite[] frames)
    {
        float delay = 1f / Mathf.Max(1f, framesPerSecond);
        int   i     = 0;
        while (true)
        {
            sr.sprite = frames[i];
            i = (i + 1) % frames.Length;
            yield return new WaitForSeconds(delay);
        }
    }

    IEnumerator PlayLoop(Sprite[] frames, float duration)
    {
        float delay   = 1f / Mathf.Max(1f, framesPerSecond);
        float elapsed = 0f;
        int   i       = 0;
        while (elapsed < duration)
        {
            sr.sprite = frames[i];
            i = (i + 1) % frames.Length;
            yield return new WaitForSeconds(delay);
            elapsed += delay;
        }
    }

    IEnumerator PlayOnce(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0) yield break;
        float delay = 1f / Mathf.Max(1f, framesPerSecond);
        foreach (Sprite frame in frames)
        {
            sr.sprite = frame;
            yield return new WaitForSeconds(delay);
        }
    }

    // -------------------------------------------------------
    // Helpers gerais
    // -------------------------------------------------------

    Vector2 RandomDirection()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }

    void FlipSprite(float dirX)
    {
        if (dirX == 0f) return;
        sr.flipX = dirX < 0f;
    }

    PetAnimation PickRandom()
    {
        List<PetAnimation> available = data.GetActionAnimations();
        if (available.Count == 0) return null;
        return available[Random.Range(0, available.Count)];
    }
}
