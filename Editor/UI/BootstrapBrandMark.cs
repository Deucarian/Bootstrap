using UnityEngine;
using UnityEngine.UIElements;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapBrandMark : VisualElement
    {
        private readonly CustomStyleProperty<Color> foreground = new CustomStyleProperty<Color>("--bootstrap-text");
        private Color color = Color.white;

        internal BootstrapBrandMark()
        {
            name = "bootstrap-brand-icon";
            pickingMode = PickingMode.Ignore;
            AddToClassList("bootstrap-brand-icon");
            RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                if (evt.customStyle.TryGetValue(foreground, out var next)) color = next;
                MarkDirtyRepaint();
            });
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            float unit = Mathf.Min(contentRect.width, contentRect.height) / 26f;
            Vector2 offset = contentRect.center - Vector2.one * unit * 12;
#if UNITY_2022_2_OR_NEWER
            var painter = context.painter2D;
            painter.strokeColor = color;
            painter.lineWidth = unit * 1.6f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            void Path(params Vector2[] points)
            {
                painter.BeginPath();
                painter.MoveTo(offset + points[0] * unit);
                for (int i = 1; i < points.Length; i++) painter.LineTo(offset + points[i] * unit);
                painter.Stroke();
            }
#else
            void Path(params Vector2[] points)
            {
                var mesh = context.Allocate((points.Length - 1) * 4, (points.Length - 1) * 6);
                for (int i = 0; i < points.Length - 1; i++)
                {
                    Vector2 start = offset + points[i] * unit, end = offset + points[i + 1] * unit;
                    Vector2 direction = (end - start).normalized;
                    Vector2 normal = new Vector2(-direction.y, direction.x) * unit * .8f;
                    foreach (var point in new[] { start - normal, start + normal, end + normal, end - normal })
                        mesh.SetNextVertex(new Vertex { position = new Vector3(point.x, point.y, Vertex.nearZ), tint = color });
                    ushort a = (ushort)(i * 4);
                    mesh.SetNextIndex(a); mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex((ushort)(a + 2));
                    mesh.SetNextIndex(a); mesh.SetNextIndex((ushort)(a + 2)); mesh.SetNextIndex((ushort)(a + 3));
                }
            }
#endif
            Path(new Vector2(12, 1), new Vector2(22, 6.5f), new Vector2(22, 18), new Vector2(12, 24),
                new Vector2(2, 18), new Vector2(2, 6.5f), new Vector2(12, 1));
            Path(new Vector2(2, 6.5f), new Vector2(12, 12), new Vector2(22, 6.5f));
            Path(new Vector2(2, 6.5f), new Vector2(12, 24), new Vector2(22, 6.5f));
            Path(new Vector2(12, 12), new Vector2(12, 24));
            Path(new Vector2(4, 7.5f), new Vector2(12, 5), new Vector2(20, 7.5f));
        }
    }
}
