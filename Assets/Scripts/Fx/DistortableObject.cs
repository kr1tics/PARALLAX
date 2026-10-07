using System.Collections;
using UnityEngine;

public class DistortableObject : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;

    [SerializeField] private float fadeInTime = 0.2f;

    [SerializeField] private float holdTime = 1.0f;

    [SerializeField] private float fadeOutTime = 0.5f;

    [SerializeField] private bool permanentToggleMode = false;

    [SerializeField] private bool startActive = false;

private bool isActive;

    private MaterialPropertyBlock properties;
    private Coroutine activeRoutine;

    private static readonly int EffectAmount =
        Shader.PropertyToID("_EffectAmount");


    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        properties = new MaterialPropertyBlock();

        isActive = startActive;

        SetEffectAmount(isActive ? 1f : 0f);
    }


    public void ActivateDistortion()
    {
        if (permanentToggleMode)
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            isActive = !isActive;

            SetEffectAmount(isActive ? 1f : 0f);

            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine =
            StartCoroutine(DistortionSequence());
    }


    private IEnumerator DistortionSequence()
    {
        float timer = 0f;

        // Fade in from 0 → 1.
        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;

            float amount =
                Mathf.Clamp01(timer / fadeInTime);

            SetEffectAmount(amount);

            yield return null;
        }

        SetEffectAmount(1f);

        // Stay fully distorted.
        yield return new WaitForSeconds(holdTime);

        timer = 0f;

        // Fade back from 1 → 0.
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;

            float amount =
                1f -
                Mathf.Clamp01(timer / fadeOutTime);

            SetEffectAmount(amount);

            yield return null;
        }

        SetEffectAmount(0f);

        activeRoutine = null;
    }


    private void SetEffectAmount(float amount)
    {
        targetRenderer.GetPropertyBlock(properties);

        properties.SetFloat(
            EffectAmount,
            amount
        );

        targetRenderer.SetPropertyBlock(properties);
    }
}