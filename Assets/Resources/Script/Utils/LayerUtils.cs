using UnityEngine;

public static class LayerUtils
{
    public static void SetLayer(GameObject _object, int _layer)
    {
        if (null == _object)
        {
            return;
        }

        // true를 인자로 넘기면 비활성화된 자식 오브젝트까지 모두 포함해서 가져옵니다.
        Transform[] allChildren = _object.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            child.gameObject.layer = _layer;
        }
        _object.layer = _layer;

    }
}
