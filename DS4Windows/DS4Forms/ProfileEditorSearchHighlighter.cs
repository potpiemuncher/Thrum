using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace DS4WinWPF.DS4Forms
{
    internal sealed class ProfileEditorSearchHighlighter
    {
        private readonly DispatcherTimer timer;
        private AdornerLayer layer;
        private SearchHighlightAdorner adorner;

        public ProfileEditorSearchHighlighter()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2),
            };
            timer.Tick += (_, _) => Clear();
        }

        public void Highlight(FrameworkElement target)
        {
            Clear();
            layer = AdornerLayer.GetAdornerLayer(target);
            if (layer == null)
            {
                return;
            }

            adorner = new SearchHighlightAdorner(target);
            layer.Add(adorner);
            timer.Start();
        }

        public void Clear()
        {
            timer.Stop();
            if (layer != null && adorner != null)
            {
                layer.Remove(adorner);
            }

            layer = null;
            adorner = null;
        }

        private sealed class SearchHighlightAdorner : Adorner
        {
            private readonly Border border;
            private readonly VisualCollection visuals;

            public SearchHighlightAdorner(UIElement adornedElement)
                : base(adornedElement)
            {
                IsHitTestVisible = false;
                border = new Border
                {
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(-3),
                    Opacity = 0.75,
                };
                border.SetResourceReference(Border.BorderBrushProperty,
                    "AccentColor");
                border.SetResourceReference(Border.BackgroundProperty,
                    "NavigationSelectionColor");
                visuals = new VisualCollection(this) { border };
            }

            protected override int VisualChildrenCount => visuals.Count;

            protected override Visual GetVisualChild(int index) =>
                visuals[index];

            protected override Size MeasureOverride(Size constraint)
            {
                border.Measure(AdornedElement.RenderSize);
                return AdornedElement.RenderSize;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                border.Arrange(new Rect(finalSize));
                return finalSize;
            }
        }
    }
}
