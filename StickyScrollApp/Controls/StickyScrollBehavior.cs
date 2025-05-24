using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StickyScrollApp.Controls
{
    /// <summary>
    /// Stickyヘッダー機能をTreeView等に付与するビヘイビア
    /// </summary>
    public class StickyScrollBehavior
    {
        private readonly TreeView _treeView;
        private readonly Func<ScrollViewer> _getScrollViewer;
        private readonly Func<HeaderedContentControl> _getStickyHeaderControl;
        private readonly Func<bool> _getAllowStickyScroll;
        private readonly Func<ControlTemplate> _getStickyHeaderContentTemplate;

        public StickyScrollBehavior(
            TreeView treeView,
            Func<ScrollViewer> getScrollViewer,
            Func<HeaderedContentControl> getStickyHeaderControl,
            Func<bool> getAllowStickyScroll,
            Func<ControlTemplate> getStickyHeaderContentTemplate)
        {
            _treeView = treeView;
            _getScrollViewer = getScrollViewer;
            _getStickyHeaderControl = getStickyHeaderControl;
            _getAllowStickyScroll = getAllowStickyScroll;
            _getStickyHeaderContentTemplate = getStickyHeaderContentTemplate;
        }

        public void UpdateStickyHeaders()
        {
            var scrollViewer = _getScrollViewer();
            var stickyHeaderControl = _getStickyHeaderControl();
            if (scrollViewer == null || stickyHeaderControl == null)
                return;

            UpdateStickyHeaderMargin(scrollViewer, stickyHeaderControl);

            if (!_getAllowStickyScroll())
            {
                stickyHeaderControl.Header = null;
                stickyHeaderControl.Content = null;
                return;
            }

            bool clear = false;

            try
            {
                if (scrollViewer.VerticalOffset == 0)
                {
                    clear = true;
                    return;
                }

                TreeViewItem topItem = FindTopVisibleTreeViewItem(scrollViewer, stickyHeaderControl);
                if (topItem == null)
                {
                    clear = true;
                    return;
                }
                //Debug.WriteLine($"一番上の要素: {topItem.DataContext}");

                var ancestors = GetAncestors(topItem).ToList();
                if (!ancestors.Any())
                {
                    //Debug.WriteLine($"{topItem.DataContext} の祖先ノードが見つかりません。");
                    clear = true;
                    return;
                }

                if (IsSameHeader(stickyHeaderControl, ancestors))
                {
                    return;
                }

                HeaderedContentControl parent = stickyHeaderControl;
                parent.Header = ancestors.First().DataContext;
                parent.Content = null;

                foreach (var ancestor in ancestors.Skip(1))
                {
                    var child = CreateHeaderElement(ancestor.DataContext);
                    parent.Content = child;
                    parent = child;
                }
            }
            finally
            {
                if (clear)
                {
                    if (stickyHeaderControl != null)
                    {
                        stickyHeaderControl.Header = null;
                        stickyHeaderControl.Content = null;
                    }
                }
            }
        }

        private bool IsSameHeader(HeaderedContentControl stickyHeaderControl, List<TreeViewItem> ancestors)
        {
            var headers = new List<object>();
            HeaderedContentControl header = stickyHeaderControl;
            do
            {
                headers.Add(header.Header);
                header = header.Content as HeaderedContentControl;
            } while (header != null);

            if (headers.Count != ancestors.Count)
                return false;

            for (int i = 0; i < ancestors.Count; i++)
            {
                if (headers[i] != ancestors[i].DataContext)
                {
                    return false;
                }
            }
            return true;
        }

        private TreeViewItem FindTopVisibleTreeViewItem(ScrollViewer scrollViewer, HeaderedContentControl stickyHeaderControl)
        {
            var visibleItems = new List<TreeViewItem>();
            FindVisibleTreeViewItemsRecursive(_treeView, visibleItems);

            int estimatedHeaderCount = GetMaxAncestorCount(visibleItems);
            double estimatedStickyHeaderHeight = EstimateStickyHeaderHeight(stickyHeaderControl, estimatedHeaderCount);

            var topItem = FindTopItem(visibleItems, scrollViewer, estimatedStickyHeaderHeight, stickyHeaderControl);

            int ancestorCount = GetAncestors(topItem).Count();
            Debug.WriteLine($"最大数: {estimatedHeaderCount}, 再見積もり数: {ancestorCount}");

            return FindTopItem(visibleItems, scrollViewer, EstimateStickyHeaderHeight(stickyHeaderControl, ancestorCount), stickyHeaderControl);
        }

        private int GetMaxAncestorCount(List<TreeViewItem> items)
        {
            int max = 0;
            foreach (var tvi in items)
            {
                int count = GetAncestors(tvi).Count();
                if (count > max) max = count;
            }
            return max;
        }

        private double EstimateStickyHeaderHeight(HeaderedContentControl stickyHeaderControl, int headerCount)
        {
            int depth = GetStickyHeaderDepth(stickyHeaderControl);
            HeaderedContentControl current = stickyHeaderControl;
            double headerElementHeight = (depth > 0) ? (current.ActualHeight / depth) : 24;
            return headerElementHeight * headerCount;
        }

        private TreeViewItem FindTopItem(List<TreeViewItem> items, ScrollViewer scrollViewer, double stickyHeaderHeight, HeaderedContentControl stickyHeaderControl)
        {
            TreeViewItem topVisibleItem = null;
            double minDistance = double.MaxValue;

            foreach (var tvi in items)
            {
                try
                {
                    var transform = tvi.TransformToAncestor(scrollViewer);
                    var point = transform.Transform(new Point(0, 0));
                    var itemRect = new Rect(point, tvi.RenderSize);

                    if (itemRect.Bottom > stickyHeaderHeight && itemRect.Top < scrollViewer.ViewportHeight)
                    {
                        double distance = Math.Abs(itemRect.Top - stickyHeaderHeight);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            topVisibleItem = tvi;
                        }
                        if (itemRect.Top == stickyHeaderHeight)
                        {
                            return tvi;
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
            }
            return topVisibleItem;
        }

        private void FindVisibleTreeViewItemsRecursive(ItemsControl parent, List<TreeViewItem> accumulator)
        {
            if (parent == null) return;

            foreach (var item in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi && tvi.IsVisible)
                {
                    accumulator.Add(tvi);
                    if (tvi.IsExpanded && tvi.Items.Count > 0)
                    {
                        FindVisibleTreeViewItemsRecursive(tvi, accumulator);
                    }
                }
            }
        }

        private IEnumerable<TreeViewItem> GetAncestors(TreeViewItem item)
        {
            var list = new List<TreeViewItem>();
            DependencyObject current = VisualTreeHelper.GetParent(item);
            while (current != null)
            {
                if (current is TreeViewItem tvi)
                {
                    list.Insert(0, tvi);
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return list;
        }

        private HeaderedContentControl CreateHeaderElement(object ancestor)
        {
            return new HeaderedContentControl
            {
                Header = ancestor,
                Template = _getStickyHeaderContentTemplate()
            };
        }

        private int GetStickyHeaderDepth(HeaderedContentControl stickyHeaderControl)
        {
            int depth = 0;
            HeaderedContentControl current = stickyHeaderControl;
            while (current != null)
            {
                depth++;
                current = current.Content as HeaderedContentControl;
            }
            return depth;
        }

        private void UpdateStickyHeaderMargin(ScrollViewer scrollViewer, HeaderedContentControl stickyHeaderControl)
        {
            if (stickyHeaderControl == null || scrollViewer == null)
                return;

            double rightMargin = (scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
                ? SystemParameters.VerticalScrollBarWidth
                : 0.0;

            var margin = stickyHeaderControl.Margin;
            stickyHeaderControl.Margin = new Thickness(margin.Left, margin.Top, rightMargin, margin.Bottom);
        }
    }
}