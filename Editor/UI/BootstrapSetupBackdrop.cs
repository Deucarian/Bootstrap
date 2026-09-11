using UnityEngine;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor
{
    // Bootstrap must render before the Editor package is installed.
    internal sealed class BootstrapSetupBackdrop : VisualElement
    {
        private Color background = new Color32(27, 40, 44, 255);
        internal BootstrapSetupBackdrop()
        {
            pickingMode = PickingMode.Ignore; style.position = Position.Absolute;
            style.top = style.left = style.bottom = style.right = 0;
            RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                if (evt.customStyle.TryGetValue(new CustomStyleProperty<Color>("--bootstrap-bg"), out var value)) background = value;
                MarkDirtyRepaint();
            });
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            if (contentRect.width <= 0 || contentRect.height <= 0) return;
            const int divisions = 16, side = divisions + 1;
            var mesh = context.Allocate(side * side, divisions * divisions * 6);
            for (int y = 0; y <= divisions; y++)
            for (int x = 0; x <= divisions; x++)
            {
                float u = x / (float)divisions, v = y / (float)divisions;
                float light = Mathf.Exp(-((u - .76f) * (u - .76f) * 4 + (v - .38f) * (v - .38f) * 3));
                Color tint = Color.Lerp(background * (background.grayscale < .5f ? .7f : .97f), background, light); tint.a = 1;
                mesh.SetNextVertex(new Vertex { position = new Vector3(u * contentRect.width, v * contentRect.height, Vertex.nearZ), tint = tint });
            }
            for (int y = 0; y < divisions; y++)
            for (int x = 0; x < divisions; x++)
            {
                ushort a = (ushort)(y * side + x);
                mesh.SetNextIndex(a); mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex((ushort)(a + side));
                mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex((ushort)(a + side + 1)); mesh.SetNextIndex((ushort)(a + side));
            }
        }
    }
}
