using UnityEngine;

public enum ToolType
{
    InkLine,
    FlatPlane,
    CurvedPlane,
    Saw,
    Chisel,
    Hammer,
    Adze
}

public interface IWoodTool
{
    ToolType GetToolType();
    bool IsActive();
}
