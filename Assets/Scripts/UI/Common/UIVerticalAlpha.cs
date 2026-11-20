using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Graphic의 정점 색 알파를 세로 위치에 따라 바꾸는 메시 효과 (머티리얼 없이 위·아래 알파 그라데이션).
/// - 알파 = UITheme.LongNoteAlphaAt(t, 아래, 위). t = 0 아래 끝 → 1 위 끝 (RectTransform 기준), 그래픽 색 알파에 곱한다.
/// - 에디터 롱노트 몸통(Img_LongNote)이 인게임 몸통과 같은 위 45% → 아래 70%를 내려고 쓴다. 값은 SetAlphas로 받는다 (직렬화하지 않음).
/// - Simple Image(정점 4개)를 가정한다. 높이가 0이면 모든 정점에 아래 값을 쓴다.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UIVerticalAlpha : BaseMeshEffect
{
    private float bottomAlpha = 1f;
    private float topAlpha = 1f;

    /// <summary>아래 끝 알파 (확인용)</summary>
    public float BottomAlpha => bottomAlpha;

    /// <summary>위 끝 알파 (확인용)</summary>
    public float TopAlpha => topAlpha;

    /// <summary>
    /// 아래·위 끝 알파를 정하고 메시를 다시 만든다. 같은 값이면 아무것도 하지 않는다.
    /// </summary>
    public void SetAlphas(float bottom, float top)
    {
        if (Mathf.Approximately(bottom, bottomAlpha) && Mathf.Approximately(top, topAlpha)) return;
        bottomAlpha = bottom;
        topAlpha = top;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    /// <summary>
    /// 정점의 세로 위치 t (0 아래 ~ 1 위)에서 곱할 알파. 순수 함수 (높이 0이면 아래 값).
    /// </summary>
    public static float AlphaAt(float y, float yMin, float height, float bottom, float top)
    {
        float t = height > 0f ? (y - yMin) / height : 0f;
        return UITheme.LongNoteAlphaAt(t, bottom, top);
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        Rect rect = graphic.rectTransform.rect;
        UIVertex vertex = default;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            Color32 color = vertex.color;
            color.a = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * AlphaAt(vertex.position.y, rect.yMin, rect.height, bottomAlpha, topAlpha)), 0, 255);
            vertex.color = color;
            vh.SetUIVertex(vertex, i);
        }
    }

    /// <summary>
    /// 지금 메시의 정점 알파를 아래·위 끝으로 나눠 돌려준다 (확인용: 그래픽 색 알파 × 효과).
    /// </summary>
    public void GetEdgeAlphas(out float bottom, out float top)
    {
        Color baseColor = graphic != null ? graphic.color : Color.white;
        float height = graphic != null ? graphic.rectTransform.rect.height : 0f;
        bottom = baseColor.a * AlphaAt(0f, 0f, height, bottomAlpha, topAlpha);
        top = baseColor.a * AlphaAt(height, 0f, height, bottomAlpha, topAlpha);
    }
}
