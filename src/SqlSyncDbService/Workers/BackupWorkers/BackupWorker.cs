﻿using Microsoft.AspNetCore.Mvc;
using SqlSyncDbService.Workers.Helpers;
using SqlSyncDbService.Workers.Interfaces;
using SqlSyncDbService.Workers.BackupWorkers;
using System.Diagnostics;

namespace SqlSyncDbService.Workers.BackupWorkers
{
    public class BackupWorker : WorkerBase
    {
        public BackupWorker(ILogger? logger = null) : base(logger)
        {
        }

        public override string Name => $"BackupWorker-{BackupConfig.DbName}";
        public override IWorkerConfig Config => BackupConfig;
        public BackupWorkerConfig BackupConfig { get; set; } = new BackupWorkerConfig();
        public BackupWorkerState BackupState { get; } = new BackupWorkerState();

        public override IWorkerState State => BackupState;

        protected override void WriteLine(string msg) => Debug.WriteLine($"\tRESTORE: {msg}");

        public override async Task<bool> RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (BackupConfig.IsAuto)
                {
                    //backup
                    await State.UpdateStateByProcess(async () =>
                    {
                        var isExistbackupFull = BackupConfig.IsExistBackupFull(BackupState.MinVersion);

                        if (!isExistbackupFull || BackupConfig.IsReset(DateTime.Now))
                        {
                            await BackupFullAsync();
                        }
                        else
                        {
                            await BackupLogAsync();
                        }
                    });
                    CallHookAsync("BackupSuccess", BackupState);
                }

                if (cancellationToken.IsCancellationRequested) break;
                await Task.Delay(BackupConfig.DelayTime, cancellationToken);
            }
            return true;
        }

        public async Task<bool> BackupLogAsync()
        {
            return await new BackupWorkerApi().BackupLog(BackupConfig, BackupState);
        }

        public async Task<bool> BackupFullAsync()
        {
            return await new BackupWorkerApi().BackupFull(BackupConfig, BackupState);
        }

        public string? GetFileBackup(string? versionToDownload, out string? version)
        {
            //check first download file
            version = BackupState.MinVersion;
            if (versionToDownload == null)
            {
                return BackupConfig.GetPathBackupFull(BackupState.MinVersion);
            }

            //get state
            var state = BackupConfig.GetStateByVersion(versionToDownload);

            //check state not found
            version = state?.NextVersion;
            if (version == null) return default;

            //check old version
            if (version.CompareTo(BackupState.MinVersion) < 0)
            {
                return BackupConfig.GetPathBackupFull(BackupState.MinVersion);
            }

            //return next version
            return BackupConfig.GetPathFile(BackupState.MinVersion, version);
        }
    }

}
