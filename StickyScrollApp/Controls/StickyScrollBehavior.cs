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
    /// TreeView����Sticky�w�b�_�[�@�\��t�^����r�w�C�r�A�N���X�B
    /// �X�N���[���ʒu�ɉ����āA�e�m�[�h�̃w�b�_�[���㕔�ɌŒ�\�����܂��B
    /// </summary>
    public class StickyScrollBehavior
    {
        // �e���v���[�g�p�[�c���̒萔
        public const string PART_ScrollViewer = "PART_ScrollViewer";
        public const string PART_StickyHeaderControl = "PART_StickyHeaderControl";

        // �ΏۂƂȂ�TreeView
        private readonly TreeView _treeView;

        // ScrollViewer�擾�p�f���Q�[�g
        private readonly Func<ScrollViewer> _getScrollViewer;

        // Sticky�w�b�_�[�p�R���g���[���擾�p�f���Q�[�g
        private readonly Func<HeaderedContentControl> _getStickyHeaderControl;

        // Sticky�X�N���[���L������p�f���Q�[�g
        private readonly Func<bool> _getAllowStickyScroll;

        // Sticky�w�b�_�[�̃e���v���[�g�擾�p�f���Q�[�g
        private readonly Func<ControlTemplate> _getStickyHeaderContentTemplate;

        /// <summary>
        /// StickyScrollBehavior�̃R���X�g���N�^
        /// </summary>
        /// <param name="treeView">�Ώ�TreeView</param>
        /// <param name="getScrollViewer">ScrollViewer�擾�f���Q�[�g</param>
        /// <param name="getStickyHeaderControl">Sticky�w�b�_�[�R���g���[���擾�f���Q�[�g</param>
        /// <param name="getAllowStickyScroll">Sticky�X�N���[���L������f���Q�[�g</param>
        /// <param name="getStickyHeaderContentTemplate">Sticky�w�b�_�[�p�e���v���[�g�擾�f���Q�[�g</param>
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
        /// Sticky�w�b�_�[�̕\�����e���X�V����B
        /// �X�N���[���ʒu��L��/�����ݒ�ɉ����ăw�b�_�[��؂�ւ���B
        /// </summary>
        public void UpdateStickyHeaders()
        {
            var scrollViewer = _getScrollViewer();
            var stickyHeaderControl = _getStickyHeaderControl();
            if (scrollViewer == null || stickyHeaderControl == null)
            {
                return;
            }

            // Sticky�w�b�_�[�̕���ScrollViewer�̕\���̈�ɍ��킹��
            UpdateStickyHeaderWidth(scrollViewer, stickyHeaderControl);

            bool clear = false;

            try
            {
                // Sticky�X�N���[���������Ȃ�w�b�_�[���N���A
                if (!_getAllowStickyScroll())
                {
                    clear = true;
                    return;
                }

                // �X�N���[���ʒu���擪�Ȃ�w�b�_�[���N���A
                if (scrollViewer.VerticalOffset == 0)
                {
                    clear = true;
                    return;
                }

                // ���ݍŏ㕔�ɕ\������Ă���TreeViewItem���擾
                TreeViewItem topItem = FindTopVisibleTreeViewItem(scrollViewer, stickyHeaderControl);
                if (topItem == null)
                {
                    clear = true;
                    return;
                }
                // ancestors: topItem�̑c��m�[�h�i���[�g�ɋ߂����j
                List<TreeViewItem> ancestors = GetAncestors(topItem).ToList();
                if (!ancestors.Any())
                {
                    clear = true;
                    return;
                }

                // �w�b�_�[���ω����Ă���΍X�V
                if (!IsSameHeader(stickyHeaderControl, ancestors))
                {
                    // �c��m�[�h������HeaderedContentControl�Ƃ��ăl�X�g���ĕ\��
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

                        // Sticky�w�b�_�[�̍�����ScrollViewer�̍����̔����𒴂��Ȃ��悤�ɂ���
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
                // �N���A�t���O�������Ă���΃w�b�_�[������
                if (clear && stickyHeaderControl != null)
                {
                    stickyHeaderControl.Header = null;
                    stickyHeaderControl.Content = null;
                }
            }
        }

        /// <summary>
        /// ���݂�Sticky�w�b�_�[�ƁA�^����ꂽ�c��m�[�h���X�g����v���Ă��邩���肷��B
        /// </summary>
        /// <param name="stickyHeaderControl">Sticky�w�b�_�[�R���g���[��</param>
        /// <param name="ancestors">�c��TreeViewItem���X�g</param>
        /// <returns>��v���Ă����true</returns>
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
        /// �X�N���[���ʒu����ŏ㕔�Ɍ����Ă���TreeViewItem�𐄒肵�ĕԂ��B
        /// </summary>
        /// <param name="scrollViewer">�Ώ�ScrollViewer</param>
        /// <param name="stickyHeaderControl">Sticky�w�b�_�[�R���g���[��</param>
        /// <returns>�ŏ㕔��TreeViewItem</returns>
        private TreeViewItem FindTopVisibleTreeViewItem(ScrollViewer scrollViewer, HeaderedContentControl stickyHeaderControl)
        {
            var visibleItems = new List<TreeViewItem>();
            // ���ݕ\������TreeViewItem���ċA�I�ɗ�
            FindVisibleTreeViewItemsRecursive(_treeView, visibleItems);

            // �c��m�[�h�̍ő吔�𐄒�
            int estimatedHeaderCount = GetMaxAncestorCount(visibleItems);
            double estimatedStickyHeaderHeight = EstimateStickyHeaderHeight(stickyHeaderControl, estimatedHeaderCount);

            // Sticky�w�b�_�[�̍������l�����čŏ㕔�A�C�e�����擾
            var topItem = FindTopItem(visibleItems, scrollViewer, estimatedStickyHeaderHeight, stickyHeaderControl);

            // ���ۂ̑c�搔��Sticky�w�b�_�[�������Čv�Z���A�ēx�ŏ㕔�A�C�e�����擾
            int ancestorCount = GetAncestors(topItem).Count();

            return FindTopItem(visibleItems, scrollViewer, EstimateStickyHeaderHeight(stickyHeaderControl, ancestorCount), stickyHeaderControl);
        }

        /// <summary>
        /// �w�胊�X�g���őc��m�[�h���̍ő�l��Ԃ��B
        /// </summary>
        /// <param name="items">TreeViewItem���X�g</param>
        /// <returns>�ő�c�搔</returns>
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
        /// <param name="headerCount">�w�b�_�[�K�w��</param>
        /// <returns>���荂��</returns>
        private static double EstimateStickyHeaderHeight(HeaderedContentControl stickyHeaderControl, int headerCount)
        {
            int depth = GetStickyHeaderDepth(stickyHeaderControl);
            HeaderedContentControl current = stickyHeaderControl;
            // �����̃w�b�_�[�[������1�i���̍����𐄒�
            double headerElementHeight = (depth > 0) ? (current.ActualHeight / depth) : 24;
            return headerElementHeight * headerCount;
        }

        /// <summary>
        /// Sticky�w�b�_�[�̍������l�����čŏ㕔��TreeViewItem��Ԃ��B
        /// </summary>
        /// <param name="items">TreeViewItem���X�g</param>
        /// <param name="scrollViewer">ScrollViewer</param>
        /// <param name="stickyHeaderHeight">Sticky�w�b�_�[����</param>
        /// <param name="stickyHeaderControl">Sticky�w�b�_�[�R���g���[��</param>
        /// <returns>�ŏ㕔��TreeViewItem</returns>
        private static TreeViewItem FindTopItem(List<TreeViewItem> items, ScrollViewer scrollViewer, double stickyHeaderHeight, HeaderedContentControl stickyHeaderControl)
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
        /// �ċA�I��TreeViewItem��񋓂��A�\�����̂��̂����X�g�ɒǉ�����B
        /// </summary>
        /// <param name="parent">�eItemsControl</param>
        /// <param name="accumulator">TreeViewItem�̒~�σ��X�g</param>
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
        /// �w��TreeViewItem�̑c��m�[�h�����[�g�ɋ߂����ŗ񋓂���B
        /// </summary>
        /// <param name="item">TreeViewItem</param>
        /// <returns>�c��TreeViewItem��</returns>
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
    }
}