using System.Drawing;
using UnityEngine;
using UnityEngine.UI;

public class FollowUI : MonoBehaviour
{
    public Transform target;
    public Vector2 offset;
    public Vector2 scale;
    public CombatObject combat;
    public Camera renderCamera;

    private Slider slider;
    private RectTransform myTransform;

    private void Awake()
    {
        myTransform = GetComponent<RectTransform>();

        slider = GetComponent<Slider>();
        if (null == slider)
        {
            Debug.LogError($"{gameObject.name} has no slider component");
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdatePositionAndScale();

        slider.value = combat.stats.hp / combat.stats.maxHP;
    }

    private void OnValidate()
    {
        UpdatePositionAndScale();
    }

    private void UpdatePositionAndScale()
    {
        myTransform = GetComponent<RectTransform>();

        if (renderCamera == null
            || myTransform == null)
        {
            return;
        }

        Vector2 finalPosition = renderCamera.WorldToScreenPoint(target.position);
        finalPosition += offset;

        myTransform.position = finalPosition;
        myTransform.localScale = scale;
    }
}
