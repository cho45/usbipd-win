﻿// SPDX-FileCopyrightText: Microsoft Corporation
//
// SPDX-License-Identifier: GPL-2.0-only

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management;
using System.Threading;

namespace UsbIpServer
{
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
    sealed class RegistryWatcher : IDisposable
    {
        readonly ManagementEventWatcher watcher;

        readonly Dictionary<BusId, Action> devices = new();

        public RegistryWatcher()
        {
            var query = @"SELECT * FROM RegistryTreeChangeEvent " +
                @"WHERE Hive='HKEY_LOCAL_MACHINE' " +
                @$"AND RootPath='{RegistryUtils.DevicesRegistryPath.Replace(@"\", @"\\",StringComparison.InvariantCulture)}'";

            watcher = new(query);
            watcher.EventArrived += HandleEvent;
            watcher.Start();
        }

        async void HandleEvent(object sender, EventArrivedEventArgs e)
        {
            // something changed in the registry, so check if we should unbind device
            var connectedDevices = await ExportedDevice.GetAll(CancellationToken.None);
            var devicesToUnbind = connectedDevices.Where(x => !RegistryUtils.IsDeviceShared(x));
            foreach (var device in devicesToUnbind)
            {
                if (devices.ContainsKey(device.BusId))
                {
                    devices[device.BusId]();
                    StopWatchingDevice(device.BusId);
                }
            }
        }

        public void WatchDevice(BusId busId, Action cancellationAction)
        {
            devices[busId] = cancellationAction;
        }

        public void StopWatchingDevice(BusId busId)
        {
            devices.Remove(busId);
        }

        bool IsDisposed;
        public void Dispose()
        {
            if (!IsDisposed)
            {
                watcher.EventArrived -= HandleEvent;
                watcher.Dispose();
                IsDisposed = true;
            }
        }
    }
}
