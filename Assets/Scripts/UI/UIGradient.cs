using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/UIGradient")]
public class UIGradient : BaseMeshEffect
{
    public Color colorTop = Color.white;
    public Color colorBottom = Color.black;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;

        List<UIVertex> vertices = new List<UIVertex>();
        vh.GetUIVertexStream(vertices);

        float bottomY = vertices[0].position.y;
        float topY = vertices[0].position.y;

        for (int i = 1; i < vertices.Count; i++)
        {
            float y = vertices[i].position.y;
            if (y > topY) topY = y;
            if (y < bottomY) bottomY = y;
        }

        float uiElementHeight = topY - bottomY;

        for (int i = 0; i < vertices.Count; i++)
        {
            UIVertex v = vertices[i];
            float yRatio = (v.position.y - bottomY) / uiElementHeight;
            Color32 color = Color32.Lerp(colorBottom, colorTop, yRatio);
            v.color = color;
            vertices[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(vertices);
    }
}
