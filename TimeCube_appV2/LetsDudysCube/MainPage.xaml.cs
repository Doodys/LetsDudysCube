using LetsDudysCube.Enums;
using LetsDudysCube.Helpers;
using LetsDudysCube.Models;
using MetroLog;
using MetroLog.Targets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
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
        public static ActivityModelPreparation activityModelList = new(new CubeModel());

        private Windows.UI.Xaml.DispatcherTimer connectionStatusTimerDevices;
        private Windows.UI.Xaml.DispatcherTimer connectionStatusTimerConnection;

        private BluetoothLEDevice candidateToConnect;
        private DeviceInformation selectedDevice;
        private GattDeviceService gattService;
        private GattCharacteristicsResult gattCharacteristics;
        private GattCharacteristic gattCharacteristic;

        private readonly ILogger logger;
        private readonly CubeModel cubeModel;
        private readonly BluetoothLEAdvertisementWatcher bleWatcher;
        private readonly Dictionary<ulong, DeviceInformation> foundDevices;

        private readonly CubeTimer upperWallTimer;
        private readonly CubeTimer leftWallTimer;
        private readonly CubeTimer rightWallTimer;
        private readonly CubeTimer lowerWallTimer;
        private readonly CubeTimer backWallTimer;
        private readonly CubeTimer frontWallTimer;

        private bool connectionStatus = false;
        private bool measurementStarted = false;
        private const string OutputFile = "TimeCube_";

        public MainPage()
        {
            this.InitializeComponent();

            foundDevices = new();

            bleWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            bleWatcher.Received += BleWatcher_Received;
            bleWatcher.Start();

            PrepareDevices();
            cubeModel = new CubeModel();

            upperWallTimer = new CubeTimer();
            leftWallTimer = new CubeTimer();
            rightWallTimer = new CubeTimer();
            lowerWallTimer = new CubeTimer();
            backWallTimer = new CubeTimer();
            frontWallTimer = new CubeTimer();

            LogManagerFactory.DefaultConfiguration.AddTarget(LogLevel.Trace, LogLevel.Fatal, new StreamingFileTarget());
            logger = LogManagerFactory.DefaultLogManager.GetLogger<MainPage>();
        }

        #region Events

        private async void BleWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);

            if (device is null)
                return;

            lock (foundDevices)
            {
                if (foundDevices.All(di => di.Value.Name != device.Name) && !foundDevices.ContainsKey(args.BluetoothAddress))
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
                textBox_IsPaired.Text = "Checking pairing status...";
                textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DarkSlateBlue);

                try
                {
                    candidateToConnect = await BluetoothLEDevice.FromBluetoothAddressAsync
                    (
                        selectedPairForDevice.Key
                    );

                    if (candidateToConnect == null)
                        throw new NullReferenceException(nameof(candidateToConnect));
                }
                catch (NullReferenceException ex)
                {
                    var dialog = new MessageDialog($"Cannot pair with a chosen device\n{ex.Message}", "Error");
                    await dialog.ShowAsync();

                    button_Pair.IsEnabled = false;
                    button_Connect.IsEnabled = false;

                    return;
                }
                catch (Exception ex) when (ex is System.FormatException or System.OverflowException)
                {
                    var dialog = new MessageDialog($"Problem with getting BLE address from device\n{ex.Message}",
                        "Error");
                    await dialog.ShowAsync();

                    button_Pair.IsEnabled = false;
                    button_Connect.IsEnabled = false;

                    return;
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

            GattDeviceServicesResult gattServices = null;

            try
            {
                textBox_IsPaired.Text = "Connecting...";
                textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DodgerBlue);

                gattServices = await candidateToConnect.GetGattServicesAsync();

                if (gattServices == null)
                {
                    var dialog =
                        new MessageDialog("Problem with connecting. Cannot get proper Gatt Service from the device.",
                            "Error");
                    await dialog.ShowAsync();

                    throw new Exception();
                }
            }
            catch (Exception)
            {
                textBox_IsPaired.Text = "Error";
                textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DarkRed);
            }

            bleWatcher.Stop();

            await PrepareGattAndBufferForApp(gattServices);
            InitConnectionStatusTimer();
            await PrepareGattDescriptorForReadingNotifications();
        }

        private void Character_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out byte[] data);

            try
            {
                var notifyMessage = BitConverter.ToString(data);

                if (!int.TryParse(notifyMessage, out var parsedMessage))
                    return;

                logger.Trace($"Hex value from notification: {parsedMessage}");

                if (measurementStarted)
                {
                    var result = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () => UpdateTime(parsedMessage)).AsTask();
                }
            }
            catch (ArgumentException) { }
        }

        private void TextBox_Up_TextChanged(object sender, TextChangedEventArgs e)
            => cubeModel.UpperWall = TextBox_Up.Text;

        private void TextBox_Front_TextChanged(object sender, TextChangedEventArgs e)
            => cubeModel.FrontWall = TextBox_Front.Text;

        private void TextBox_Right_TextChanged(object sender, TextChangedEventArgs e)
            => cubeModel.RightWall = TextBox_Right.Text;

        private void TextBox_Back_TextChanged(object sender, TextChangedEventArgs e)
            => cubeModel.BackWall = TextBox_Back.Text;

        private void TextBox_Left_TextChanged(object sender, TextChangedEventArgs e)
            => cubeModel.LeftWall = TextBox_Left.Text;

        private void TextBox_Down_TextChanged(object sender, TextChangedEventArgs e)
            => cubeModel.BelowWall = TextBox_Down.Text;

        private async void Button_OutputDirectory_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
            };
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();

            if (folder == null)
                return;

            Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace(
                "PickedFolderToken", folder);

            cubeModel.OutputDirectory = folder.Path;
            Button_OutputDirectory.IsEnabled = false;
        }

        #endregion Events

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
            if (candidateToConnect == null)
                return;

            if (candidateToConnect.ConnectionStatus.Equals(BluetoothConnectionStatus.Connected))
            {
                if (!connectionStatus)
                {
                    gattCharacteristic.ValueChanged -= Character_ValueChanged;
                    await PrepareGattDescriptorForReadingNotifications();
                    logger.Trace($"Connected");
                }

                textBox_IsPaired.Text = "Device is connected";
                textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DeepSkyBlue);

                SetTextBoxComponentsStateOnWalls(true);
                connectionStatus = true;
            }
            else
            {
                if (connectionStatus)
                {
                    logger.Warn($"Stopped receiving values from a device.");
                    logger.Trace($"Disconnected");
                }

                textBox_IsPaired.Text = "Connecting...";
                textBox_IsPaired.Foreground = new SolidColorBrush(Colors.DodgerBlue);

                SetTextBoxComponentsStateOnWalls(false);

                connectionStatus = false;

                gattCharacteristic.ValueChanged -= Character_ValueChanged;
            }
        }

        private void PrepareActivityNameListBox()
        {
            if (activityModelList == null)
                return;

            _ = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher
                .RunAsync(
                    CoreDispatcherPriority.Normal,
                    agileCallback: () => ListBox_ActivityName.ItemsSource = activityModelList
                    );
        }

        private void PrepareDevices()
        {
            if (!foundDevices.Any())
                return;

            var devicesInfo = foundDevices.Select(device => device.Value).ToList();

            comboBox_Devices.ItemsSource = devicesInfo.Where(di => !string.IsNullOrEmpty(di.Name));
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

        private async void SaveOutputFile()
        {
            var outputFileName = string.Concat(OutputFile, DateTime.Now.ToString("yyyyMMddHHmmss"), ".txt");

            StorageFolder storageFolder =
                await StorageFolder.GetFolderFromPathAsync(cubeModel.OutputDirectory);
            StorageFile sampleFile =
                await storageFolder.CreateFileAsync(outputFileName,
                    CreationCollisionOption.ReplaceExisting);

            List<string> outputData = activityModelList.Select(activity => $"{activity.ActivityName} : {activity.TimeOutput}").ToList();

            await Windows.Storage.FileIO.WriteLinesAsync(sampleFile, outputData);
        }

        private async Task PrepareGattAndBufferForApp(GattDeviceServicesResult gattServices)
        {
            if (gattServices is null)
            {
                throw new ArgumentNullException(nameof(gattServices));
            }

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

            gattCharacteristic.ValueChanged += Character_ValueChanged;
        }

        private async Task InitializeStart()
        {
            var validationMessage = ValidateWindowControls();

            if (validationMessage.Length > 0)
            {
                var dialog =
                    new MessageDialog(validationMessage.ToString(),
                        "Error");
                await dialog.ShowAsync();
            }
            else
            {
                measurementStarted = true;
                SetTextBoxComponentsStateOnWalls(false);
                leftWallTimer.Start();
            }
        }

        private void InitializeStop()
        {
            measurementStarted = false;

            upperWallTimer.Stop();
            leftWallTimer.Stop();
            rightWallTimer.Stop();
            lowerWallTimer.Stop();
            backWallTimer.Stop();
            frontWallTimer.Stop();

            SuspendDeviceConnectionAndServices();

            SaveOutputFile();
            //RestartTimeCube();
        }

        private void SuspendDeviceConnectionAndServices()
        {
            if (gattService == null || !gattService.Session.MaintainConnection)
                return;

            gattService.Session.MaintainConnection = false;
            gattService.Session.Dispose();
            gattService!.Dispose();
            gattService = null;

            var status = gattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.None);

            candidateToConnect!.Dispose();
            candidateToConnect = null;
        }

        private StringBuilder ValidateWindowControls()
        {
            var validationMessage = new StringBuilder();

            if (string.IsNullOrEmpty(cubeModel.OutputDirectory))
                validationMessage.AppendLine("You have to choose output directory");

            if (string.IsNullOrEmpty(TextBox_Up.Text))
                validationMessage.AppendLine("You have to choose name for upper wall");

            if (string.IsNullOrEmpty(TextBox_Left.Text))
                validationMessage.AppendLine("You have to choose name for left wall");

            if (string.IsNullOrEmpty(TextBox_Right.Text))
                validationMessage.AppendLine("You have to choose name for right wall");

            if (string.IsNullOrEmpty(TextBox_Down.Text))
                validationMessage.AppendLine("You have to choose name for lower wall");

            if (string.IsNullOrEmpty(TextBox_Front.Text))
                validationMessage.AppendLine("You have to choose name for front wall");

            if (string.IsNullOrEmpty(TextBox_Back.Text))
                validationMessage.AppendLine("You have to choose name for back wall");

            return validationMessage;
        }

        private void UpdateTime(int message)
        {
            if (!measurementStarted)
                return;

            switch (message)
            {
                case (int)PositionEnum.Left:
                    upperWallTimer.Stop();
                    rightWallTimer.Stop();
                    lowerWallTimer.Stop();
                    backWallTimer.Stop();
                    frontWallTimer.Stop();
                    if (!leftWallTimer.isStarted)
                    {
                        leftWallTimer.Start();
                    }
                    UpdateActivityModel(cubeModel.LeftWall, leftWallTimer);
                    break;

                case (int)PositionEnum.Right:
                    upperWallTimer.Stop();
                    leftWallTimer.Stop();
                    lowerWallTimer.Stop();
                    backWallTimer.Stop();
                    frontWallTimer.Stop();
                    if (!rightWallTimer.isStarted)
                    {
                        rightWallTimer.Start();
                    }
                    UpdateActivityModel(cubeModel.RightWall, rightWallTimer);
                    break;

                case (int)PositionEnum.Up:
                    leftWallTimer.Stop();
                    rightWallTimer.Stop();
                    lowerWallTimer.Stop();
                    backWallTimer.Stop();
                    frontWallTimer.Stop();
                    if (!upperWallTimer.isStarted)
                    {
                        upperWallTimer.Start();
                    }
                    UpdateActivityModel(cubeModel.UpperWall, upperWallTimer);
                    break;

                case (int)PositionEnum.Down:
                    upperWallTimer.Stop();
                    leftWallTimer.Stop();
                    rightWallTimer.Stop();
                    backWallTimer.Stop();
                    frontWallTimer.Stop();
                    if (!lowerWallTimer.isStarted)
                    {
                        lowerWallTimer.Start();
                    }
                    UpdateActivityModel(cubeModel.BelowWall, lowerWallTimer);
                    break;

                case (int)PositionEnum.Back:
                    upperWallTimer.Stop();
                    leftWallTimer.Stop();
                    rightWallTimer.Stop();
                    lowerWallTimer.Stop();
                    frontWallTimer.Stop();
                    if (!backWallTimer.isStarted)
                    {
                        backWallTimer.Start();
                    }
                    UpdateActivityModel(cubeModel.BackWall, backWallTimer);
                    break;

                case (int)PositionEnum.Front:
                    upperWallTimer.Stop();
                    leftWallTimer.Stop();
                    rightWallTimer.Stop();
                    lowerWallTimer.Stop();
                    backWallTimer.Stop();
                    if (!frontWallTimer.isStarted)
                    {
                        frontWallTimer.Start();
                    }
                    UpdateActivityModel(cubeModel.FrontWall, frontWallTimer);
                    break;

                default:
                    upperWallTimer.Stop();
                    leftWallTimer.Stop();
                    rightWallTimer.Stop();
                    lowerWallTimer.Stop();
                    backWallTimer.Stop();
                    frontWallTimer.Stop();
                    break;
            }
        }

        private void UpdateActivityModel(string name, CubeTimer wallTimer)
        {
            var activity = activityModelList.FirstOrDefault(am => am.ActivityName.Equals(name));

            if (activity == null)
                return;

            activity.TimeSpent = wallTimer.TimerOutput();
            activity.TimeOutput = activity.TimeSpent.ToString(@"hh\:mm\:ss");
            ListBox_ActivityName.ItemsSource = activityModelList;
        }

        #endregion Private methods

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            cubeModel.OutputDirectory = AppContext.BaseDirectory;
            activityModelList.Clear();
            activityModelList = new ActivityModelPreparation(cubeModel);
            _ = InitializeStart();
        }

        private void testbutton2_Click(object sender, RoutedEventArgs e)
        {
            InitializeStop();
        }
    }
}