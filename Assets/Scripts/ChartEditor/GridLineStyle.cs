using System;
using UnityEngine;

/// <summary>
/// 그리드 선 하나의 모양 (색·굵기). 인스펙터에서 선 종류마다 바꿀 수 있다.
/// </summary>
[Serializable]
public class GridLineStyle
{
    public Color color = Color.white;

    [Tooltip("선 굵기 (1920×1080 기준 px)")]
    [Min(0.5f)] public float thickness = 2f;

    public GridLineStyle(Color color, float thickness)
    {
        this.color = color;
        this.thickness = thickness;
    }
}
