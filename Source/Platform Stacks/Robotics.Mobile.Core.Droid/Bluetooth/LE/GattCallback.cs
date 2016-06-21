using System;
using Android.Bluetooth;

namespace Robotics.Mobile.Core.Bluetooth.LE
{
	public class GattCallback : BluetoothGattCallback
	{

		public event EventHandler<DeviceConnectionEventArgs> DeviceConnected = delegate {};
		public event EventHandler<DeviceConnectionEventArgs> DeviceDisconnected = delegate {};
		public event EventHandler<ServicesDiscoveredEventArgs> ServicesDiscovered = delegate {};
		public event EventHandler<CharacteristicReadEventArgs> CharacteristicValueUpdated = delegate {};

		protected Adapter _adapter;

		public GattCallback (Adapter adapter)
		{
			this._adapter = adapter;
		}

        public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
        {
            Console.WriteLine("OnConnectionStateChange: ");
            base.OnConnectionStateChange(gatt, status, newState);

            Device device = null;
            if (newState != ProfileState.Disconnected)
            { 
                device = new Device(gatt.Device, gatt, this, 0);
            }

			switch (newState) {
			// disconnected
			case ProfileState.Disconnected:
				Console.WriteLine ("disconnected");
				this.DeviceDisconnected (this, new DeviceConnectionEventArgs () { Device = device });
				gatt.Close ();
				gatt.Dispose ();
				break;
				// connecting
			case ProfileState.Connecting:
				Console.WriteLine ("Connecting");
				break;
				// connected
			case ProfileState.Connected:
				Console.WriteLine ("Connected");
				this.DeviceConnected (this, new DeviceConnectionEventArgs () { Device = device });
				break;
				// disconnecting
			case ProfileState.Disconnecting:
				Console.WriteLine ("Disconnecting");
				break;
			}
		}

		public override void OnServicesDiscovered (BluetoothGatt gatt, GattStatus status)
		{
			base.OnServicesDiscovered (gatt, status);

			Console.WriteLine ("OnServicesDiscovered: " + status.ToString ());

			this.ServicesDiscovered (this, new ServicesDiscoveredEventArgs ());
		}

		public override void OnDescriptorRead (BluetoothGatt gatt, BluetoothGattDescriptor descriptor, GattStatus status)
		{
			base.OnDescriptorRead (gatt, descriptor, status);

			Console.WriteLine ("OnDescriptorRead: " + descriptor.ToString());

		}

		public override void OnCharacteristicRead (BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
		{
			base.OnCharacteristicRead (gatt, characteristic, status);

			Console.WriteLine ("OnCharacteristicRead: " + characteristic.GetStringValue (0));

			this.CharacteristicValueUpdated (this, new CharacteristicReadEventArgs () { 
				Characteristic = new Characteristic (characteristic, gatt, this) }
			);
		}

		public override void OnCharacteristicChanged (BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
		{
			base.OnCharacteristicChanged (gatt, characteristic);

			Console.WriteLine ("OnCharacteristicChanged: " + characteristic.GetStringValue (0));

			this.CharacteristicValueUpdated (this, new CharacteristicReadEventArgs () { 
				Characteristic = new Characteristic (characteristic, gatt, this) }
			);
		}

		public override void OnCharacteristicWrite (BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
		{
			base.OnCharacteristicWrite (gatt, characteristic, status);

			Console.WriteLine ("OnCharacteristicWrite: " + characteristic.GetStringValue(0) + " - " + status.ToString());

			this.CharacteristicValueUpdated (this, new CharacteristicReadEventArgs () { 
				Characteristic = new Characteristic (characteristic, gatt, this) }
			);
		}
	}
}

