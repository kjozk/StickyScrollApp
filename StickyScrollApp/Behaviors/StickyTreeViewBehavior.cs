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
    /// TreeView��Sticky�w�b�_�[�@�\��t�^����Behavior�B
    /// �X�N���[�����ɐe�m�[�h�̃w�b�_�[��TreeView�㕔�ɌŒ�\�����܂��B
    /// </summary>
    /// <remarks>
    /// ����Behavior�́A<c>PART_ScrollViewer</c> ����� <c>PART_StickyHeaderControl</c> �Ƃ���
    /// �e���v���[�g�p�[�c������ <see cref="TreeView"/> �� <see cref="ControlTemplate"/> �ł̂ݓ��삵�܂��B
    /// �����̃p�[�c�����݂��Ȃ��ꍇ�ASticky�w�b�_�[�@�\�͓��삵�܂���B
    /// </remarks>
    public class StickyTreeViewBehavior : Behavior<TreeView>
    {
        // �e���v���[�g���ŎQ�Ƃ���ScrollViewer�̃p�[�c��
        public const string PART_ScrollViewer = "PART_ScrollViewer";

        // �e���v���[�g���ŎQ�Ƃ���Sticky�w�b�_�[�pHeaderedContentControl�̃p�[�c��
        public const string PART_StickyHeaderControl = "PART_StickyHeaderControl";

        // �w�b�_�[�����̃f�t�H���g�l
        private const double DefaultHeaderElementHeight = 24.0;

        // Sticky�w�b�_�[�̍ő卂���䗦�iScrollViewer������50%�܂Łj
        private const double StickyHeaderMaxHeightRatio = 0.5;

        // �X�N���[���r���[
        private ScrollViewer _scrollViewer;

        // Sticky�w�b�_�[
        private HeaderedContentControl _stickyHeaderControl;

        // Sticky�X�N���[���̗L��/������؂�ւ���ˑ��v���p�e�B
        public static readonly DependencyProperty AllowStickyScrollProperty =
            DependencyProperty.Register(
                nameof(AllowStickyScroll),
                typeof(bool),
                typeof(StickyTreeViewBehavior),
                new PropertyMetadata(true, OnAllowStickyScrollChanged));

        /// <summary>
        /// Sticky�X�N���[���̗L��/����
        /// </summary>
        public bool AllowStickyScroll
        {
            get => (bool)GetValue(AllowStickyScrollProperty);
            set => SetValue(AllowStickyScrollProperty, value);
        }

        // Sticky�w�b�_�[�p��ControlTemplate���w�肷��ˑ��v���p�e�B
        public static readonly DependencyProperty StickyHeaderContentTemplateProperty =
            DependencyProperty.Register(
                nameof(StickyHeaderContentTemplate),
                typeof(ControlTemplate),
                typeof(StickyTreeViewBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Sticky�w�b�_�[�̕\���Ɏg��ControlTemplate
        /// </summary>
        public ControlTemplate StickyHeaderContentTemplate
        {
            get => (ControlTemplate)GetValue(StickyHeaderContentTemplateProperty);
            set => SetValue(StickyHeaderContentTemplateProperty, value);
        }

        /// <summary>
        /// Behavior��TreeView�ɃA�^�b�`���ꂽ�Ƃ��̏���������
        /// </summary>
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        /// <summary>
        /// Behavior���f�^�b�`���ꂽ�Ƃ��̃N���[���A�b�v����
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
        /// TreeView��Loaded���Ƀe���v���[�g�p�[�c���擾���A�C�x���g���t�b�N
        /// </summary>
        /// <param name="sender">�C�x���g�������iTreeView�j</param>
        /// <param name="e">�C�x���g����</param>
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
        /// AllowStickyScroll�v���p�e�B�ύX���̃R�[���o�b�N
        /// </summary>
        /// <param name="d">�ˑ��v���p�e�B�̏��L�I�u�W�F�N�g</param>
        /// <param name="e">�v���p�e�B�ύX�C�x���g����</param>
        private static void OnAllowStickyScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as StickyTreeViewBehavior;
            behavior?.UpdateStickyHeaders();
        }

        /// <summary>
        /// �X�N���[������Sticky�w�b�_�[���X�V
        /// </summary>
        /// <param name="sender">�C�x���g�������iScrollViewer�j</param>
        /// <param name="e">�X�N���[���C�x���g����</param>
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateStickyHeaders();
        }

        /// <summary>
        /// Sticky�w�b�_�[�̕\�����e���X�V����B
        /// �X�N���[���ʒu��TreeView�̏�Ԃɉ�����Sticky�w�b�_�[���č\�z���܂��B
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

                // �c�惊�X�g�����݂�Sticky�w�b�_�[�ƈقȂ�ꍇ�̂ݍč\�z
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
                        Debug.WriteLine($"StickyHeader�\�z���ɗ�O: {ex}");
                        clear = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StickyHeader�X�V���ɗ�O: {ex}");
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
        /// Sticky�w�b�_�[�̕���ScrollViewer�̕\���̈敝�ɍ��킹��B
        /// </summary>
        /// <param name="scrollViewer">ScrollViewer</param>
        /// <param name="stickyHeaderControl">Sticky�w�b�_�[�R���g���[��</param>
        private void UpdateStickyHeaderWidth(ScrollViewer scrollViewer, HeaderedContentControl stickyHeaderControl)
        {
            if (stickyHeaderControl == null || scrollViewer == null)
            {
                return;
            }
            stickyHeaderControl.Width = scrollViewer.ViewportWidth;
        }

        /// <summary>
        /// Sticky�w�b�_�[�̊K�w���č\�z���܂��B
        /// �c��m�[�h��HeaderedContentControl�Ƃ��ăl�X�g���ĕ\�����܂��B
        /// </summary>
        /// <param name="root">Sticky�w�b�_�[�̃��[�g�ƂȂ�HeaderedContentControl</param>
        /// <param name="ancestors">���[�g�ɋ߂����̑c��f�[�^���X�g�i��s�j</param>
        /// <param name="template">�e�K�w�Ŏg�p����ControlTemplate</param>
        /// <param name="maxHeight">Sticky�w�b�_�[�S�̂̍ő卂���ipx�j</param>
        private static void RebuildStickyHeaderHierarchy(
            HeaderedContentControl root,
            IReadOnlyList<object> ancestors,
            ControlTemplate template,
            double maxHeight)
        {
            if (ancestors == null || ancestors.Count == 0) return;

            // �c��m�[�h������HeaderedContentControl�Ƃ��ăl�X�g���ĕ\��
            HeaderedContentControl parent = root;
            parent.Header = ancestors.First();
            parent.Content = null;

            foreach (var ancestor in ancestors.Skip(1))
            {
                var child = new HeaderedContentControl { Template = template, Header = ancestor };
                parent.Content = child;
                parent = child;

                // Sticky�w�b�_�[�̍�����ScrollViewer�̍����̔����𒴂��Ȃ��悤�ɂ���
                if (root.ActualHeight > maxHeight)
                {
                    parent.Content = null;
                    break;
                }
            }
        }

        /// <summary>
        /// ���݂�Sticky�w�b�_�[�ƁA�^����ꂽ�c��m�[�h���X�g����v���Ă��邩���肷��B
        /// </summary>
        /// <param name="stickyHeaderControl">Sticky�w�b�_�[�R���g���[��</param>
        /// <param name="ancestors">�c��TreeViewItem���X�g</param>
        /// <returns>��v���Ă����true</returns>
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
        /// �X�N���[���ʒu����ŏ㕔�Ɍ����Ă���TreeViewItem�𐄒肵�ĕԂ��B
        /// </summary>
        /// <param name="scrollViewer">�Ώ�ScrollViewer</param>
        /// <param name="stickyHeaderControl">Sticky�w�b�_�[�R���g���[��</param>
        /// <returns>�ŏ㕔��TreeViewItem</returns>
        private TreeViewItem FindTopVisibleTreeViewItem(ScrollViewer scrollViewer, HeaderedContentControl stickyHeaderControl)
        {
            // ���ݕ\������TreeViewItem���ċA�I�ɗ�
            var visibleItems = FindVisibleTreeViewItems(AssociatedObject).ToList();

            // Sticky�w�b�_�[�̍����𐄒�
            int estimatedHeaderCount = GetMaxAncestorCount(visibleItems);
            double estimatedStickyHeaderHeight = EstimateStickyHeaderHeight(stickyHeaderControl, estimatedHeaderCount);

            // Sticky�w�b�_�[�̍������l�����čŏ㕔�A�C�e�����擾
            var topItem = FindTopItem(visibleItems, scrollViewer, estimatedStickyHeaderHeight);

            // ���ۂ̑c�搔��Sticky�w�b�_�[�������Čv�Z���A�ēx�ŏ㕔�A�C�e�����擾
            int ancestorCount = GetAncestors(topItem).Count();
            return FindTopItem(visibleItems, scrollViewer, EstimateStickyHeaderHeight(stickyHeaderControl, ancestorCount));
        }

        /// <summary>
        /// �w�胊�X�g���őc��m�[�h���̍ő�l��Ԃ��B
        /// </summary>
        /// <param name="items">TreeViewItem�̃��X�g</param>
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
        /// Sticky�w�b�_�[�̍����𐄒肷��B
        /// </summary>
        /// <param name="stickyHeaderControl">Sticky�w�b�_�[�R���g���[��</param>
        /// <param name="headerCount">�w�b�_�[�K�w�̐�</param>
        /// <returns>���肳���Sticky�w�b�_�[�̍����ipx�j</returns>
        private static double EstimateStickyHeaderHeight(HeaderedContentControl stickyHeaderControl, int headerCount)
        {
            int depth = GetStickyHeaderDepth(stickyHeaderControl);
            HeaderedContentControl current = stickyHeaderControl;
            // �����̃w�b�_�[�[������1�i���̍����𐄒�
            double headerElementHeight = (depth > 0) ? (current.ActualHeight / depth) : DefaultHeaderElementHeight;
            return headerElementHeight * headerCount;
        }

        /// <summary>
        /// Sticky�w�b�_�[�̍������l�����čŏ㕔��TreeViewItem��Ԃ��B
        /// </summary>
        /// <param name="items">TreeViewItem�̃��X�g</param>
        /// <param name="scrollViewer">�Ώ�ScrollViewer</param>
        /// <param name="stickyHeaderHeight">Sticky�w�b�_�[�̍����ipx�j</param>
        /// <returns>�ŏ㕔��TreeViewItem</returns>
        private static TreeViewItem FindTopItem(List<TreeViewItem> items, ScrollViewer scrollViewer, double stickyHeaderHeight)
        {
            TreeViewItem topVisibleItem = null;
            double minDistance = double.MaxValue;

            foreach (var tvi in items)
            {
                try
                {
                    // TreeViewItem�̃X�N���[���r���[���ł̈ʒu���擾
                    var transform = tvi.TransformToAncestor(scrollViewer);
                    var point = transform.Transform(new Point(0, 0));
                    var itemRect = new Rect(point, tvi.RenderSize);

                    // Sticky�w�b�_�[�̉��Ɍ����Ă���A�C�e����T��
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
                    // �r�W���A���c���[�����Ă���ꍇ�̓X�L�b�v
                    continue;
                }
            }
            return topVisibleItem;
        }

        /// <summary>
        /// �ċA�I��TreeViewItem��񋓂��A�\�����̂��̂�Ԃ��B
        /// </summary>
        /// <param name="parent">�eItemsControl</param>
        /// <returns>�\������TreeViewItem��</returns>
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
        /// �w��TreeViewItem�̑c��m�[�h�����[�g�ɋ߂����ŗ񋓂���B
        /// </summary>
        /// <param name="item">TreeViewItem</param>
        /// <returns>�c��TreeViewItem��</returns>
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
        /// Sticky�w�b�_�[�̃l�X�g�[�����擾����B
        /// </summary>
        /// <param name="stickyHeaderControl">Sticky�w�b�_�[�R���g���[��</param>
        /// <returns>�[��</returns>
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