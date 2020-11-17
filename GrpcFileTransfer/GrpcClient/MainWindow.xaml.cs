using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GrpcClient
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

        private string GetFilePath()
        {
            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension
            dlg.Title = "选择文件";
            dlg.Filter = "所有文件(*.*)|*.*";
            dlg.FileName = "选择文件夹.";
            dlg.FilterIndex = 1;
            dlg.ValidateNames = false;
            dlg.CheckFileExists = false;
            dlg.CheckPathExists = true;
            dlg.Multiselect = false;//允许同时选择多个文件

            // Display OpenFileDialog by calling ShowDialog method
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == true)
            {
                // Open document
                return dlg.FileName;
            }

            return string.Empty;
        }

        // 打开文件
        private void btnOpenUpload_Click(object sender, RoutedEventArgs e)
        {
            lblUploadPath.Content = GetFilePath();
        }

        private CancellationTokenSource uploadTokenSource;

        //上传
        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            lblMessage.Content = string.Empty;

            uploadTokenSource = new CancellationTokenSource();
            List<string> fileNames = new List<string>();
            fileNames.Add(lblUploadPath.Content.ToString());
            var result = await ServerNet.FileTransfer.FileUpload(fileNames, "123", uploadTokenSource.Token);

            lblMessage.Content = result.Message;

            uploadTokenSource = null;
        }

        //取消上传
        private void btnCancelUpload_Click(object sender, RoutedEventArgs e)
        {
            uploadTokenSource?.Cancel();
        }

        //打开需要下载的文件
        private void btnOpenDownload_Click(object sender, RoutedEventArgs e)
        {
            txtDownloadPath.Text = GetFilePath();
        }

        //下载文件
        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            lblMessage.Content = string.Empty;

            downloadTokenSource = new CancellationTokenSource();
            List<string> fileNames = new List<string>();
            fileNames.Add(System.IO.Path.GetFileName(txtDownloadPath.Text));
            var result = await ServerNet.FileTransfer.FileDownload(fileNames, "123", Environment.CurrentDirectory, downloadTokenSource.Token);

            lblMessage.Content = result.Message;

            downloadTokenSource = null;
        }

        private CancellationTokenSource downloadTokenSource;

        //下载取消
        private void btnCancelDownload_Click(object sender, RoutedEventArgs e)
        {
            downloadTokenSource?.Cancel();
        }
    }
}
