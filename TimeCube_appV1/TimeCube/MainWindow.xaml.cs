using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using TimeCube.Builders;
using TimeCube.Enums;
using TimeCube.Models;

namespace TimeCube
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<string> serialPorts;
        public EntryDataModel entryDataModel;
        public ObservableCollection<ActivityModel> activityModels;

        private CubeTimer upperWallTimer;
        private CubeTimer leftWallTimer;
        private CubeTimer rightWallTimer;
        private CubeTimer lowerWallTimer;
        private CubeTimer breakWallTimer;

        private PortConfig portConfig;
        private string serialMessage;
        private Thread readThread;
        private SerialPort serialPort;

        private readonly string outputFile = "TimeCube_";

        public MainWindow()
        {
            InitializeComponent();

            entryDataModel = new EntryDataModel();
            serialPorts = TimeCubeWindowBuilder.PrepareSerialPortsCollection();
            FillSerialPortsComboBox();

            upperWallTimer = new CubeTimer();
            leftWallTimer = new CubeTimer();
            rightWallTimer = new CubeTimer();
            lowerWallTimer = new CubeTimer();
            breakWallTimer = new CubeTimer();

            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        #region Events

        private void SetOutputDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    var outputDir = dialog.SelectedPath;

                    entryDataModel.OutputDirectory = outputDir;
                    OutputDirectoryTextBox.Text = outputDir;
                }
            }
        }

        private void PortsComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            entryDataModel.PortName = (string)PortsComboBox.SelectedItem;
        }

        private void UpperWallTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            entryDataModel.UpperWallName = UpperWallTextBox.Text;
        }

        private void LeftWallTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            entryDataModel.LeftWallName = LeftWallTextBox.Text;
        }

        private void RightWallTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            entryDataModel.RightWallName = RightWallTextBox.Text;
        }

        private void LowerWallTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            entryDataModel.LowerWallName = LowerWallTextBox.Text;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeStart();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeStop();
        }

        #endregion

        #region Private Methods

        private void FillSerialPortsComboBox()
        {
            PortsComboBox.ItemsSource = serialPorts;
        }

        private void GenerateActivityModel()
        {
            if (activityModels == null)
                activityModels = TimeCubeWindowBuilder.InitializeActivityModels(entryDataModel);
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (!StartButton.IsEnabled && !string.IsNullOrEmpty(serialMessage))
            {
                string message = serialMessage.Replace("\r", "");

                if (message.Equals(PositionEnum.LEFT.ToString()))
                {
                    upperWallTimer.Stop();
                    rightWallTimer.Stop();
                    lowerWallTimer.Stop();
                    breakWallTimer.Stop();
                    leftWallTimer.Start();
                    var activity = activityModels.FirstOrDefault(am => am.ActivityName.Equals(entryDataModel.LeftWallName));
                    activity.TimeSpent = leftWallTimer.TimerOutput();
                    activity.TimeOutput = activity.TimeSpent.ToString(@"hh\:mm\:ss");
                }
                if (message.Equals(PositionEnum.RIGHT.ToString()))
                {
                    upperWallTimer.Stop();
                    leftWallTimer.Stop();
                    lowerWallTimer.Stop();
                    breakWallTimer.Stop();
                    rightWallTimer.Start();
                    var activity = activityModels.FirstOrDefault(am => am.ActivityName.Equals(entryDataModel.RightWallName));
                    activity.TimeSpent = rightWallTimer.TimerOutput();
                    activity.TimeOutput = activity.TimeSpent.ToString(@"hh\:mm\:ss");
                }
                if (message.Equals(PositionEnum.UP.ToString()))
                {
                    leftWallTimer.Stop();
                    rightWallTimer.Stop();
                    lowerWallTimer.Stop();
                    breakWallTimer.Stop();
                    upperWallTimer.Start();
                    var activity = activityModels.FirstOrDefault(am => am.ActivityName.Equals(entryDataModel.UpperWallName));
                    activity.TimeSpent = upperWallTimer.TimerOutput();
                    activity.TimeOutput = activity.TimeSpent.ToString(@"hh\:mm\:ss");
                }
                if (message.Equals(PositionEnum.DOWN.ToString()))
                {
                    upperWallTimer.Stop();
                    leftWallTimer.Stop();
                    rightWallTimer.Stop();
                    breakWallTimer.Stop();
                    lowerWallTimer.Start();
                    var activity = activityModels.FirstOrDefault(am => am.ActivityName.Equals(entryDataModel.LowerWallName));
                    activity.TimeSpent = lowerWallTimer.TimerOutput();
                    activity.TimeOutput = activity.TimeSpent.ToString(@"hh\:mm\:ss");
                }
                if (message.Equals(PositionEnum.BACK.ToString()))
                {
                    upperWallTimer.Stop();
                    leftWallTimer.Stop();
                    rightWallTimer.Stop();
                    lowerWallTimer.Stop();
                    breakWallTimer.Start();
                    var activity = activityModels.FirstOrDefault(am => am.ActivityName.Equals(entryDataModel.BreakWallName));
                    activity.TimeSpent = breakWallTimer.TimerOutput();
                    activity.TimeOutput = activity.TimeSpent.ToString(@"hh\:mm\:ss");
                }

                MeasurmentList.Items.Refresh();
            }
        }

        private void InitializeStart()
        {
            var validationMessage = ValidateWindowControls();

            if (validationMessage.Length > 0)
                MessageBox.Show(validationMessage.ToString(), "Error", MessageBoxButton.OK);
            else
            {
                portConfig = new PortConfig((string)PortsComboBox.SelectedItem);

                InvertIsEnabledState();
                GenerateActivityModel();
                MeasurmentList.ItemsSource = activityModels;

                leftWallTimer.Start();
                rightWallTimer.Start();
                lowerWallTimer.Start();
                breakWallTimer.Start();
                upperWallTimer.Start();

                StartSerialPortReading();
            }
        }

        private void InitializeStop()
        {
            portConfig.continueReading = false;

            InvertIsEnabledState();

            upperWallTimer.Stop();
            leftWallTimer.Stop();
            rightWallTimer.Stop();
            lowerWallTimer.Stop();
            breakWallTimer.Stop();
            
            readThread.Join();
            serialPort.Close();

            SaveOutputFile();
            RestartTimeCube();
        }

        private void InvertIsEnabledState()
        {
            StopButton.IsEnabled = !StopButton.IsEnabled;
            StartButton.IsEnabled = !StartButton.IsEnabled;

            UpperWallTextBox.IsEnabled = !UpperWallTextBox.IsEnabled;
            LeftWallTextBox.IsEnabled = !LeftWallTextBox.IsEnabled;
            RightWallTextBox.IsEnabled = !RightWallTextBox.IsEnabled;
            LowerWallTextBox.IsEnabled = !LowerWallTextBox.IsEnabled;
        }

        private void StartSerialPortReading()
        {
            Exception ex = null;
            readThread = new Thread(() => SafeExecution(() => Read(), out ex));
            serialPort = portConfig.serialPort;

            try
            {
                serialPort.Open();
                portConfig.continueReading = true;
                readThread.Start();

                if (ex != null)
                    throw new Exception();
            }
            catch(Exception)
            {
                portConfig.continueReading = false;
                MessageBox.Show($"Error occured during opening serial port {serialPort.PortName}\n{ex.Message}", "Error", MessageBoxButton.OK);
                InitializeStop();
            }
        }

        private void SafeExecution(Action readThread, out Exception exception)
        {
            exception = null;

            try
            {
                readThread.Invoke();
            }
            catch (Exception ex)
            {
                Handler(ex);
            }
        }

        private void Handler(Exception ex)
        {
            portConfig.continueReading = false;
            MessageBox.Show($"Error occured during opening serial port {serialPort.PortName}\n{ex.Message}", "Error", MessageBoxButton.OK);
            InitializeStop();
        }

        private void Read()
        {
            while (portConfig.continueReading)
            {
                try
                {
                    serialMessage = portConfig.serialPort.ReadLine();
                }
                catch (IOException ex)
                {
                    portConfig.continueReading = false;
                    MessageBox.Show($"IO Exception occured on port {serialPort.PortName}\n{ex.Message}", "Error", MessageBoxButton.OK);
                    throw (ex);
                }
                catch (InvalidOperationException ex)
                {
                    portConfig.continueReading = false;
                    MessageBox.Show($"COM port {serialPort.PortName} was probably disconnected\n{ex.Message}", "Error", MessageBoxButton.OK);
                    throw (ex);
                }
                catch (TimeoutException ex)
                {
                    //portConfig.continueReading = false;
                    //MessageBox.Show($"TimeoutException occured on port {serialPort.PortName}\n{ex.Message}", "Error", MessageBoxButton.OK);
                    //InitializeStop();
                    //throw (ex);
                    throw new Exception($"TimeoutException occured on port {serialPort.PortName}\n{ex.Message}");
                }
            }
        }

        private StringBuilder ValidateWindowControls()
        {
            var validationMessage = new StringBuilder();

            if (PortsComboBox.SelectedItem == null)
                validationMessage.AppendLine("You have to choose COM port");

            if (string.IsNullOrEmpty(OutputDirectoryTextBox.Text))
                validationMessage.AppendLine("You have to choose output directory");

            if (string.IsNullOrEmpty(UpperWallTextBox.Text))
                validationMessage.AppendLine("You have to choose name for upper wall");

            if (string.IsNullOrEmpty(LeftWallTextBox.Text))
                validationMessage.AppendLine("You have to choose name for left wall");

            if (string.IsNullOrEmpty(RightWallTextBox.Text))
                validationMessage.AppendLine("You have to choose name for right wall");

            if (string.IsNullOrEmpty(LowerWallTextBox.Text))
                validationMessage.AppendLine("You have to choose name for lower wall");

            return validationMessage;
        }

        private void SaveOutputFile()
        {
            var path = $@"{entryDataModel.OutputDirectory}\\{outputFile}{DateTime.Now.ToString(@"ddmmyyyyhhmmss")}.txt";

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

        private void RestartTimeCube()
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        #endregion
    }
}
