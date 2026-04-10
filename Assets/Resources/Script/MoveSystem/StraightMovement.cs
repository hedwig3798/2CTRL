using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Straight
    : MonoBehaviour
{
    public Vector3 dir = Vector3.zero;
    public Transform target;
    public float speed;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    private Spawnable spawnable;

    private void Awake()
    {
        if (dir == Vector3.zero)
        {
            dir = Random.insideUnitCircle.normalized;
        }

        spawnable = GetComponent<Spawnable>();

        if (spawnable != null)
        {
            spawnable.OnSpawnEvent += SetDirection;
        }
    }

    private void SetDirection()
    {
        if (null == spawnable)
        {
            return;
        }

        target = spawnable.settingData.target;

        dir = target.position - transform.position;
        dir.z = 0;
        dir = dir.normalized;
    }

    private void Update()
    {
        transform.Translate(speed * Time.deltaTime * dir);

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !(dir.x < 0);
        }
    }
}