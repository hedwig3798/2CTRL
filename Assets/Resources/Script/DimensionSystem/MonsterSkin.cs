using UnityEngine;

/// <summary>
/// 차원별 몬스터 외형 (Spawner.SpawnAt 에서 MonsterData 를 키로 적용)
/// - 본체(SpriteRenderer 오브젝트) 에 Animator 가 있는 몬스터는 animator 만 쓰고 sprite 는 무시한다
/// - Animator 가 없는 몬스터는 sprite 만 쓴다
/// - 비어 있는 값은 프리팹 기본값을 쓴다
/// </summary>
[System.Serializable]
public struct MonsterSkin
{
    [Tooltip("외형을 바꿀 몬스터 (키)")]
    public MonsterData monster;

    [Tooltip("이 차원에서 쓸 애니메이터 (비우면 프리팹 기본)")]
    public AnimatorOverrideController animator;

    [Tooltip("Animator 가 없는 몬스터에 쓸 스프라이트 (Animator 가 있으면 무시, 비우면 프리팹 기본)")]
    public Sprite sprite;
}
