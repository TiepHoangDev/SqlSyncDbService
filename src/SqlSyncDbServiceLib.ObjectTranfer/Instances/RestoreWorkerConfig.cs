﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSyncDbServiceLib.ObjectTranfer.Instances
{
    public class RestoreWorkerConfig : WorkerConfigBase
    {
        public string IdBackupWorker { get; set; }
        public string BackupAddress { get; set; }
        public int MaxFileDownload { get; set; } = 50;

        public static RestoreWorkerConfig Create(string SqlConnectString, string BackupAddress, string IdBackupWorker)
        {
            return new RestoreWorkerConfig
            {
                BackupAddress = BackupAddress,
                IdBackupWorker = IdBackupWorker,
                SqlConnectString = SqlConnectString,
            };
        }

        public override void OnUpdateSqlConnectionString(string newValue, string oldValue)
        {
            base.OnUpdateSqlConnectionString(newValue, oldValue);
            DirData = Path.Combine(DirRoot, "restore");
        }

        public RestoreWorkerState GetStateByVersion(string version)
            => base.GetStateByVersion<RestoreWorkerState>(version);
        public override async Task ValidateSettingAsync(CancellationToken cancellationToken)
        {
            await base.ValidateSettingAsync(cancellationToken);
            ValidateSettingIsNullOrWhiteSpace("IdBackupWorker, BackupAddress", IdBackupWorker, BackupAddress);
            if (MaxFileDownload <= 0) throw new Exception($"Please set {nameof(MaxFileDownload)} > 0.");
            await ValidateBackupAddressAsync(cancellationToken);
        }

        public virtual async Task ValidateBackupAddressAsync(CancellationToken cancellationToken)
        {
            var RequestUri = GetUrlDownload();
            try
            {
                using (var http = new HttpClient())
                {
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Head,
                        RequestUri = RequestUri
                    };
                    var res = await http.SendAsync(request, cancellationToken);
                    if (!res.IsSuccessStatusCode)
                    {
                        throw new Exception($"Expect Url download is: {RequestUri}. that endpoint is not work, please check if backup sevrer is running or ignore if you sure that correct! Detail response: {(int)res.StatusCode} {res.StatusCode} {res.ReasonPhrase}");
                    }
                    res.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Expect Url download is: {RequestUri}. that endpoint is not work, please check if backup sevrer is running or ignore if you sure that correct! Detail error: {ex}");
            }
        }

        public Uri GetUrlDownload()
        {
            return new Uri($"{BackupAddress}/{GetNewBackupRequest.router}");
        }
    }
}