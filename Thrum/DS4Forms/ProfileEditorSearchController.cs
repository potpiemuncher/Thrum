using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WinWPF.DS4Forms
{
    internal sealed class ProfileEditorSearchController
    {
        private sealed class SearchTarget
        {
            public SearchTarget(FrameworkElement element, TabItem sectionTab,
                Expander denseSection, int navigationIndex)
            {
                Element = element;
                SectionTab = sectionTab;
                DenseSection = denseSection;
                NavigationIndex = navigationIndex;
            }

            public FrameworkElement Element { get; }
            public TabItem SectionTab { get; }
            public Expander DenseSection { get; }
            public int NavigationIndex { get; }
        }

        private readonly TabControl sectionTabs;
        private readonly Expander axisSection;
        private readonly Expander touchpadSection;
        private readonly Expander gyroSection;
        private readonly Popup resultsPopup;
        private readonly TextBlock statusText;
        private readonly Action<int> navigate;
        private readonly ProfileEditorSearchIndex index = new();
        private readonly ProfileEditorSearchHighlighter highlighter = new();
        private bool indexed;

        public ProfileEditorSearchController(TabControl sectionTabs,
            Expander axisSection, Expander touchpadSection,
            Expander gyroSection, Popup resultsPopup, TextBlock statusText,
            Action<int> navigate)
        {
            this.sectionTabs = sectionTabs ??
                throw new ArgumentNullException(nameof(sectionTabs));
            this.axisSection = axisSection ??
                throw new ArgumentNullException(nameof(axisSection));
            this.touchpadSection = touchpadSection ??
                throw new ArgumentNullException(nameof(touchpadSection));
            this.gyroSection = gyroSection ??
                throw new ArgumentNullException(nameof(gyroSection));
            this.resultsPopup = resultsPopup ??
                throw new ArgumentNullException(nameof(resultsPopup));
            this.statusText = statusText ??
                throw new ArgumentNullException(nameof(statusText));
            this.navigate = navigate ??
                throw new ArgumentNullException(nameof(navigate));
        }

        public ObservableCollection<ProfileEditorSearchEntry> Results
        {
            get;
        } = new();

        public void EnsureIndexed()
        {
            if (indexed)
            {
                return;
            }

            BuildIndex();
            indexed = true;
        }

        public void UpdateResults(string query)
        {
            Results.Clear();
            if (!indexed || string.IsNullOrWhiteSpace(query))
            {
                resultsPopup.IsOpen = false;
                statusText.Text =
                    "Type a setting label, then press Enter to open it.";
                return;
            }

            IReadOnlyList<ProfileEditorSearchEntry> matches =
                index.Search(query);
            foreach (ProfileEditorSearchEntry match in matches)
            {
                Results.Add(match);
            }

            resultsPopup.IsOpen = matches.Count > 0;
            statusText.Text = matches.Count == 0
                ? "No setting label matches this search."
                : $"{matches.Count} match{(matches.Count == 1 ? string.Empty : "es")}. Press Enter to open the first.";
        }

        public bool OpenFirst()
        {
            if (Results.Count == 0)
            {
                return false;
            }

            Open(Results[0]);
            return true;
        }

        public void Open(ProfileEditorSearchEntry result)
        {
            if (result?.Target is not SearchTarget target)
            {
                return;
            }

            navigate(target.NavigationIndex);
            if (target.DenseSection != null)
            {
                target.DenseSection.IsExpanded = true;
            }

            resultsPopup.IsOpen = false;
            statusText.Text = $"Opened {result.SectionName}: {result.Label}.";

            sectionTabs.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() => HighlightResult(result, target)));
        }

        public void ClosePopup() => resultsPopup.IsOpen = false;

        public void ClearHighlight()
        {
            resultsPopup.IsOpen = false;
            highlighter.Clear();
        }

        private void BuildIndex()
        {
            index.Clear();
            for (int tabIndex = 0; tabIndex < sectionTabs.Items.Count;
                tabIndex++)
            {
                if (sectionTabs.Items[tabIndex] is not TabItem sectionTab)
                {
                    continue;
                }

                string sectionName = ContentText(sectionTab.Header);
                Expander denseSection = tabIndex switch
                {
                    0 => axisSection,
                    2 => touchpadSection,
                    3 => gyroSection,
                    _ => null,
                };
                int navigationIndex = tabIndex + 4;
                FrameworkElement sectionTarget = denseSection ??
                    sectionTab.Content as FrameworkElement ?? sectionTab;
                index.Add(sectionName, sectionName,
                    new SearchTarget(sectionTarget, sectionTab, denseSection,
                        navigationIndex));

                if (sectionTab.Content is not DependencyObject sectionContent)
                {
                    continue;
                }

                foreach (DependencyObject current in WalkEditorTree(sectionContent))
                {
                    if (current is FrameworkElement element &&
                        TryGetSearchLabel(element, out string label))
                    {
                        index.Add(label, sectionName,
                            new SearchTarget(element, sectionTab, denseSection,
                                navigationIndex));
                    }
                }
            }
        }

        private void HighlightResult(ProfileEditorSearchEntry result,
            SearchTarget target)
        {
            FrameworkElement highlightTarget = target.Element.IsVisible &&
                target.Element.ActualHeight > 0
                    ? target.Element
                    : target.DenseSection?.IsVisible == true
                        ? target.DenseSection
                        : target.SectionTab.Content as FrameworkElement;
            if (highlightTarget == null)
            {
                return;
            }

            highlightTarget.BringIntoView();
            highlighter.Highlight(highlightTarget);
            if (!ReferenceEquals(highlightTarget, target.Element))
            {
                statusText.Text =
                    $"Opened {result.SectionName}. Choose the matching output mode to show {result.Label}.";
            }
        }

        private static IEnumerable<DependencyObject> WalkEditorTree(
            DependencyObject root)
        {
            Stack<DependencyObject> pending = new();
            HashSet<DependencyObject> visited = new();
            pending.Push(root);

            while (pending.Count > 0)
            {
                DependencyObject current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                yield return current;

                foreach (object child in LogicalTreeHelper.GetChildren(current))
                {
                    if (child is DependencyObject dependencyChild)
                    {
                        pending.Push(dependencyChild);
                    }
                }

                if (current is Visual ||
                    current is System.Windows.Media.Media3D.Visual3D)
                {
                    int childCount = VisualTreeHelper.GetChildrenCount(current);
                    for (int childIndex = 0; childIndex < childCount;
                        childIndex++)
                    {
                        pending.Push(VisualTreeHelper.GetChild(current,
                            childIndex));
                    }
                }
            }
        }

        private static bool TryGetSearchLabel(FrameworkElement element,
            out string label)
        {
            object content = element switch
            {
                Label control => control.Content,
                CheckBox control => control.Content,
                RadioButton control => control.Content,
                Button control => control.Content,
                GroupBox control => control.Header,
                Expander control => control.Header,
                _ => null,
            };
            label = ContentText(content);
            return !string.IsNullOrWhiteSpace(label) && label != "..." &&
                label.Any(char.IsLetter);
        }

        private static string ContentText(object content)
        {
            return content switch
            {
                string text => text,
                TextBlock textBlock => textBlock.Text,
                AccessText accessText => accessText.Text,
                _ => string.Empty,
            };
        }
    }
}
