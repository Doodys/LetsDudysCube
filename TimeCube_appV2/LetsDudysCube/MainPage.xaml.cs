using LetsDudysCube.Helpers;
using LetsDudysCube.Models;
using MetroLog;
using MetroLog.Targets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Security.Cryptography;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace LetsDudysCube
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ILogger logger;

        private DispatcherTimer connectionStatusTimerDevices;
        private DispatcherTimer connectionStatusTimerConnection;

        private BluetoothLEDevice candidateToConnect;
        private DeviceInformation selectedDevice;
        private GattDeviceService gattService;
        private GattCharacteristicsResult gattCharacteristics;
        private GattCharacteristic gattCharacteristic;
        private CubeModel cubeModel;
        private ObservableCollection<ActivityModel> activityModels;
        private BluetoothLEAdvertisementWatcher bleWatcher;

        private Dictionary<ulong, DeviceInformation> foundDevices = new Dictionary<ulong, DeviceInformation>();

        private bool ConnectionStatus = false;
        private readonly string outputFile = "TimeCube_";

        public MainPage()
        {
            this.InitializeComponent();

            bleWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            bleWatcher.Received += BleWatcher_Received;

            bleWatcher.Start();

            PrepareDevices();
            cubeModel = new CubeModel();

            LogManagerFactory.DefaultConfiguration.AddTarget(LogLevel.Trace, LogLevel.Fatal, new StreamingFileTarget());
            logger = LogManagerFactory.DefaultLogManager.GetLogger<MainPage>();
        }

        #region Events

        private async void BleWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);

            lock (foundDevices)
            {
                if (!foundDevices.Any(di => di.Value.Name == device.Name) && !foundDevices.ContainsKey(args.BluetoothAddress))
                {
                    foundDevices.Add(
                        args.BluetoothAddress,
                        device.DeviceInformation
                        );
                }
            }
        }

        private async void ComboBox_Devices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedDevice = (DeviceInformation)comboBox_Devices.SelectedItem;
            var selectedPairForDevice = foundDevices.FirstOrDefault(fd => fd.Value == selectedDevice);

            if (selectedDevice != null)
            {
                try
                {
                    candidateToConnect = await BluetoothLEDevice.FromBluetoothAddressAsync
                    (
                        selectedPairForDevice.Key
                    );

                    if (candidateToConnect == null)
                        throw new NullReferenceException($"Device: {candidateToConnect.Name}");
                }
                catch (NullReferenceException ex)
                {
                    var dialog = new MessageDialog($"Cannot pair with a chosen device\n{ex.Message}", "Error");
                    await dialog.ShowAsync();

                    button_Pair.IsEnabled = false;
                    button_Connect.IsEnabled = false;

                    return;
                }
                catch (Exception ex)
                {
                    if (ex is System.FormatException || ex is System.OverflowException)
                    {
                        var dialog = new MessageDialog($"Problem with getting BLE address from device\n{ex.Message}", "Error");
                        await dialog.ShowAsync();

                        button_Pair.IsEnabled = false;
                        button_Connect.IsEnabled = false;

                        return;
                    }

                    throw;
                }

                if (!candidateToConnect.DeviceInformation.Pairing.IsPaired)
                {
                    textBox_IsPaired.Text = "Device is not paired";
                    textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DarkRed);

                    button_Pair.IsEnabled = true;
                    button_Connect.IsEnabled = false;
                }
                else
                {
                    textBox_IsPaired.Text = "Device is paired";
                    textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DarkGreen);

                    button_Pair.IsEnabled = false;
                    button_Connect.IsEnabled = true;
                }
            }
        }

        private void ComboBox_Devices_DropDownOpened(object sender, object e)
        {
            InitDeviceListStatusTimer();
        }

        private void ComboBox_Devices_DropDownClosed(object sender, object e)
        {
            connectionStatusTimerDevices.Stop();
        }

        private void Button_Pair_Click(object sender, RoutedEventArgs e)
        {
            PairDevices();
        }

        private async void button_Connect_Click(object sender, RoutedEventArgs e)
        {
            if (candidateToConnect == null)
                throw new ArgumentNullException(nameof(candidateToConnect));

            var gattServices = await candidateToConnect.GetGattServicesAsync();

            if (gattServices == null)
            {
                var dialog = new MessageDialog($"Problem with connecting. Cannot get proper Gatt Service from the device.", "Error");
                await dialog.ShowAsync();

                return;
            }

            bleWatcher.Stop();

            await PrepareGattAndBufferForApp(gattServices);
            InitConnectionStatusTimer();
            await PrepareGattDescriptorForReadingNotifications();
        }

        private void Charac_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out byte[] data);
            try
            {
                var notifyMessage = BitConverter.ToString(data);
                logger.Trace($"Hex value from notification: {notifyMessage}");
            }
            catch (ArgumentException) { }
        }

        private void TextBox_Up_TextChanged(object sender, TextChangedEventArgs e)
        {
            cubeModel.UpperWall = TextBox_Up.Text;
        }

        private void TextBox_Front_TextChanged(object sender, TextChangedEventArgs e)
        {
            cubeModel.FrontWall = TextBox_Front.Text;
        }

        private void TextBox_Right_TextChanged(object sender, TextChangedEventArgs e)
        {
            cubeModel.RightWall = TextBox_Right.Text;
        }

        private void TextBox_Back_TextChanged(object sender, TextChangedEventArgs e)
        {
            cubeModel.BackWall = TextBox_Back.Text;
        }

        private void TextBox_Left_TextChanged(object sender, TextChangedEventArgs e)
        {
            cubeModel.LeftWall = TextBox_Left.Text;
        }

        private void TextBox_Down_TextChanged(object sender, TextChangedEventArgs e)
        {
            cubeModel.BelowWall = TextBox_Down.Text;
        }

        #endregion


        #region Private methods

        private void InitDeviceListStatusTimer()
        {
            connectionStatusTimerDevices = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            connectionStatusTimerDevices.Tick += HandlerDeviceListStatusTimer_Tick;
            connectionStatusTimerDevices.Start();
        }

        private void HandlerDeviceListStatusTimer_Tick(object sender, object o)
        {
            PrepareDevices();
        }

        private void InitConnectionStatusTimer()
        {
            connectionStatusTimerConnection = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            connectionStatusTimerConnection.Tick += HandlerConnectionStatusTimer_Tick;
            connectionStatusTimerConnection.Start();

            logger.Error($"Started checking connection status of the device {candidateToConnect.Name}");
        }

        private async void HandlerConnectionStatusTimer_Tick(object sender, object o)
        {
            if (candidateToConnect != null)
            {
                if (candidateToConnect.ConnectionStatus.Equals(BluetoothConnectionStatus.Connected))
                {
                    if (!ConnectionStatus)
                    {
                        gattCharacteristic.ValueChanged -= Charac_ValueChanged;
                        await PrepareGattDescriptorForReadingNotifications();
                        logger.Trace($"Connected");
                    }

                    textBox_IsPaired.Text = "Device is connected";
                    textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DeepSkyBlue);

                    SetTextBoxComponentsStateOnWalls(true);                  
                    ConnectionStatus = true;
                }
                else
                {
                    if (ConnectionStatus)
                    {
                        logger.Warn($"Stopped receiving values from a device.");
                        logger.Trace($"Disconnected");
                    }

                    textBox_IsPaired.Text = "Connecting...";
                    textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DodgerBlue);

                    SetTextBoxComponentsStateOnWalls(false);
                    
                    ConnectionStatus = false;

                    gattCharacteristic.ValueChanged -= Charac_ValueChanged;
                }
            }
        }

        private void PrepareActivityNameListBox()
        {
            ListBox_ActivityName.ItemsSource = activityModels;
        }

        private void PrepareDevices()
        {
            if (foundDevices.Any())
            {
                var devicesInfo = new List<DeviceInformation>();

                foreach (var device in foundDevices)
                    devicesInfo.Add(device.Value);

                comboBox_Devices.ItemsSource = devicesInfo.Where(di => !string.IsNullOrEmpty(di.Name));
            }
        }

        private async void PairDevices()
        {
            var deviceId = candidateToConnect.DeviceInformation.Id;
            candidateToConnect.Dispose();
            candidateToConnect = null;
            candidateToConnect = await BluetoothLEDevice.FromIdAsync(deviceId);
            var handlerPairingRequested = new TypedEventHandler<DeviceInformationCustomPairing, DevicePairingRequestedEventArgs>(HandlerPairingReq);
            candidateToConnect.DeviceInformation.Pairing.Custom.PairingRequested += handlerPairingRequested;

            textBox_IsPaired.Text = "Pairing device now...";
            textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DarkCyan);

            var pairingResult = await candidateToConnect.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);

            if (pairingResult.Status != DevicePairingResultStatus.Paired)
            {
                textBox_IsPaired.Text = "Pairing failed!";
                textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DarkRed);
                logger.Error($"Failed to pair with device {candidateToConnect.Name}");
            }
            else
            {
                candidateToConnect.DeviceInformation.Pairing.Custom.PairingRequested -= handlerPairingRequested;
                logger.Trace($"Successfully paired with device {candidateToConnect.Name}");
                candidateToConnect.Dispose();
                candidateToConnect = await BluetoothLEDevice.FromIdAsync(deviceId);

                textBox_IsPaired.Text = "Device is paired";
                textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DarkSlateBlue);

                button_Pair.IsEnabled = false;
                button_Connect.IsEnabled = true;
            }
        }

        private void HandlerPairingReq(DeviceInformationCustomPairing CP, DevicePairingRequestedEventArgs DPR)
        {
            //so we get here for custom pairing request.
            //this is the magic place where your pin goes.
            //my device actually does not require a pin but
            //windows requires at least a "0".  So this solved 
            //it.  This does not pull up the Windows UI either.
            DPR.Accept("0");
        }

        private void SetTextBoxComponentsStateOnWalls(bool state)
        {
            TextBox_Back.IsEnabled = state;
            TextBox_Front.IsEnabled = state;
            TextBox_Left.IsEnabled = state;
            TextBox_Right.IsEnabled = state;
            TextBox_Up.IsEnabled = state;
            TextBox_Down.IsEnabled = state;
        }

        private void SaveOutputFile()
        {
            var path = $@"{cubeModel.OutputDirectory}\\{outputFile}{DateTime.Now.ToString(@"ddmmyyyyhhmmss")}.txt";

            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    foreach (var activity in activityModels)
                    {
                        sw.WriteLine($"{activity.ActivityName} : {activity.TimeOutput}");
                    }
                }
            }
        }

        private async Task PrepareGattAndBufferForApp(GattDeviceServicesResult gattServices)
        {
            gattService = gattServices.Services.Last(); //last service on the list is our main service           
            gattService.Session.MaintainConnection = true;
            gattCharacteristics = await gattService.GetCharacteristicsAsync(); //gattService.GetCharacteristicsForUuidAsync(gattService.Uuid);
            gattCharacteristic = gattCharacteristics.Characteristics.FirstOrDefault
                (c => c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify));

            BufferForAppSingleton.GattService = gattService;
            BufferForAppSingleton.GattCharacteristic = gattCharacteristic;
            BufferForAppSingleton.BleDevice = candidateToConnect;

            logger.Trace($"Prepared GATT service and characteristics");
        }

        private async Task PrepareGattDescriptorForReadingNotifications()
        {
            var descriptorWriteResult = GattCommunicationStatus.ProtocolError;
            var descriptorValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;

            while (descriptorWriteResult != GattCommunicationStatus.Success)
            {
                descriptorWriteResult = await gattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(descriptorValue);
            }

            gattCharacteristic.ValueChanged += Charac_ValueChanged;
        }

        #endregion
    }
}
