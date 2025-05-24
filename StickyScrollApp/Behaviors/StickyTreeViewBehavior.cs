using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace StickyScrollApp.Controls
{
    /// <summary>
    /// TreeViewにStickyヘッダー機能を付与するBehavior。
    /// スクロール時に親ノードのヘッダーをTreeView上部に固定表示します。
    /// </summary>
    /// <remarks>
    /// このBehaviorは、<c>PART_ScrollViewer</c> および <c>PART_StickyHeaderControl</c> という
    /// テンプレートパーツを持つ <see cref="TreeView"/> の <see cref="ControlTemplate"/> でのみ動作します。
    /// これらのパーツが存在しない場合、Stickyヘッダー機能は動作しません。
    /// </remarks>
    public class StickyTreeViewBehavior : Behavior<TreeView>
    {
        // テンプレート内で参照するScrollViewerのパーツ名
        public const string PART_ScrollViewer = "PART_ScrollViewer";

        // テンプレート内で参照するStickyヘッダー用HeaderedContentControlのパーツ名
        public const string PART_StickyHeaderControl = "PART_StickyHeaderControl";

        // ヘッダー高さのデフォルト値
        private const double DefaultHeaderElementHeight = 24.0;

        // Stickyヘッダーの最大高さ比率（ScrollViewer高さの50%まで）
        private const double StickyHeaderMaxHeightRatio = 0.5;

        // スクロールビュー
        private ScrollViewer _scrollViewer;

        // Stickyヘッダー
        private HeaderedContentControl _stickyHeaderControl;

        // Stickyスクロールの有効/無効を切り替える依存プロパティ
        public static readonly DependencyProperty AllowStickyScrollProperty =
            DependencyProperty.Register(
                nameof(AllowStickyScroll),
                typeof(bool),
                typeof(StickyTreeViewBehavior),
                new PropertyMetadata(true, OnAllowStickyScrollChanged));

        /// <summary>
        /// Stickyスクロールの有効/無効
        /// </summary>
        public bool AllowStickyScroll
        {
            get => (bool)GetValue(AllowStickyScrollProperty);
            set => SetValue(AllowStickyScrollProperty, value);
        }

        // Stickyヘッダー用のControlTemplateを指定する依存プロパティ
        public static readonly DependencyProperty StickyHeaderContentTemplateProperty =
            DependencyProperty.Register(
                nameof(StickyHeaderContentTemplate),
                typeof(ControlTemplate),
                typeof(StickyTreeViewBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Stickyヘッダーの表示に使うControlTemplate
        /// </summary>
        public ControlTemplate StickyHeaderContentTemplate
        {
            get => (ControlTemplate)GetValue(StickyHeaderContentTemplateProperty);
            set => SetValue(StickyHeaderContentTemplateProperty, value);
        }

        /// <summary>
        /// BehaviorがTreeViewにアタッチされたときの初期化処理
        /// </summary>
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        /// <summary>
        /// Behaviorがデタッチされたときのクリーンアップ処理
        /// </summary>
        protected override void OnDetaching()
        {
            base.OnDetaching();
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            }
            AssociatedObject.Loaded -= AssociatedObject_Loaded;
        }

        /// <summary>
        /// TreeViewのLoaded時にテンプレートパーツを取得し、イベントをフック
        /// </summary>
        /// <param name="sender">イベント発生元（TreeView）</param>
        /// <param name="e">イベント引数</param>
        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = AssociatedObject.Template.FindName(PART_ScrollViewer, AssociatedObject) as ScrollViewer;
            _stickyHeaderControl = AssociatedObject.Template.FindName(PART_StickyHeaderControl, AssociatedObject) as HeaderedContentControl;

            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
            UpdateStickyHeaders();
        }

        /// <summary>
        /// AllowStickyScrollプロパティ変更時のコールバック
        /// </summary>
        /// <param name="d">依存プロパティの所有オブジェクト</param>
        /// <param name="e">プロパティ変更イベント引数</param>
        private static void OnAllowStickyScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as StickyTreeViewBehavior;
            behavior?.UpdateStickyHeaders();
        }

        /// <summary>
        /// スクロール時にStickyヘッダーを更新
        /// </summary>
        /// <param name="sender">イベント発生元（ScrollViewer）</param>
        /// <param name="e">スクロールイベント引数</param>
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateStickyHeaders();
        }

        /// <summary>
        /// Stickyヘッダーの表示内容を更新する。
        /// スクロール位置やTreeViewの状態に応じてStickyヘッダーを再構築します。
        /// </summary>
        public void UpdateStickyHeaders()
        {
            if (_scrollViewer == null || _stickyHeaderControl == null)
            {
                return;
            }

            UpdateStickyHeaderWidth(_scrollViewer, _stickyHeaderControl);

            bool clear = true;

            try
            {
                if (!AllowStickyScroll)
                {
                    return;
                }

                if (_scrollViewer.VerticalOffset == 0)
                {
                    return;
                }

                TreeViewItem topItem = FindTopVisibleTreeViewItem(_scrollViewer, _stickyHeaderControl);
                if (topItem == null)
                {
                    return;
                }

                List<object> ancestors = GetAncestors(topItem).Reverse().Select(a => a.DataContext).ToList();
                if (!ancestors.Any())
                {
                    return;
                }

                clear = false;

                // 祖先リストが現在のStickyヘッダーと異なる場合のみ再構築
                if (!IsSameHeaderHierarchy(_stickyHeaderControl, ancestors))
                {
                    try
                    {
                        RebuildStickyHeaderHierarchy(
                            _stickyHeaderControl,
                            ancestors,
                            StickyHeaderContentTemplate,
                            _scrollViewer.ViewportHeight * StickyHeaderMaxHeightRatio
                        );
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"StickyHeader構築中に例外: {ex}");
                        clear = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StickyHeader更新中に例外: {ex}");
                clear = true;
            }
            finally
            {
                if (clear && _stickyHeaderControl != null)
                {
                    _stickyHeaderControl.Header = null;
                    _stickyHeaderControl.Content = null;
                }
            }
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

        /// <summary>
        /// Stickyヘッダーの階層を再構築します。
        /// 祖先ノードをHeaderedContentControlとしてネストして表示します。
        /// </summary>
        /// <param name="root">StickyヘッダーのルートとなるHeaderedContentControl</param>
        /// <param name="ancestors">ルートに近い順の祖先データリスト（空不可）</param>
        /// <param name="template">各階層で使用するControlTemplate</param>
        /// <param name="maxHeight">Stickyヘッダー全体の最大高さ（px）</param>
        private static void RebuildStickyHeaderHierarchy(
            HeaderedContentControl root,
            IReadOnlyList<object> ancestors,
            ControlTemplate template,
            double maxHeight)
        {
            if (ancestors == null || ancestors.Count == 0) return;

            // 祖先ノードを順にHeaderedContentControlとしてネストして表示
            HeaderedContentControl parent = root;
            parent.Header = ancestors.First();
            parent.Content = null;

            foreach (var ancestor in ancestors.Skip(1))
            {
                var child = new HeaderedContentControl { Template = template, Header = ancestor };
                parent.Content = child;
                parent = child;

                // Stickyヘッダーの高さがScrollViewerの高さの半分を超えないようにする
                if (root.ActualHeight > maxHeight)
                {
                    parent.Content = null;
                    break;
                }
            }
        }

        /// <summary>
        /// 現在のStickyヘッダーと、与えられた祖先ノードリストが一致しているか判定する。
        /// </summary>
        /// <param name="stickyHeaderControl">Stickyヘッダーコントロール</param>
        /// <param name="ancestors">祖先TreeViewItemリスト</param>
        /// <returns>一致していればtrue</returns>
        private static bool IsSameHeaderHierarchy(HeaderedContentControl stickyHeaderControl, List<object> ancestors)
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
                if (!object.Equals(headers[i], ancestors[i]))
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
            // 現在表示中のTreeViewItemを再帰的に列挙
            var visibleItems = FindVisibleTreeViewItems(AssociatedObject).ToList();

            // Stickyヘッダーの高さを推定
            int estimatedHeaderCount = GetMaxAncestorCount(visibleItems);
            double estimatedStickyHeaderHeight = EstimateStickyHeaderHeight(stickyHeaderControl, estimatedHeaderCount);

            // Stickyヘッダーの高さを考慮して最上部アイテムを取得
            var topItem = FindTopItem(visibleItems, scrollViewer, estimatedStickyHeaderHeight);

            // 実際の祖先数でStickyヘッダー高さを再計算し、再度最上部アイテムを取得
            int ancestorCount = GetAncestors(topItem).Count();
            return FindTopItem(visibleItems, scrollViewer, EstimateStickyHeaderHeight(stickyHeaderControl, ancestorCount));
        }

        /// <summary>
        /// 指定リスト内で祖先ノード数の最大値を返す。
        /// </summary>
        /// <param name="items">TreeViewItemのリスト</param>
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
        /// <param name="headerCount">ヘッダー階層の数</param>
        /// <returns>推定されるStickyヘッダーの高さ（px）</returns>
        private static double EstimateStickyHeaderHeight(HeaderedContentControl stickyHeaderControl, int headerCount)
        {
            int depth = GetStickyHeaderDepth(stickyHeaderControl);
            HeaderedContentControl current = stickyHeaderControl;
            // 既存のヘッダー深さから1段分の高さを推定
            double headerElementHeight = (depth > 0) ? (current.ActualHeight / depth) : DefaultHeaderElementHeight;
            return headerElementHeight * headerCount;
        }

        /// <summary>
        /// Stickyヘッダーの高さを考慮して最上部のTreeViewItemを返す。
        /// </summary>
        /// <param name="items">TreeViewItemのリスト</param>
        /// <param name="scrollViewer">対象ScrollViewer</param>
        /// <param name="stickyHeaderHeight">Stickyヘッダーの高さ（px）</param>
        /// <returns>最上部のTreeViewItem</returns>
        private static TreeViewItem FindTopItem(List<TreeViewItem> items, ScrollViewer scrollViewer, double stickyHeaderHeight)
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
        /// 再帰的にTreeViewItemを列挙し、表示中のものを返す。
        /// </summary>
        /// <param name="parent">親ItemsControl</param>
        /// <returns>表示中のTreeViewItem列挙</returns>
        private static IEnumerable<TreeViewItem> FindVisibleTreeViewItems(ItemsControl parent)
        {
            if (parent == null) yield break;

            foreach (var item in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi && tvi.IsVisible)
                {
                    yield return tvi;
                    if (tvi.IsExpanded && tvi.Items.Count > 0)
                    {
                        foreach (var child in FindVisibleTreeViewItems(tvi))
                        {
                            yield return child;
                        }
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
            if (item == null) yield break;
            DependencyObject current = VisualTreeHelper.GetParent(item);
            while (current != null)
            {
                if (current is TreeViewItem tvi)
                {
                    yield return tvi;
                }
                current = VisualTreeHelper.GetParent(current);
            }
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
    }
}