namespace CNC.Controls.Probing;

/// <summary>External/internal edge-finder probe target (legacy layout: A bottom-left, B bottom-right, C top-right, D top-left).</summary>
public enum Edge
{
    None = 0,
    A,
    B,
    C,
    D,
    Z,
    AB,
    AD,
    CB,
    CD
}

public enum Center
{
    None = 0,
    Inside,
    Outside
}

/// <summary>Rotation apply origin — mirrors legacy <c>OriginControl.Origin</c>.</summary>
public enum ProbeOrigin
{
    None = 0,
    A,
    B,
    C,
    D,
    Center,
    AB,
    AD,
    CB,
    CD,
    CurrentPos
}

public enum CenterFindMode
{
    XY = 0,
    X,
    Y
}
