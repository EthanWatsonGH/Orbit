using System;
using UnityEngine;

// Finds grid crossings that a scale handle can reach without violating its limits.
public static class WorldScaleSnapping
{
    const float DirectionEpsilon = 0.000001f;
    const int MaximumColumnSearch = 512;

    public static bool TrySnapAlongLine(Vector2 origin, Vector2 direction, float target, float minimum, float maximum, float increment, out float result)
    {
        result = Mathf.Clamp(target, minimum, maximum);
        float bestDistance = float.PositiveInfinity;
        bool found = false;
        TryLineAxis(origin.x, direction.x, target, minimum, maximum, increment, ref found, ref bestDistance, ref result);
        TryLineAxis(origin.y, direction.y, target, minimum, maximum, increment, ref found, ref bestDistance, ref result);
        return found;
    }

    static void TryLineAxis(float origin, float direction, float target, float minimum, float maximum, float increment,
        ref bool found, ref float bestDistance, ref float result)
    {
        if (Mathf.Abs(direction) < DirectionEpsilon)
            return;

        double first = (origin + (double)direction * minimum) / increment;
        double last = (origin + (double)direction * maximum) / increment;
        double lowest = Math.Ceiling(Math.Min(first, last) - 0.000001);
        double highest = Math.Floor(Math.Max(first, last) + 0.000001);
        if (lowest > highest)
            return;

        double nearest = Math.Round((origin + (double)direction * target) / increment);
        double gridIndex = Math.Max(lowest, Math.Min(highest, nearest));
        float distance = (float)((gridIndex * increment - origin) / direction);
        float error = Mathf.Abs(distance - target);
        if (!found || error < bestDistance)
        {
            found = true;
            bestDistance = error;
            result = Mathf.Clamp(distance, minimum, maximum);
        }
    }

    public static bool TrySnapCorner(Vector2 origin, Vector2 xDirection, Vector2 yDirection,
        Vector2 target, Vector2 minimum, Vector2 maximum, float increment, out Vector2 result)
    {
        // The two directions are the selected frame's orthogonal world-space axes.
        Vector2 desired = new Vector2(Mathf.Clamp(target.x, minimum.x, maximum.x), Mathf.Clamp(target.y, minimum.y, maximum.y));
        Vector2 desiredWorld = origin + xDirection * desired.x + yDirection * desired.y;
        result = desired;
        Vector2 best = desired;
        float bestError = float.PositiveInfinity;
        bool found = false;

        // Rounding a world point moves it by at most half a cell on each axis.
        // An interior candidate gives the search a small upper distance bound.
        float marginX = increment * 0.5f * (Mathf.Abs(xDirection.x) + Mathf.Abs(xDirection.y));
        float marginY = increment * 0.5f * (Mathf.Abs(yDirection.x) + Mathf.Abs(yDirection.y));
        if (maximum.x - minimum.x >= 2f * marginX && maximum.y - minimum.y >= 2f * marginY)
        {
            Vector2 inner = new Vector2(
                Mathf.Clamp(desired.x, minimum.x + marginX, maximum.x - marginX),
                Mathf.Clamp(desired.y, minimum.y + marginY, maximum.y - marginY));
            Vector2 innerWorld = origin + xDirection * inner.x + yDirection * inner.y;
            Vector2 candidate = new Vector2(Mathf.Round(innerWorld.x / increment) * increment, Mathf.Round(innerWorld.y / increment) * increment);
            TryCandidate(candidate, origin, xDirection, yDirection, minimum, maximum, desiredWorld, ref found, ref bestError, ref best);
        }

        long centerColumn = (long)Math.Round((double)desiredWorld.x / increment);
        // Degenerate, extremely narrow feasible areas may contain no grid point.
        // Bound the search so a tiny increment can never stall the editor.
        for (int offset = 0; offset <= MaximumColumnSearch; offset++)
        {
            if (found && Math.Max(0, offset - 0.5) * increment > Math.Sqrt(bestError))
                break;
            SearchColumn(centerColumn + offset, origin, xDirection, yDirection, minimum, maximum, increment, desiredWorld, ref found, ref bestError, ref best);
            if (offset != 0)
                SearchColumn(centerColumn - offset, origin, xDirection, yDirection, minimum, maximum, increment, desiredWorld, ref found, ref bestError, ref best);
        }

        if (found)
            result = best;
        return found;
    }

    static void SearchColumn(long column, Vector2 origin, Vector2 xDirection, Vector2 yDirection,
        Vector2 minimum, Vector2 maximum, float increment, Vector2 desiredWorld,
        ref bool found, ref float bestError, ref Vector2 best)
    {
        double x = column * (double)increment;
        double lower = double.NegativeInfinity;
        double upper = double.PositiveInfinity;
        if (!IntersectRange((x - origin.x) * xDirection.x - origin.y * xDirection.y, xDirection.y, minimum.x, maximum.x, ref lower, ref upper) ||
            !IntersectRange((x - origin.x) * yDirection.x - origin.y * yDirection.y, yDirection.y, minimum.y, maximum.y, ref lower, ref upper))
            return;

        double lowest = Math.Ceiling(lower / increment - 0.000001);
        double highest = Math.Floor(upper / increment + 0.000001);
        if (lowest > highest)
            return;
        double row = Math.Max(lowest, Math.Min(highest, Math.Round((double)desiredWorld.y / increment)));
        Vector2 candidate = new Vector2((float)x, (float)(row * increment));
        TryCandidate(candidate, origin, xDirection, yDirection, minimum, maximum, desiredWorld, ref found, ref bestError, ref best);
    }

    static bool IntersectRange(double offset, double coefficient, double minimum, double maximum, ref double lower, ref double upper)
    {
        if (Math.Abs(coefficient) < DirectionEpsilon)
            return offset >= minimum && offset <= maximum;
        double first = (minimum - offset) / coefficient;
        double last = (maximum - offset) / coefficient;
        lower = Math.Max(lower, Math.Min(first, last));
        upper = Math.Min(upper, Math.Max(first, last));
        return lower <= upper;
    }

    static void TryCandidate(Vector2 point, Vector2 origin, Vector2 xDirection, Vector2 yDirection,
        Vector2 minimum, Vector2 maximum, Vector2 desiredWorld, ref bool found, ref float bestError, ref Vector2 best)
    {
        Vector2 relative = point - origin;
        Vector2 local = new Vector2(Vector2.Dot(relative, xDirection), Vector2.Dot(relative, yDirection));
        const float tolerance = 0.0001f;
        if (local.x < minimum.x - tolerance || local.x > maximum.x + tolerance ||
            local.y < minimum.y - tolerance || local.y > maximum.y + tolerance)
            return;
        float error = (point - desiredWorld).sqrMagnitude;
        if (!found || error < bestError)
        {
            found = true;
            bestError = error;
            best = local;
        }
    }
}
