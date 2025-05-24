using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace StickyScrollApp.Controls
{
    /// <summary>
    /// Stickyヘッダー機能付きのTreeViewコントロール
    /// </summary>
    public class StickyTreeView : TreeView
    {
        // スクロールビューとヘッダーパネルの参照
        private StickyScrollBehavior _stickyBehavior;　

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
            control?._stickyBehavior?.UpdateStickyHeaders();
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
            if (GetTemplateChild(StickyScrollBehavior.PART_ScrollViewer) is ScrollViewer scrollViewer &&
                GetTemplateChild(StickyScrollBehavior.PART_StickyHeaderControl) is HeaderedContentControl stickyHeaderControl)
            {
                scrollViewer.ScrollChanged += (sender, e) => _stickyBehavior.UpdateStickyHeaders();

                // StickyScrollBehaviorの初期化
                _stickyBehavior = new StickyScrollBehavior(
                    this,
                    () => scrollViewer,
                    () => stickyHeaderControl,
                    () => this.AllowStickyScroll,
                    () => this.StickyHeaderContentTemplate
                );

                _stickyBehavior.UpdateStickyHeaders();
            }
        }
    }
}

