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
    /// TreeView等にStickyヘッダー機能を付与するビヘイビアクラス。
    /// スクロール位置に応じて、親ノードのヘッダーを上部に固定表示します。
    /// </summary>
    public class StickyScrollBehavior
    {
        // テンプレートパーツ名の定数
        public const string PART_ScrollViewer = "PART_ScrollViewer";
        public const string PART_StickyHeaderControl = "PART_StickyHeaderControl";

        // 対象となるTreeView
        private readonly TreeView _treeView;

        // ScrollViewer取得用デリゲート
        private readonly Func<ScrollViewer> _getScrollViewer;

        // Stickyヘッダー用コントロール取得用デリゲート
        private readonly Func<HeaderedContentControl> _getStickyHeaderControl;

        // Stickyスクロール有効判定用デリゲート
        private readonly Func<bool> _getAllowStickyScroll;

        // Stickyヘッダーのテンプレート取得用デリゲート
        private readonly Func<ControlTemplate> _getStickyHeaderContentTemplate;

        /// <summary>
        /// StickyScrollBehaviorのコンストラクタ
        /// </summary>
        /// <param name="treeView">対象TreeView</param>
        /// <param name="getScrollViewer">ScrollViewer取得デリゲート</param>
        /// <param name="getStickyHeaderControl">Stickyヘッダーコントロール取得デリゲート</param>
        /// <param name="getAllowStickyScroll">Stickyスクロール有効判定デリゲート</param>
        /// <param name="getStickyHeaderContentTemplate">Stickyヘッダー用テンプレート取得デリゲート</param>
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

        /// <summary>
        /// Stickyヘッダーの表示内容を更新する。
        /// スクロール位置や有効/無効設定に応じてヘッダーを切り替える。
        /// </summary>
        public void UpdateStickyHeaders()
        {
            var scrollViewer = _getScrollViewer();
            var stickyHeaderControl = _getStickyHeaderControl();
            if (scrollViewer == null || stickyHeaderControl == null)
            {
                return;
            }

            // Stickyヘッダーの幅をScrollViewerの表示領域に合わせる
            UpdateStickyHeaderWidth(scrollViewer, stickyHeaderControl);

            bool clear = false;

            try
            {
                // Stickyスクロールが無効ならヘッダーをクリア
                if (!_getAllowStickyScroll())
                {
                    clear = true;
                    return;
                }

                // スクロール位置が先頭ならヘッダーをクリア
                if (scrollViewer.VerticalOffset == 0)
                {
                    clear = true;
                    return;
                }

                // 現在最上部に表示されているTreeViewItemを取得
                TreeViewItem topItem = FindTopVisibleTreeViewItem(scrollViewer, stickyHeaderControl);
                if (topItem == null)
                {
                    clear = true;
                    return;
                }
                // ancestors: topItemの祖先ノード（ルートに近い順）
                List<TreeViewItem> ancestors = GetAncestors(topItem).ToList();
                if (!ancestors.Any())
                {
                    clear = true;
                    return;
                }

                // ヘッダーが変化していれば更新
                if (!IsSameHeader(stickyHeaderControl, ancestors))
                {
                    // 祖先ノードを順にHeaderedContentControlとしてネストして表示
                    HeaderedContentControl parent = stickyHeaderControl;
                    parent.Header = ancestors.First().DataContext;
                    parent.Content = null;

                    foreach (var ancestor in ancestors.Skip(1))
                    {
                        var child = new HeaderedContentControl
                        {
                            Header = ancestor.DataContext,
                            Template = _getStickyHeaderContentTemplate()
                        };
                        parent.Content = child;
                        parent = child;

                        // Stickyヘッダーの高さがScrollViewerの高さの半分を超えないようにする
                        if (stickyHeaderControl.ActualHeight > scrollViewer.ViewportHeight / 2)
                        {
                            parent.Content = null;
                            break;
                        }
                    }
                }
            }
            finally
            {
                // クリアフラグが立っていればヘッダーを消去
                if (clear && stickyHeaderControl != null)
                {
                    stickyHeaderControl.Header = null;
                    stickyHeaderControl.Content = null;
                }
            }
        }

        /// <summary>
        /// 現在のStickyヘッダーと、与えられた祖先ノードリストが一致しているか判定する。
        /// </summary>
        /// <param name="stickyHeaderControl">Stickyヘッダーコントロール</param>
        /// <param name="ancestors">祖先TreeViewItemリスト</param>
        /// <returns>一致していればtrue</returns>
        private static bool IsSameHeader(HeaderedContentControl stickyHeaderControl, List<TreeViewItem> ancestors)
        {
            var headers = new List<object>();
            HeaderedContentControl header = stickyHeaderControl;
            do
            {
                headers.Add(header.Header);
                header = header.Content as HeaderedContentControl;
            } while (header != null);

            if (headers.Count != ancestors.Count)
            {
                return false;
            }

            for (int i = 0; i < ancestors.Count; i++)
            {
                if (headers[i] != ancestors[i].DataContext)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// スクロール位置から最上部に見えているTreeViewItemを推定して返す。
        /// </summary>
        /// <param name="scrollViewer">対象ScrollViewer</param>
        /// <param name="stickyHeaderControl">Stickyヘッダーコントロール</param>
        /// <returns>最上部のTreeViewItem</returns>
        private TreeViewItem FindTopVisibleTreeViewItem(ScrollViewer scrollViewer, HeaderedContentControl stickyHeaderControl)
        {
            var visibleItems = new List<TreeViewItem>();
            // 現在表示中のTreeViewItemを再帰的に列挙
            FindVisibleTreeViewItemsRecursive(_treeView, visibleItems);

            // 祖先ノードの最大数を推定
            int estimatedHeaderCount = GetMaxAncestorCount(visibleItems);
            double estimatedStickyHeaderHeight = EstimateStickyHeaderHeight(stickyHeaderControl, estimatedHeaderCount);

            // Stickyヘッダーの高さを考慮して最上部アイテムを取得
            var topItem = FindTopItem(visibleItems, scrollViewer, estimatedStickyHeaderHeight, stickyHeaderControl);

            // 実際の祖先数でStickyヘッダー高さを再計算し、再度最上部アイテムを取得
            int ancestorCount = GetAncestors(topItem).Count();

            return FindTopItem(visibleItems, scrollViewer, EstimateStickyHeaderHeight(stickyHeaderControl, ancestorCount), stickyHeaderControl);
        }

        /// <summary>
        /// 指定リスト内で祖先ノード数の最大値を返す。
        /// </summary>
        /// <param name="items">TreeViewItemリスト</param>
        /// <returns>最大祖先数</returns>
        private static int GetMaxAncestorCount(List<TreeViewItem> items)
        {
            int max = 0;
            foreach (var tvi in items)
            {
                int count = GetAncestors(tvi).Count();
                if (count > max) max = count;
            }
            return max;
        }

        /// <summary>
        /// Stickyヘッダーの高さを推定する。
        /// </summary>
        /// <param name="stickyHeaderControl">Stickyヘッダーコントロール</param>
        /// <param name="headerCount">ヘッダー階層数</param>
        /// <returns>推定高さ</returns>
        private static double EstimateStickyHeaderHeight(HeaderedContentControl stickyHeaderControl, int headerCount)
        {
            int depth = GetStickyHeaderDepth(stickyHeaderControl);
            HeaderedContentControl current = stickyHeaderControl;
            // 既存のヘッダー深さから1段分の高さを推定
            double headerElementHeight = (depth > 0) ? (current.ActualHeight / depth) : 24;
            return headerElementHeight * headerCount;
        }

        /// <summary>
        /// Stickyヘッダーの高さを考慮して最上部のTreeViewItemを返す。
        /// </summary>
        /// <param name="items">TreeViewItemリスト</param>
        /// <param name="scrollViewer">ScrollViewer</param>
        /// <param name="stickyHeaderHeight">Stickyヘッダー高さ</param>
        /// <param name="stickyHeaderControl">Stickyヘッダーコントロール</param>
        /// <returns>最上部のTreeViewItem</returns>
        private static TreeViewItem FindTopItem(List<TreeViewItem> items, ScrollViewer scrollViewer, double stickyHeaderHeight, HeaderedContentControl stickyHeaderControl)
        {
            TreeViewItem topVisibleItem = null;
            double minDistance = double.MaxValue;

            foreach (var tvi in items)
            {
                try
                {
                    // TreeViewItemのスクロールビュー内での位置を取得
                    var transform = tvi.TransformToAncestor(scrollViewer);
                    var point = transform.Transform(new Point(0, 0));
                    var itemRect = new Rect(point, tvi.RenderSize);

                    // Stickyヘッダーの下に見えているアイテムを探す
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
                    // ビジュアルツリーが壊れている場合はスキップ
                    continue;
                }
            }
            return topVisibleItem;
        }

        /// <summary>
        /// 再帰的にTreeViewItemを列挙し、表示中のものをリストに追加する。
        /// </summary>
        /// <param name="parent">親ItemsControl</param>
        /// <param name="accumulator">TreeViewItemの蓄積リスト</param>
        private static void FindVisibleTreeViewItemsRecursive(ItemsControl parent, List<TreeViewItem> accumulator)
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

        /// <summary>
        /// 指定TreeViewItemの祖先ノードをルートに近い順で列挙する。
        /// </summary>
        /// <param name="item">TreeViewItem</param>
        /// <returns>祖先TreeViewItem列挙</returns>
        private static IEnumerable<TreeViewItem> GetAncestors(TreeViewItem item)
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
        /// Stickyヘッダーのネスト深さを取得する。
        /// </summary>
        /// <param name="stickyHeaderControl">Stickyヘッダーコントロール</param>
        /// <returns>深さ</returns>
        private static int GetStickyHeaderDepth(HeaderedContentControl stickyHeaderControl)
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

        /// <summary>
        /// Stickyヘッダーの幅をScrollViewerの表示領域幅に合わせる。
        /// </summary>
        /// <param name="scrollViewer">ScrollViewer</param>
        /// <param name="stickyHeaderControl">Stickyヘッダーコントロール</param>
        private void UpdateStickyHeaderWidth(ScrollViewer scrollViewer, HeaderedContentControl stickyHeaderControl)
        {
            if (stickyHeaderControl == null || scrollViewer == null)
            {
                return;
            }
            stickyHeaderControl.Width = scrollViewer.ViewportWidth;
        }
    }
}