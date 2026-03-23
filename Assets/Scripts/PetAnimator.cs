using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PetAnimator : MonoBehaviour
{
    private PetData        data;
    private SpriteRenderer sr;

    [Header("Animacao")]
    public float framesPerSecond = 10f;

    private float moveSpeed;
    private float minWalkTime;
    private float maxWalkTime;
    private float minRestTime;
    private float maxRestTime;
    private float minActionHold;
    private float maxActionHold;
    private float boundX;
    private float boundY;

    private Coroutine animCoroutine;

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
    // Loop principal de comportamento
    // -------------------------------------------------------

    IEnumerator BehaviorLoop()
    {
        while (true)
        {
            // --- Fase 1: Andar ---
            float walkTime = Random.Range(minWalkTime, maxWalkTime);
            Vector2 dir    = RandomDirection();

            FlipSprite(dir.x);
            yield return StartCoroutine(WalkFor(dir, walkTime));

            // --- Fase 2: Sentar ---
            FlipSprite(0f);
            yield return StartCoroutine(PlayOnce(data.sitting));

            // --- Fase 3: Descansar com acao aleatoria no meio ---
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
                // Volta ao sitting apos a acao
                yield return StartCoroutine(PlayOnce(data.sitting));
            }

            float remaining = restTime - actionAt;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
        }
    }

    // Anda na direcao dada pelo tempo definido
    IEnumerator WalkFor(Vector2 dir, float duration)
    {
        SetAnimation(data.walk);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector3 next = transform.position + (Vector3)(dir * moveSpeed * Time.deltaTime);

            // Rebate nas bordas
            if (Mathf.Abs(next.x) > boundX) { dir.x = -dir.x; FlipSprite(dir.x); }
            if (Mathf.Abs(next.y) > boundY) { dir.y = -dir.y; }

            transform.position = new Vector3(
                Mathf.Clamp(next.x, -boundX, boundX),
                Mathf.Clamp(next.y, -boundY, boundY),
                0f
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopAnimation();
    }

    // Fica em idle pelo tempo definido, disparando uma acao aleatoria no meio
    IEnumerator RestFor(float duration)
    {
        float elapsed = 0f;
        float actionAt = Random.Range(duration * 0.2f, duration * 0.6f);
        bool  actionDone = false;

        SetAnimation(data.idle);

        while (elapsed < duration)
        {
            if (!actionDone && elapsed >= actionAt)
            {
                actionDone = true;
                PetAnimation action = PickRandom();
                if (action != null)
                {
                    StopAnimation();
                    float holdTime = Random.Range(minActionHold, maxActionHold);

                    if (action.loop)
                    {
                        // Loopeia pelo tempo definido
                        yield return StartCoroutine(PlayLoop(action.frames, holdTime));
                    }
                    else
                    {
                        // Toca uma vez e segura o ultimo frame
                        yield return StartCoroutine(PlayOnce(action.frames));
                        yield return new WaitForSeconds(holdTime);
                    }

                    SetAnimation(data.idle);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopAnimation();
    }

    // -------------------------------------------------------
    // Controle de animacao
    // -------------------------------------------------------

    void SetAnimation(Sprite[] frames)
    {
        StopAnimation();
        if (frames != null && frames.Length > 0)
            animCoroutine = StartCoroutine(LoopFrames(frames));
    }

    void StopAnimation()
    {
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }
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
    // Helpers
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
