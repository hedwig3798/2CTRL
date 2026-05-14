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

        // 이벤트 구독
        healthSystem.OnDeath += Play;

        // 머테리얼 블록 생성
        materialBlock = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(materialBlock);
    }

    public void Play(GameObject _object)
    {
        // 중복 실행 방지
        if (true == isDissolved)
        {
            return;
        }

        isDissolved = true;

        // 이펙트 코루틴
        StartCoroutine(DissoveLoop());
    }

    private IEnumerator DissoveLoop()
    {
        float currentTime = 0.0f;

        // 초기화
        materialBlock.SetFloat("_DissolveAmount", 1.0f);
        spriteRenderer.SetPropertyBlock(materialBlock);

        // 점점 사라지는 이펙트
        while (duration > currentTime)
        {
            currentTime += Time.deltaTime;
            float amount = currentTime / duration;

            materialBlock.SetFloat("_DissolveAmount", 1 - amount);
            spriteRenderer.SetPropertyBlock(materialBlock);

            yield return null;
        }

        isDissolved = false;
        owner.SetActive(false);
    }

    public void Initialize(BlackBoard _data)
    {
        // 값 초기화
        isDissolved = false;

        if (null == materialBlock)
        {
            Debug.LogError("DissolveEffectContorller has no MaterialBlock");
        }

        materialBlock.SetFloat("_DissolveAmount", 1.0f);
        spriteRenderer.SetPropertyBlock(materialBlock);
    }
}
