using System;
using System.Collections.Generic;
using Android.Bluetooth;
using System.Threading.Tasks;
using Java.Util;
using Android.Content;
using Android.OS;
using Android.Bluetooth.LE;
using Android.Runtime;
using System.Linq;

namespace Robotics.Mobile.Core.Bluetooth.LE
{
	public class Adapter : Java.Lang.Object, BluetoothAdapter.ILeScanCallback, IAdapter
	{
		// events
		public event EventHandler<DeviceDiscoveredEventArgs> DeviceDiscovered = delegate {};
		public event EventHandler<DeviceConnectionEventArgs> DeviceConnected = delegate {};
		public event EventHandler<DeviceConnectionEventArgs> DeviceDisconnected = delegate {};
		public event EventHandler ScanTimeoutElapsed = delegate {};

		// class members
		protected BluetoothManager _manager;
		protected BluetoothAdapter _adapter;
		protected GattCallback _gattCallback;
        private BluetoothLeScanner Scanner;
        private LEScanCallback ScanCallback;

        Queue<IDevice> DisconnectQueue = new Queue<IDevice>();
        IDevice CurrentlyDisconnectingDevice;

		public bool IsScanning {
			get { return this._isScanning; }
		} protected bool _isScanning;

		public IList<IDevice> DiscoveredDevices {
			get {
				return this._discoveredDevices;
			}
		} protected IList<IDevice> _discoveredDevices = new List<IDevice> ();

        public IList<IDevice> ConnectedDevices
        {
            get
            {
                return this._connectedDevices;
            }
        }
        protected IList<IDevice> _connectedDevices = new List<IDevice>();

        public Adapter ()
		{
			var appContext = Android.App.Application.Context;
			// get a reference to the bluetooth system service
			this._manager = (BluetoothManager) appContext.GetSystemService(Context.BluetoothService);
			this._adapter = this._manager.Adapter;

			this._gattCallback = new GattCallback (this);

			this._gattCallback.DeviceConnected += (object sender, DeviceConnectionEventArgs e) => {
                if (!this._connectedDevices.Any(d => d.ID.ToString() == e.Device.ID.ToString()))
                {
                    this._connectedDevices.Add(e.Device);
                }

                this.DeviceConnected(this, e);
            };

			this._gattCallback.DeviceDisconnected += (object sender, DeviceConnectionEventArgs e) => {
                IDevice device;

                if (this._connectedDevices.Count == 1)
                {
                    device = this._connectedDevices.First();
                    this._connectedDevices.Clear();
				} else {
                    device = ConnectedDevices.FirstOrDefault(d => d.ID.Equals(CurrentlyDisconnectingDevice.ID));

                    if (device != null)
                    {
                        this._connectedDevices.Remove(device);
                    }
                }

                e.Device = device;
                this.DeviceDisconnected (this, e);
                CurrentlyDisconnectingDevice = null;

                ProcessDisconnectQueue();
			};
		}

		public async void StartScanningForDevices (Guid serviceUuid, int timeout = 10000)
		{
			StartScanningForDevices (serviceUuid.ToString(), timeout);
		}

        public async void StartScanningForDevices(int timeout)
        {
            StartScanningForDevices(timeout: timeout);
        }

        public async void StartScanningForDevices (string serviceUuid = "", int timeout = 10000)
		{
			Console.WriteLine ("Adapter: Starting a scan for devices.");

			// clear out the list
			this._discoveredDevices = new List<IDevice> ();

			// start scanning
			this._isScanning = true;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                Scanner = this._adapter.BluetoothLeScanner;
				if (Scanner == null) {
					return;
				}

                if (String.IsNullOrEmpty(serviceUuid))
                {
                    ScanCallback = new LEScanCallback(this);
                }
                else
                {
                    ScanCallback = new LEScanCallback(this, serviceUuid);
                }

                Scanner.StartScan(ScanCallback);
            } else
            {
                if (String.IsNullOrEmpty(serviceUuid))
                {
                    this._adapter.StartLeScan(this);
                }
                else
                {
                    this._adapter.StartLeScan(new[] { UUID.FromString(serviceUuid) }, this);
                }
            }

			// in 10 seconds, stop the scan
			await Task.Delay (timeout);

			// if we're still scanning
			if (this._isScanning) {
				Console.WriteLine ("BluetoothLEManager: Scan timeout has elapsed.");
                StopScanningForDevices();
				this.ScanTimeoutElapsed (this, new EventArgs ());
			}
		}

		public void StopScanningForDevices ()
		{
			Console.WriteLine ("Adapter: Stopping the scan for devices.");
            
			if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop && null != Scanner && null != ScanCallback) {
				if (Scanner != null) {
					Scanner.StopScan (ScanCallback);
				}

				ScanCallback = null;
			} else {
				this._adapter.StopLeScan(this);
			}

			this._isScanning = false;
        }

		public void OnLeScan (BluetoothDevice bleDevice, int rssi, byte[] scanRecord)
		{
			Console.WriteLine ("Adapter.LeScanCallback: " + bleDevice.Name);

			Device device = new Device (bleDevice, null, null, rssi);

			if (!DeviceExistsInDiscoveredList (bleDevice))
            {
                this._discoveredDevices.Add(device);
                this.DeviceDiscovered(this, new DeviceDiscoveredEventArgs { Device = device });
            }
		}

		protected bool DeviceExistsInDiscoveredList(BluetoothDevice device)
		{
			foreach (var d in this._discoveredDevices) {
				if (device.Address == ((BluetoothDevice)d.NativeDevice).Address)
					return true;
			}
			return false;
		}


		public void ConnectToDevice (IDevice device)
		{
            if (!DiscoveredDevices.Any(d => d.ID.ToString() == device.ID.ToString()))
            {
                _discoveredDevices.Add(device);
            }

            var connectedDevice = ConnectedDevices.FirstOrDefault(d => d.ID.ToString() == device.ToString());

            if (null != connectedDevice && null != DeviceConnected) {
                DeviceConnected(this, new DeviceConnectionEventArgs
                {
                    Device = connectedDevice
                });
            } else
            {
                ((BluetoothDevice)device.NativeDevice).ConnectGatt(Android.App.Application.Context, false, this._gattCallback);
            }
		}

        object _locker = new { };

		public void DisconnectDevice (IDevice device)
		{
            if (null != device)
            {
                DisconnectQueue.Enqueue(device);
            }

            ProcessDisconnectQueue();
        }

        void ProcessDisconnectQueue ()
        {
            lock (_locker)
            {
                if (null == CurrentlyDisconnectingDevice && DisconnectQueue.Any())
                {
                    CurrentlyDisconnectingDevice = DisconnectQueue.Dequeue();

					if (null != CurrentlyDisconnectingDevice) {
						((Device)CurrentlyDisconnectingDevice).Disconnect ();

						if (CurrentlyDisconnectingDevice.State == DeviceState.Disconnected) {
							CurrentlyDisconnectingDevice = null;
						}
					} else {
						CurrentlyDisconnectingDevice = null;
						ProcessDisconnectQueue ();
					}
                }
            }
        }

        class LEScanCallback : ScanCallback
        {
            private string serviceUuid;

            public LEScanCallback(Adapter adapter)
            {
                this.Adapter = adapter;
            }

            public LEScanCallback(Adapter adapter, string serviceUuid) : this(adapter)
            {
                this.serviceUuid = serviceUuid;
            }

            public Adapter Adapter { get; private set; }

            public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult result)
            {
                if (!String.IsNullOrWhiteSpace(serviceUuid))
                {
                    if (result.ScanRecord != null && result.ScanRecord.ServiceUuids != null && result.ScanRecord.ServiceUuids.Contains(new ParcelUuid(UUID.FromString(serviceUuid))))
                    {
                        this.Adapter.OnLeScan(result.Device, result.Rssi, result.ScanRecord.GetBytes());
                    }
                } else
                {
                    this.Adapter.OnLeScan(result.Device, result.Rssi, result.ScanRecord.GetBytes());
                }
            }
        }

        class ScanStoppedCallback : ScanCallback
        {
            private Adapter Adapter;

            public ScanStoppedCallback(Adapter adapter)
            {
                this.Adapter = adapter;
            }

            public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult result)
            {
                Console.WriteLine("Scan stopped");
            }
        }
    }
}

