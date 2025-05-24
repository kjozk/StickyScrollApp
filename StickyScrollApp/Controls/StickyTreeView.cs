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
    /// Stickyヘッダー機能付きのTreeViewコントロール
    /// </summary>
    public class StickyTreeView : TreeView
    {
        // テンプレートパーツ名の定数
        private const string PART_ScrollViewer = "PART_ScrollViewer";
        private const string PART_StickyHeaderPanel = "PART_StickyHeaderPanel";

        // スクロールビューとヘッダーパネルの参照
        private ScrollViewer _scrollViewer;
        private StackPanel _stickyHeaderPanel;
        private bool _isUpdatingStickyHeaders; // 再帰防止用フラグ

        /// <summary>
        /// Stickyスクロールの有効/無効を切り替える依存プロパティ
        /// </summary>
        public static readonly DependencyProperty AllowStickyScrollProperty =
            DependencyProperty.Register(
                nameof(AllowStickyScroll),
                typeof(bool),
                typeof(StickyTreeView),
                new PropertyMetadata(true, OnAllowStickyScrollChanged));

        /// <summary>
        /// Stickyヘッダー用のControlTemplateを指定する依存プロパティ
        /// </summary>
        public static readonly DependencyProperty StickyHeaderContentTemplateProperty =
            DependencyProperty.Register(
                nameof(StickyHeaderContentTemplate),
                typeof(ControlTemplate),
                typeof(StickyTreeView),
                new PropertyMetadata(null));

        /// <summary>
        /// AllowStickyScrollプロパティ
        /// </summary>
        public bool AllowStickyScroll
        {
            get => (bool)GetValue(AllowStickyScrollProperty);
            set => SetValue(AllowStickyScrollProperty, value);
        }

        /// <summary>
        /// StickyHeaderContentTemplateプロパティ
        /// </summary>
        public ControlTemplate StickyHeaderContentTemplate
        {
            get => (ControlTemplate)GetValue(StickyHeaderContentTemplateProperty);
            set => SetValue(StickyHeaderContentTemplateProperty, value);
        }

        /// <summary>
        /// AllowStickyScroll変更時のコールバック
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnAllowStickyScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as StickyTreeView;
            control?.UpdateStickyHeaders();
        }

        /// <summary>
        /// デフォルトスタイルの設定
        /// </summary>
        static StickyTreeView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StickyTreeView),
                new FrameworkPropertyMetadata(typeof(StickyTreeView)));
        }

        /// <summary>
        /// テンプレート適用時に呼び出されるメソッド
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // テンプレート適用時にパーツ取得とイベントフック
            _scrollViewer = GetTemplateChild(PART_ScrollViewer) as ScrollViewer;
            _stickyHeaderPanel = GetTemplateChild(PART_StickyHeaderPanel) as StackPanel;

            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
            else
            {
                Debug.WriteLine($"{PART_ScrollViewer} not found in template.");
            }

            if (_stickyHeaderPanel == null)
            {
                Debug.WriteLine($"{PART_StickyHeaderPanel} not found in template.");
            }
        }

        /// <summary>
        /// スクロールイベント発生時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingStickyHeaders) return; // 再帰防止
            UpdateStickyHeaders();
        }

        /// <summary>
        /// Stickyヘッダーの更新処理
        /// </summary>
        private void UpdateStickyHeaders()
        {
            if (_scrollViewer == null || _stickyHeaderPanel == null)
            {
                return;
            }

            if (!this.AllowStickyScroll)
            {
                _stickyHeaderPanel.Children.Clear();
                return;
            }

            try
            {
                _isUpdatingStickyHeaders = true; // 再帰防止開始

                // スクロールがトップならヘッダーを空にする
                if (_scrollViewer.VerticalOffset == 0)
                {
                    _stickyHeaderPanel.Children.Clear();
                    return;
                }

                // 一番上に表示されているTreeViewItemを取得
                TreeViewItem topItem = FindTopVisibleTreeViewItem();
                if (topItem == null)
                {
                    _isUpdatingStickyHeaders = false;
                    return;
                }

                // 祖先ノードを取得しヘッダーとして表示
                var ancestors = GetAncestors(topItem).ToList();

                // 既存のChildrenと比較し、同じなら何もしない
                if (IsSameHeader(ancestors))
                {
                    return;
                }
                
                _stickyHeaderPanel.Children.Clear();
                int depth = 1;
                foreach (var ancestor in ancestors)
                {
                    _stickyHeaderPanel.Children.Add(CreateHeaderElement(ancestor, depth));
                    depth++;
                }
            }
            finally
            {
                _isUpdatingStickyHeaders = false; // 再帰防止終了
            }
        }

        /// <summary>
        /// ancestorsと_stickyHeaderPanel.Childrenの内容が同じか判定する
        /// </summary>
        /// <param name="ancestors"></param>
        /// <returns></returns>
        private bool IsSameHeader(List<TreeViewItem> ancestors)
        {
            if (_stickyHeaderPanel.Children.Count != ancestors.Count)
                return false;

            for (int i = 0; i < ancestors.Count; i++)
            {
                var presenter = _stickyHeaderPanel.Children[i] as ContentPresenter;
                if (presenter == null || presenter.Content != ancestors[i].DataContext)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// スクロールビュー内で一番上に表示されているTreeViewItemを返す（上端に最も近いもの）
        /// StickyHeaderPanelの高さは、更新後の想定高さで見積もって判定する
        /// </summary>
        private TreeViewItem FindTopVisibleTreeViewItem()
        {
            if (_scrollViewer == null || _stickyHeaderPanel == null) return null;

            // 1. まず全ての候補TreeViewItemを列挙
            var visibleItems = new List<TreeViewItem>();
            FindVisibleTreeViewItemsRecursive(this, visibleItems);

            // 2. 祖先ノード数の最大値を取得
            int estimatedHeaderCount = GetMaxAncestorCount(visibleItems);

            // 3. 仮のStickyHeaderPanel高さを計算
            double estimatedStickyHeaderHeight = EstimateStickyHeaderHeight(estimatedHeaderCount);

            // 4. StickyHeaderPanelの高さを仮定して最上位のTreeViewItemを判定
            return FindTopItem(visibleItems, estimatedStickyHeaderHeight);

            // --- ローカルメソッド群 ---

            int GetMaxAncestorCount(List<TreeViewItem> items)
            {
                int max = 0;
                foreach (var tvi in items)
                {
                    int count = GetAncestors(tvi).Count();
                    if (count > max) max = count;
                }
                return max;
            }

            double EstimateStickyHeaderHeight(int headerCount)
            {
                double headerElementHeight = 0;
                if (_stickyHeaderPanel.Children.Count > 0 && _stickyHeaderPanel.Children[0] is FrameworkElement fe)
                    headerElementHeight = fe.ActualHeight;
                else
                    headerElementHeight = 24; // デフォルト値
                return headerElementHeight * headerCount;
            }

            TreeViewItem FindTopItem(List<TreeViewItem> items, double stickyHeaderHeight)
            {
                TreeViewItem topVisibleItem = null;
                double minDistance = double.MaxValue;

                foreach (var tvi in items)
                {
                    try
                    {
                        var transform = tvi.TransformToAncestor(_scrollViewer);
                        var point = transform.Transform(new Point(0, 0));
                        var itemRect = new Rect(point, tvi.RenderSize);

                        // 仮定したStickyHeaderPanelの下端より下に一部でも表示されているか
                        if (itemRect.Bottom > stickyHeaderHeight && itemRect.Top < _scrollViewer.ViewportHeight)
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
        }

        /// <summary>
        /// 実際に表示されているTreeViewItemを再帰的に列挙
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="accumulator"></param>
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

        /// <summary>
        /// 再帰的に一番上に表示されているTreeViewItemを探す
        /// </summary>
        /// <param name="tvi"></param>
        /// <returns></returns>
        private TreeViewItem FindTopVisibleTreeViewItemRecursive(TreeViewItem tvi)
        {
            if (tvi == null)
            {
                return null;
            }

            // TreeViewItemの境界をスクロールビュー内で取得
            if (IsElementPartiallyVisibleInScrollViewer(_scrollViewer, tvi))
            {
                return tvi;
            }

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
                    {
                        return found;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 要素がScrollViewer内で一部でも表示されているか判定
        /// </summary>
        /// <param name="sv"></param>
        /// <param name="element"></param>
        /// <returns></returns>
        private bool IsElementPartiallyVisibleInScrollViewer(ScrollViewer sv, FrameworkElement element)
        {
            if (element == null) return false;

            GeneralTransform childTransform = element.TransformToAncestor(sv);
            Rect childBounds = childTransform.TransformBounds(new Rect(new Point(0, 0), element.RenderSize));
            Rect scrollBounds = new Rect(new Point(0, 0), sv.RenderSize);

            // 完全に外に出ていなければ一部表示とみなす
            return childBounds.Bottom > 0 && childBounds.Top < scrollBounds.Height;
        }

        /// <summary>
        /// データからTreeViewItemを探す
        /// </summary>
        /// <param name="container"></param>
        /// <param {data"></param>
        /// <returns></returns>
        private TreeViewItem FindTreeViewItemForData(ItemsControl container, object data)
        {
            if (container == null)
            {
                return null;
            }

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
                {
                    continue;
                }

                if (item == data)
                {
                    return tvi;
                }

                var child = FindTreeViewItemForData(tvi, data);
                if (child != null)
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>
        /// 指定TreeViewItemの祖先を列挙
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
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

        /// <summary>
        /// ヘッダー用のUI要素を生成
        /// </summary>
        /// <param name="ancestor"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        private FrameworkElement CreateHeaderElement(TreeViewItem ancestor, int depth)
        {
            var data = ancestor.DataContext;
            double indent = 20 * depth;

            if (this.StickyHeaderContentTemplate != null)
            {
                var headered = new ContentControl
                {
                    Content = data,
                    Margin = new Thickness(indent, 0, 0, 0),
                    ContentTemplate = this.ItemTemplate,
                    ContentTemplateSelector = this.ItemTemplateSelector,
                    Template = this.StickyHeaderContentTemplate
                };
                return headered;
            }
            else
            {
                // 従来通り
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
}

