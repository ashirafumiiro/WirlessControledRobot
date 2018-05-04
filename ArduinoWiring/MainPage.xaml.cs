using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Communication;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using System.Threading;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ArduinoWiring
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DispatcherTimer timeout;
        byte servoPin = 11;
        public ushort pos = 0;
        public ushort Position {
            get { return pos; }

            set {
                if (value < 10) pos = 10;
                else if (value > 170) pos = 170;
                else pos = value;
            }
        }
        public ushort speed;
        public byte speedPin = 10;
        public byte frontPin = 4;
        public byte reversePin = 3;
        public bool moveForward = true, moveBackward = true, moveLeft = true, moveRight= true;
        // stopwatch for tracking connection timing
        Stopwatch connectionStopwatch = new Stopwatch();
        public IStream connection;
        public RemoteDevice arduino;
        Task<DeviceInformationCollection> task;
        CancellationTokenSource cancelTokenSource;

        public MainPage()
        {
            this.InitializeComponent();
            cancelTokenSource = new CancellationTokenSource();
            task = BluetoothSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>(cancelTokenSource.Token);
            if (true)
            {
                task.ContinueWith(listTask =>
                {
                //store the result and populate the device list on the UI thread
                var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
                    {
                        Connections connections = new Connections();

                        var result = listTask.Result;
                        if (result == null || result.Count == 0)
                        {
                            DebugInfo.Text = "No items found.";
                            ConnectionList.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            foreach (DeviceInformation device in result)
                            {
                                connections.Add(new Connection(device.Name, device));
                            }
                            DebugInfo.Text = "Select an device and press \"Connect\" to connect.";
                        }

                        ConnectionList.ItemsSource = connections;
                    }));
                });
            }
        }

    private async void OnDeviceReady()
        {
            arduino.pinMode(servoPin, PinMode.SERVO);
            Position = 90;
            arduino.analogWrite(servoPin, Position);
            arduino.pinMode(speedPin, PinMode.PWM);
            await DebugInfo.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() => {
                DebugInfo.Text = "Connected to Device";
            }));
        }

        //this async Task function will execute infinitely in the background

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            DebugInfo.Text = "Atempting to Connect";
            ConnectButton.IsEnabled = false;
            DeviceInformation device = null;
            if (ConnectionList.SelectedItem != null)
            {
                var selectedConnection = ConnectionList.SelectedItem as Connection;
                device = selectedConnection.Source as DeviceInformation;

                connection = new BluetoothSerial(device);
                arduino = new RemoteDevice(connection);
                arduino.DeviceReady += OnDeviceReady;
                arduino.DeviceConnectionFailed += Arduino_DeviceConnectionFailed;
                arduino.DeviceConnectionLost += Arduino_DeviceConnectionLost;
                //using my Bluetooth device's baud rate, StandardFirmata configured to match
                connection.begin(9600, SerialConfig.SERIAL_8N1);

                //stop watch for timing
                connectionStopwatch.Reset();
                connectionStopwatch.Start();

                timeout = new DispatcherTimer();
                timeout.Interval = new TimeSpan(0, 0, 30);
                timeout.Tick += Timeout_Tick;
                timeout.Start();
            }
            else
            {
                DebugInfo.Text = "Please select a device";
            }
        }

        //Remember to Disable all other buttons when connection is lost.
        private async void Arduino_DeviceConnectionLost(string message)
        {
            await ConnectButton.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>{
                DebugInfo.Text = "Connection Lost";
            }));
           
        }

        private void Timeout_Tick(object sender, object e)
        {
            var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                timeout.Stop();
                DebugInfo.Text = "Connection attempt timed out.";
                ConnectButton.IsEnabled = true;
                //Reset();
            }));
        }

        private void Arduino_DeviceConnectionFailed(string message)
        {
            string info = "failed to connect";
            DebugInfo.Text = info;// + message;
            ConnectButton.IsEnabled = true;
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ForwardButton_Checked(object sender, RoutedEventArgs e)
        {
            arduino.digitalWrite(13, PinState.HIGH);
        }

        private void ForwardButton_Unchecked(object sender, RoutedEventArgs e)
        {
            arduino.digitalWrite(13, PinState.LOW);
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            Position -= 5;
            arduino.analogWrite(servoPin, Position);
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            Position += 5;
            arduino.analogWrite(servoPin, Position);
        }

        private void SpeedMeter_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            speed = (ushort) SpeedMeter.Value;
            arduino.analogWrite(speedPin, speed);
        }

        private void FrontButton_Checked(object sender, RoutedEventArgs e)
        {
            MoveForward();
        }

        private void FrontButton_Unchecked(object sender, RoutedEventArgs e)
        {
            arduino.analogWrite(speedPin, 0);
            arduino.digitalWrite(reversePin, PinState.LOW);
            arduino.digitalWrite(frontPin, PinState.LOW);
        }

        private void ReverseButton_Checked(object sender, RoutedEventArgs e)
        {
            arduino.analogWrite(speedPin, 0);
            arduino.digitalWrite(frontPin, PinState.LOW);
            arduino.digitalWrite(reversePin, PinState.HIGH);
            arduino.analogWrite(speedPin, speed);
        }

        private void ReverseButton_Unchecked(object sender, RoutedEventArgs e)
        {
            arduino.analogWrite(speedPin, 0);
            arduino.digitalWrite(reversePin, PinState.LOW);
            arduino.digitalWrite(frontPin, PinState.LOW);
        }

        private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Left && e.Key !=
        Windows.System.VirtualKey.Right && e.Key !=
          Windows.System.VirtualKey.Down && e.Key !=
            Windows.System.VirtualKey.Up)
                base.OnKeyDown(e);
            else
            {
                //implement code here
                if (e.Key == Windows.System.VirtualKey.Up && moveForward)
                {
                    moveBackward = false;
                    ReverseButton.IsChecked = false;
                    FrontButton.IsChecked = true;
                }

                if (e.Key == Windows.System.VirtualKey.Down && moveBackward)
                {
                    moveForward = false;
                    FrontButton.IsChecked = false;
                    ReverseButton.IsChecked = true;
                }
                if (e.Key == Windows.System.VirtualKey.Left)
                {
                    moveRight = false;
                    moveLeft = true;
                    await Loop();
                }
                if (e.Key == Windows.System.VirtualKey.Right)
                {
                    moveLeft = false;
                    moveRight = true;
                    await Loop();
                }
            }

        }

        //Managing Keys
        private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Left && e.Key !=
        Windows.System.VirtualKey.Right && e.Key !=
          Windows.System.VirtualKey.Down && e.Key !=
            Windows.System.VirtualKey.Up)
                base.OnKeyDown(e);
            else
            {
                //implement code here
                if (e.Key == Windows.System.VirtualKey.Up)
                {
                    moveBackward = true;
                    ReverseButton.IsChecked = false;
                    FrontButton.IsChecked = false;
                }

                if (e.Key == Windows.System.VirtualKey.Down)
                {
                    moveForward = true;
                    FrontButton.IsChecked = false;
                    ReverseButton.IsChecked = false;
                }
                if (e.Key == Windows.System.VirtualKey.Left)
                {
                    moveLeft = false;
                }
                if (e.Key == Windows.System.VirtualKey.Right)
                {
                    moveRight = false;
                }

            }

        }


        //Controlling function

        private async Task Loop()
        {
            if (moveLeft)
            {
                while (moveLeft)
                {                  
                    Position += 5;
                    arduino.analogWrite(servoPin, Position);
                    await Task.Delay(100);     
                }
            }

            if (moveRight)
            {
                while (moveRight)
                {  
                    Position -= 5;
                    arduino.analogWrite(servoPin, Position);
                    await Task.Delay(100);            
                }
            }

        }

        public void MoveForward()
        {
            arduino.analogWrite(speedPin, 0);
            arduino.digitalWrite(reversePin, PinState.LOW);
            arduino.digitalWrite(frontPin, PinState.HIGH);
            arduino.analogWrite(speedPin, speed);
        }
    }
}

/*
 * Test for non directional keys and eliminate them
 
     if (e.Key != Windows.System.VirtualKey.Left && e.Key !=
        Windows.System.VirtualKey.Right && e.Key !=
          Windows.System.VirtualKey.Down && e.Key !=
            Windows.System.VirtualKey.Up)
              base.OnKeyDown(e);

     
     
     */
