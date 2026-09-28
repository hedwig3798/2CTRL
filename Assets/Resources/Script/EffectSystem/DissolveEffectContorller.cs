using System.Collections;
using UnityEngine;

public class DissolveEffectContorller
    : MonoBehaviour
    , Initializable
{
    public GameObject owner;

    public bool isDissolved { get; private set; }

    public SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock materialBlock;
    public float duration = 2.0f;

    public HealthSystem healthSystem;

    private void Awake()
    {
        if (null == spriteRenderer)
        {
            Debug.LogError("Dissolve Effect Controller has no Sprite Renderer");
            return;
        }

        if (null == healthSystem)
        {
            Debug.LogError("Dissolve Effect Controller has no Health System");
            return;
        }

        // ???? ????
        healthSystem.OnDeath += Play;

        // ??????? ???? ????
        materialBlock = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(materialBlock);
    }

    public void Play(GameObject _object)
    {
        // ??? ???? ????
        if (true == isDissolved)
        {
            return;
        }

        isDissolved = true;

        // ????? ????
        StartCoroutine(DissoveLoop());
    }

    private IEnumerator DissoveLoop()
    {
        float currentTime = 0.0f;

        // ????
        materialBlock.SetFloat("_DissolveAmount", 1.0f);
        spriteRenderer.SetPropertyBlock(materialBlock);

        // ???? ??????? ?????
        while (duration > currentTime)
        {
            currentTime += Time.deltaTime;
            float amount = currentTime / duration;

            materialBlock.SetFloat("_DissolveAmount", 1 - amount);
            spriteRenderer.SetPropertyBlock(materialBlock);

            yield return null;
        }

        isDissolved = false;
        if (null != owner)
        {
            owner.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void Initialize(BlackBoard _data)
    {
        // ?? ????
        isDissolved = false;

        if (null == materialBlock || null == spriteRenderer)
        {
            Debug.LogError("DissolveEffectContorller has no MaterialBlock");
            return;
        }

        materialBlock.SetFloat("_DissolveAmount", 1.0f);
        spriteRenderer.SetPropertyBlock(materialBlock);
    }
}
