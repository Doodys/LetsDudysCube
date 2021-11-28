using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace LetsDudysCube.Helpers
{
    public static class BufferForAppSingleton
    {
        public static GattDeviceService GattService { get; set; }
        public static GattCharacteristic GattCharacteristic { get; set; }
        public static BluetoothLEDevice BleDevice { get; set; }
    }
}