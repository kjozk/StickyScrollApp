using StickyScrollApp.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;

namespace StickyScrollApp
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<TreeItemViewModel> RootItems { get; } = new ObservableCollection<TreeItemViewModel>();

        public MainWindow()
        {
            InitializeComponent();

            // ダミーデータ作成
            var folder1 = new FolderViewModel { Name = "フォルダA" };
            var subFolderA1 = new FolderViewModel { Name = "サブフォルダA-1" };
            subFolderA1.Children.Add(new FileViewModel { Name = "ファイルA-1-1" });
            subFolderA1.Children.Add(new FileViewModel { Name = "ファイルA-1-2" });
            folder1.Children.Add(subFolderA1);
            folder1.Children.Add(new FileViewModel { Name = "ファイルA-1" });
            folder1.Children.Add(new FileViewModel { Name = "ファイルA-2" });
            folder1.Children.Add(new FileViewModel { Name = "ファイルA-3" });
            var subFolderA2 = new FolderViewModel { Name = "サブフォルダA-2" };
            subFolderA2.Children.Add(new FileViewModel { Name = "ファイルA-2-1" });
            subFolderA2.Children.Add(new FileViewModel { Name = "ファイルA-2-2" });
            subFolderA2.Children.Add(new FileViewModel { Name = "ファイルA-2-3" });
            folder1.Children.Add(subFolderA2);

            var folder2 = new FolderViewModel { Name = "フォルダB" };
            var subFolderB1 = new FolderViewModel { Name = "サブフォルダB-1" };
            subFolderB1.Children.Add(new FileViewModel { Name = "ファイルB-1-1" });
            subFolderB1.Children.Add(new FileViewModel { Name = "ファイルB-1-2" });
            subFolderB1.Children.Add(new FileViewModel { Name = "ファイルB-1-3" });
            var subFolderB2 = new FolderViewModel { Name = "サブフォルダB-2" };
            for (int i = 1; i <= 5; i++)
            {
                subFolderB2.Children.Add(new FileViewModel { Name = $"ファイルB-2-{i}" });
            }
            folder2.Children.Add(subFolderB1);
            folder2.Children.Add(subFolderB2);

            var folder3 = new FolderViewModel { Name = "フォルダC" };
            folder3.Children.Add(new FileViewModel { Name = "ファイルC-1" });
            folder3.Children.Add(new FileViewModel { Name = "ファイルC-2" });
            var subFolderC = new FolderViewModel { Name = "サブフォルダC-1" };
            var subSubFolderC = new FolderViewModel { Name = "サブサブフォルダC-1-1" };
            for (int i = 1; i <= 5; i++)
            {
                subSubFolderC.Children.Add(new FileViewModel { Name = $"ファイルC-1-1-{i}" });
            }
            subFolderC.Children.Add(subSubFolderC);
            folder3.Children.Add(subFolderC);

            var folder4 = new FolderViewModel { Name = "フォルダD" };
            for (int i = 1; i <= 5; i++)
            {
                folder4.Children.Add(new FileViewModel { Name = $"ファイルD-{i}" });
            }

            RootItems.Add(folder1);
            RootItems.Add(folder2);
            RootItems.Add(folder3);
            RootItems.Add(folder4);

            DataContext = this;
        }
    }
}
