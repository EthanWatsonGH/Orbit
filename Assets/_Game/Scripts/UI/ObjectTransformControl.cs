public enum ObjectTransformControl
{
    MoveBoth,
    MoveX,
    MoveY,
    ScaleBoth,
    // Reserved for the later Scale From Edge controls. Keep these values in place
    // so serialized prefab control bindings keep their numeric values.
    ScaleX,
    ScaleY,
    Rotate,
    Duplicate
}
