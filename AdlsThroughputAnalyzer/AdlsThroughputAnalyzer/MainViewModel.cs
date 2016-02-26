using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure;
using Auth = Microsoft.Azure.Common.Authentication;
using Microsoft.Azure.Common.Authentication.Models;
using Microsoft.Azure.Management.DataLake.StoreFileSystem;
using Microsoft.Azure.Management.DataLake.StoreFileSystem.Models;
using Microsoft.Azure.Management.DataLake.StoreUploader;
using Microsoft.Azure.Common.Authentication.Factories;
using System.Threading;

namespace AdlsThroughputAnalyzer
{
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Fields

        private static SubscriptionCloudCredentials _credentials;
        private Progress<UploadProgress> _progressTracker;
        private Stopwatch _stopwatch = new Stopwatch();
        private static Random _random = new Random();

        #endregion

        #region Constructors

        public MainViewModel()
        {
            SubscriptionId = Properties.Settings.Default.SubscriptionId;
            AccountName = Properties.Settings.Default.AccountName;
            BlobSize = Properties.Settings.Default.BlobSize;
            MaxSegmentSize = Properties.Settings.Default.MaxSegmentSize;
            MaxThreadCount = Properties.Settings.Default.MaxThreadCount;
            TempFileLocal = Properties.Settings.Default.TempFileLocal;
            TempFileRemote = Properties.Settings.Default.TempFileRemote;

            _progressTracker = new Progress<UploadProgress>();
            _progressTracker.ProgressChanged += UploadProgressChanged;

            LoginCommand = new RelayCommand(ExecuteLogin);
            UploadFileCommand = new RelayCommand(
                async (p) => await ExecuteUploadFileAsync(p), CanUploadFile);
            DirectDownloadCommand = new RelayCommand(
                async (p) => await ExecuteDirectDownloadAsync(p), CanDirectDownload);
        }

        #endregion

        #region Public Properties

        private string _subscriptionId;
        public string SubscriptionId
        {
            get { return _subscriptionId; }
            set { SetField(ref _subscriptionId, value, "SubscriptionId"); }
        }

        private string _accountName;
        public string AccountName
        {
            get { return _accountName; }
            set { SetField(ref _accountName, value, "AccountName"); }
        }

        private long _blobSize;
        public long BlobSize
        {
            get { return _blobSize; }
            set { SetField(ref _blobSize, value, "BlobSize"); }
        }

        private long _maxSegmentSize;
        public long MaxSegmentSize
        {
            get { return _maxSegmentSize; }
            set { SetField(ref _maxSegmentSize, value, "MaxSegmentSize"); }
        }

        private int _maxThreadCount;
        public int MaxThreadCount
        {
            get { return _maxThreadCount; }
            set { SetField(ref _maxThreadCount, value, "MaxThreadCount"); }
        }

        private string _tempFileLocal;
        public string TempFileLocal
        {
            get { return _tempFileLocal; }
            set { SetField(ref _tempFileLocal, value, "TempFileLocal"); }
        }

        private string _tempFileRemote;
        public string TempFileRemote
        {
            get { return _tempFileRemote; }
            set { SetField(ref _tempFileRemote, value, "TempFileRemote"); }
        }

        private string _progressInfo;
        public string ProgressInfo
        {
            get { return _progressInfo; }
            set { SetField(ref _progressInfo, value, "ProgressInfo"); }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get { return _progressValue; }
            set { SetField(ref _progressValue, value, "ProgressValue"); }
        }

        public RelayCommand LoginCommand { get; private set; }

        public RelayCommand UploadFileCommand { get; private set; }

        public RelayCommand DirectDownloadCommand { get; private set; }

        #endregion

        #region Public Methods

        public void ExecuteLogin(object parameter)
        {
            try
            {
                _credentials = GetAccessToken();
                ProgressInfo = "Login completed successfully.";
                OnPropertyChanged("UploadFileCommand");
                OnPropertyChanged("DirectDownloadCommand");
            }
            catch (Exception ex)
            {
                ProgressInfo = $"Error: {ex.Message}";
            }
        }

        public bool CanUploadFile(object parameter)
        {
            return _credentials != null;
        }

        public async Task ExecuteUploadFileAsync(object parameter)
        {
            if (_credentials == null)
                return;

            try
            {
                ProgressInfo = "Creating local temp file...";
                await CreateTempFileAsync();

                _stopwatch.Reset();
                ProgressInfo = "Initiating upload...";
                await UploadTempFileAsync();
            }
            catch (Exception ex)
            {
                ProgressInfo = $"Error: {ex.Message}";
            }
        }

        public bool CanDirectDownload(object parameter)
        {
            return _credentials != null;
        }

        public async Task ExecuteDirectDownloadAsync(object parameter)
        {
            if (_credentials == null)
                return;

            try
            {
                var subscriptionId = new Guid(SubscriptionId);
                var cloudCredentials = GetCloudCredentials(_credentials, subscriptionId);
                var client = new DataLakeStoreFileSystemManagementClient(cloudCredentials);

                // Get Remote File Info
                ProgressInfo = "Getting temote file info...";
                var summary = await client.FileSystem.GetContentSummaryAsync(
                    TempFileRemote, AccountName);
                var fileSize = summary.ContentSummary.Length;

                _stopwatch.Reset();
                ProgressInfo = "Downloading file...";
                await DownloadFileAsync(client, fileSize);
            }
            catch (Exception ex)
            {
                ProgressInfo = $"Error: {ex.Message}";
            }
        }

        #endregion

        #region Private Methods

        private void UploadProgressChanged(object sender, UploadProgress e)
        {
            ProgressValue = ((double)e.UploadedByteCount / e.TotalFileLength) * 100.0;
            var speed = (e.UploadedByteCount / 1048576.0) / (_stopwatch.ElapsedMilliseconds / 1000.0);
            ProgressInfo = $"{e.TotalSegmentCount} Segment(s), {speed: 0.000} MB/s";
        }

        private static SubscriptionCloudCredentials GetAccessToken(string username = null, SecureString password = null)
        {
            var authFactory = new AuthenticationFactory();
            var account = new AzureAccount
            {
                Type = AzureAccount.AccountType.User
            };

            if (username != null && password != null)
                account.Id = username;

            var env = AzureEnvironment.PublicEnvironments[EnvironmentName.AzureCloud];
            var authResult = authFactory.Authenticate(account, env, AuthenticationFactory.CommonAdTenant, password, Auth.ShowDialog.Auto);

            return new TokenCloudCredentials(authResult.AccessToken);
        }

        private static SubscriptionCloudCredentials GetCloudCredentials(SubscriptionCloudCredentials creds, Guid subId)
        {
            return new TokenCloudCredentials(subId.ToString(), ((TokenCloudCredentials)creds).Token);
        }

        private static char GetLetter()
        {
            // This method returns a random lowercase letter.
            // ... Between 'a' and 'z' inclusize.
            int num = _random.Next(0, 26); // Zero to 25
            char let = (char)('a' + num);
            return let;
        }

        private Task CreateTempFileAsync()
        {
            var blobSize = BlobSize;
            var localTempFile = TempFileLocal;

            return Task.Run(() => {
                var rowCount = blobSize * 1024;
                var directory = Path.GetDirectoryName(localTempFile);

                var sb = new StringBuilder();

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(localTempFile))
                {
                    var fileInfo = new FileInfo(localTempFile);
                    var fileSize = fileInfo.Length / 1048576.0;
                    if (blobSize == fileSize)
                        return;
                    File.Delete(localTempFile);
                }

                using (var file = File.OpenWrite(localTempFile))
                {
                    for (int r = 0; r < rowCount; r++)
                    {
                        sb.Clear();
                        for (int i = 0; i < 1023; i++)
                        {
                            sb.Append(GetLetter());
                        }
                        sb.Append('\n');
                        var data = Encoding.UTF8
                            .GetBytes(sb.ToString());
                        file.Write(data, 0, data.Length);
                    }
                    file.Close();
                }
            });
        }

        private Task UploadTempFileAsync()
        {
            var subscriptionId = new Guid(SubscriptionId);
            var cloudCredentials = GetCloudCredentials(_credentials, subscriptionId);
            var client = new DataLakeStoreFileSystemManagementClient(cloudCredentials);
            var frontEndAdapter = new DataLakeStoreFrontEndAdapter(
                AccountName, client);
            var uploadParameters = new UploadParameters(
                TempFileLocal, TempFileRemote,
                AccountName, threadCount: MaxThreadCount,
                isOverwrite: true, isBinary: false,
                maxSegmentLength: MaxSegmentSize * 1048576);

            return Task.Run(() => {
                var uploader = new DataLakeStoreUploader(uploadParameters, frontEndAdapter, _progressTracker);

                _stopwatch.Start();
                uploader.Execute();
                _stopwatch.Stop();

                Debug.WriteLine(_stopwatch.ElapsedMilliseconds);
            });
        }

        private async Task DownloadFileAsync(DataLakeStoreFileSystemManagementClient client, long fileSize)
        {
            var lcts = new LimitedConcurrencyLevelTaskScheduler(MaxThreadCount);
            var tasks = new List<Task>();
            var factory = new TaskFactory(lcts);
            var cts = new CancellationTokenSource();
            var segmentSize = MaxSegmentSize * 1048576;
            long currentSize = 0;

            _stopwatch.Start();
            while (currentSize < fileSize)
            {
                var length = currentSize + segmentSize < fileSize
                    ? segmentSize
                    : fileSize - currentSize;

                var parameters = new FileOpenParameters
                {
                    Offset = currentSize,
                    Length = segmentSize
                };

                Task t = factory.StartNew((p) => {
                    var fileOpenParameters = p as FileOpenParameters;
                    client.FileSystem.DirectOpen(
                        TempFileRemote, AccountName,
                        fileOpenParameters);
                }, parameters, cts.Token);
                tasks.Add(t);

                currentSize += segmentSize;
            }
            await Task.WhenAll(tasks.ToArray());
            _stopwatch.Stop();

            var speed = (fileSize / 1048576.0) / (_stopwatch.ElapsedMilliseconds / 1000.0);
            ProgressInfo = $"{tasks.Count} Segment(s), {speed: 0.000} MB/s";

            //TODO: Should the downloaded data be written to disk?
            //File.WriteAllBytes(LocalTempFile, file.FileContents);
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
