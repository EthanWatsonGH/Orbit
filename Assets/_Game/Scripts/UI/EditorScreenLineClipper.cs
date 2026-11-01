using UnityEngine;

// Clips a parametric screen-space line to a viewport without changing its origin.
internal static class EditorScreenLineClipper
{
    public static bool Clip(Vector2 origin, Vector2 direction, Rect rect, ref float minimum, ref float maximum)
    {
        return ClipAxis(origin.x, direction.x, rect.xMin, rect.xMax, ref minimum, ref maximum) &&
               ClipAxis(origin.y, direction.y, rect.yMin, rect.yMax, ref minimum, ref maximum);
    }

    static bool ClipAxis(float origin, float direction, float lower, float upper, ref float minimum, ref float maximum)
    {
        if (Mathf.Abs(direction) < 0.00001f)
            return origin >= lower && origin <= upper;

        float first = (lower - origin) / direction;
        float second = (upper - origin) / direction;
        minimum = Mathf.Max(minimum, Mathf.Min(first, second));
        maximum = Mathf.Min(maximum, Mathf.Max(first, second));
        return minimum <= maximum;
    }
}
