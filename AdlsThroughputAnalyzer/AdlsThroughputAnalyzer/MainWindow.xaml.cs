using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AdlsThroughputAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainViewModel ViewModel { get { return (MainViewModel)DataContext; } }

        protected override void OnClosing(CancelEventArgs e)
        {
            Properties.Settings.Default.SubscriptionId = ViewModel.SubscriptionId;
            Properties.Settings.Default.AccountName = ViewModel.AccountName;
            Properties.Settings.Default.BlobSize = ViewModel.BlobSize;
            Properties.Settings.Default.MaxSegmentSize = ViewModel.MaxSegmentSize;
            Properties.Settings.Default.MaxThreadCount = ViewModel.MaxThreadCount;
            Properties.Settings.Default.TempFileLocal = ViewModel.TempFileLocal;
            Properties.Settings.Default.TempFileRemote = ViewModel.TempFileRemote;
            Properties.Settings.Default.Save();

            base.OnClosing(e);
        }
    }
}
