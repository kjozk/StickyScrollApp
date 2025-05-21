using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StickyScrollApp.Controls
{
    public class StickyTreeView : TreeView
    {
        private ScrollViewer _scrollViewer;
        private StackPanel _stickyHeaderPanel;
        private bool _isUpdatingStickyHeaders; // 再帰防止用フラグ

        static StickyTreeView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StickyTreeView),
                new FrameworkPropertyMetadata(typeof(StickyTreeView)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
            _stickyHeaderPanel = GetTemplateChild("PART_StickyHeaderPanel") as StackPanel;

            if (_scrollViewer != null)
                _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingStickyHeaders) return; // 再帰防止
            UpdateStickyHeaders();
        }

        private void UpdateStickyHeaders()
        {
            if (_scrollViewer == null || _stickyHeaderPanel == null)
                return;

            try
            {
                _isUpdatingStickyHeaders = true; // 再帰防止開始

                // スクロールがトップならヘッダーを空にする
                if (_scrollViewer.VerticalOffset == 0)
                {
                    _stickyHeaderPanel.Children.Clear();
                    return;
                }

                // スクロールビュー内で一番上に表示されているTreeViewItemを取得
                TreeViewItem topItem = FindTopVisibleTreeViewItem();
                if (topItem == null)
                {
                    _isUpdatingStickyHeaders = false;
                    return;
                }

                var ancestors = GetAncestors(topItem);

                _stickyHeaderPanel.Children.Clear();
                int depth = 1;
                foreach (var ancestor in ancestors)
                {
                    _stickyHeaderPanel.Children.Add(CreateHeaderElement(ancestor, depth));
                    depth++;
                }

                //_scrollViewer.Padding = new Thickness
                //{
                //    Top = _stickyHeaderPanel.ActualHeight,
                //    Bottom = - _stickyHeaderPanel.ActualHeight,
                //};
            }
            finally
            {
                _isUpdatingStickyHeaders = false; // 再帰防止終了
            }
        }

        /// <summary>
        /// スクロールビュー内で一番上に表示されているTreeViewItemを返す（上端に最も近いもの）
        /// </summary>
        private TreeViewItem FindTopVisibleTreeViewItem()
        {
            if (_scrollViewer == null || _stickyHeaderPanel == null) return null;

            var visibleItems = new List<TreeViewItem>();
            FindVisibleTreeViewItemsRecursive(this, visibleItems);

            TreeViewItem topVisibleItem = null;
            double minDistance = double.MaxValue;

            // _stickyHeaderPanelの高さを取得
            double stickyHeaderHeight = _stickyHeaderPanel.ActualHeight;

            foreach (var tvi in visibleItems)
            {
                try
                {
                    // TreeViewItemの上端座標をScrollViewer基準で取得
                    var transform = tvi.TransformToAncestor(_scrollViewer);
                    var point = transform.Transform(new Point(0, 0));

                    // 上端がScrollViewerの表示領域内、または上端より上（負の値）でも下端が表示領域内なら候補
                    if (point.Y < _scrollViewer.ViewportHeight && point.Y + tvi.ActualHeight > 0)
                    {
                        // _stickyHeaderPanelと重なっていないか判定
                        if (point.Y + tvi.ActualHeight <= stickyHeaderHeight)
                        {
                            // 完全にStickyHeaderPanelの裏に隠れているのでスキップ
                            continue;
                        }

                        // StickyHeaderPanelの下端より下にある最上位の要素を選ぶ
                        double distance = Math.Abs(point.Y - stickyHeaderHeight);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            topVisibleItem = tvi;
                        }
                        // 上端がStickyHeaderPanelの下端にぴったりなら即返す
                        if (point.Y == stickyHeaderHeight)
                            return tvi;
                    }
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
            }
            return topVisibleItem;
        }

        // 実際に表示されている (UI要素として存在している) TreeViewItemのみを列挙するヘルパー
        private void FindVisibleTreeViewItemsRecursive(ItemsControl parent, List<TreeViewItem> accumulator)
        {
            if (parent == null) return;

            foreach (var item in parent.Items)
            {
                // ItemContainerGenerator.ContainerFromItem は、生成済みのコンテナがあれば返す
                if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi && tvi.IsVisible) // IsVisibleもチェック
                {
                    accumulator.Add(tvi);
                    if (tvi.IsExpanded && tvi.Items.Count > 0)
                    {
                        FindVisibleTreeViewItemsRecursive(tvi, accumulator);
                    }
                }
            }
        }

        private TreeViewItem FindTopVisibleTreeViewItemRecursive(TreeViewItem tvi)
        {
            if (tvi == null)
                return null;

            // TreeViewItemの境界をスクロールビュー内で取得
            if (IsElementPartiallyVisibleInScrollViewer(_scrollViewer, tvi))
                return tvi;

            // 展開されている場合は子も調べる
            if (tvi.IsExpanded && tvi.Items.Count > 0)
            {
                for (int i = 0; i < tvi.Items.Count; i++)
                {
                    var childTvi = tvi.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                    if (childTvi == null)
                    {
                        tvi.UpdateLayout();
                        childTvi = tvi.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                    }
                    var found = FindTopVisibleTreeViewItemRecursive(childTvi);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        /// <summary>
        /// 要素がScrollViewer内で一部でも表示されているか判定
        /// </summary>
        private bool IsElementPartiallyVisibleInScrollViewer(ScrollViewer sv, FrameworkElement element)
        {
            if (element == null) return false;

            GeneralTransform childTransform = element.TransformToAncestor(sv);
            Rect childBounds = childTransform.TransformBounds(new Rect(new Point(0, 0), element.RenderSize));
            Rect scrollBounds = new Rect(new Point(0, 0), sv.RenderSize);

            // 完全に外に出ていなければ一部表示とみなす
            return childBounds.Bottom > 0 && childBounds.Top < scrollBounds.Height;
        }

        private TreeViewItem FindTreeViewItemForData(ItemsControl container, object data)
        {
            if (container == null)
                return null;

            for (int i = 0; i < container.Items.Count; i++)
            {
                var item = container.Items[i];
                var tvi = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (tvi == null)
                {
                    container.UpdateLayout();
                    tvi = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                }
                if (tvi == null)
                    continue;

                if (item == data)
                    return tvi;

                var child = FindTreeViewItemForData(tvi, data);
                if (child != null)
                    return child;
            }
            return null;
        }

        private IEnumerable<TreeViewItem> GetAncestors(TreeViewItem item)
        {
            var list = new List<TreeViewItem>();
            DependencyObject current = VisualTreeHelper.GetParent(item);
            while (current != null)
            {
                if (current is TreeViewItem tvi)
                    list.Insert(0, tvi);
                current = VisualTreeHelper.GetParent(current);
            }
            return list;
        }

        private FrameworkElement CreateHeaderElement(TreeViewItem ancestor, int depth)
        {
            var data = ancestor.DataContext;
            double indent = 20 * depth;

            return new ContentPresenter
            {
                Content = data,
                Margin = new Thickness(indent, 0, 0, 0),
                ContentTemplate = this.ItemTemplate,
                ContentTemplateSelector = this.ItemTemplateSelector
            };
        }
    }
}
