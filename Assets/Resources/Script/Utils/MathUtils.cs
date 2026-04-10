using UnityEngine;

public static class MathUtils
{
    public static Vector3 GetDirection(Vector3 _from, Vector3 _to)
    {
        return (_to - _from).normalized;
    }

    public static Vector3 GetDirection(Transform _from, Transform _to)
    {
        return (_to.position - _from.position).normalized;
    }
}
