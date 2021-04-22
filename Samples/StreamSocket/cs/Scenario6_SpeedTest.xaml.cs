//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using SDKTemplate;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StreamSocketSample
{
    /// <summary>
    /// A page for sixth scenario.
    /// </summary>
    public sealed partial class Scenario6 : Page
    {
        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as NotifyUser()
        private MainPage rootPage = MainPage.Current;

        /// <summary>
        /// The socket used to send data.
        /// </summary>
        private StreamSocket socket;

        /// <summary>
        /// The task for the test if it's running.
        /// </summary>
        private Task testTask;

        /// <summary>
        /// The cancellation source to stop the test if it's running.
        /// </summary>
        private CancellationTokenSource testCancellation;


        public Scenario6()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        /// <summary>
        /// Runs the speed test until it's canceled.
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> that can be used to stop the test.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> that represents the operation.
        /// </returns>
        private async Task RunTestAsync(CancellationToken ct)
        {
            // Used for generating random data below
            Random random = new Random();

            // Create a DataWriter if we did not create one yet. Otherwise use one that is already cached.
            object outValue;
            DataWriter writer;
            if (!CoreApplication.Properties.TryGetValue("clientDataWriter", out outValue))
            {
                writer = new DataWriter(socket.OutputStream);
                CoreApplication.Properties.Add("clientDataWriter", writer);
            }
            else
            {
                writer = (DataWriter)outValue;
            }

            // Create a 1k buffer
            byte[] buffer = new byte[1024];

            // Run until canceled
            while (!ct.IsCancellationRequested)
            {
                // Randomize the buffer
                random.NextBytes(buffer);

                // Write the packet type, followed by length of the buffer as UINT32 value followed up by the data.
                // Writing data to the writer will just store data in memory.
                writer.WriteByte((byte)PacketType.Bufffer);
                writer.WriteUInt32((UInt32)buffer.Length);
                writer.WriteBytes(buffer);

                // Write the locally buffered data to the network.
                try
                {
                    await writer.StoreAsync();
                    // SendOutput.Text = "\"" + stringToSend + "\" sent successfully.";
                    Debug.WriteLine("Wrote buffer");
                }
                catch (Exception exception)
                {
                    // If this is an unknown status it means that the error if fatal and retry will likely fail.
                    if (SocketError.GetStatus(exception.HResult) == SocketErrorStatus.Unknown)
                    {
                        throw;
                    }

                    rootPage.NotifyUser("Send failed with error: " + exception.Message, NotifyType.ErrorMessage);
                }
            }
        }

        /// <summary>
        /// This is the click handler for the 'SendHello' button.
        /// </summary>
        /// <param name="sender">Object for which the event was generated.</param>
        /// <param name="e">Event's parameters.</param>
        private void StartTest_Click(object sender, RoutedEventArgs e)
        {
            if (!CoreApplication.Properties.ContainsKey("connected"))
            {
                rootPage.NotifyUser("Please connect before starting the test.", NotifyType.ErrorMessage);
                return;
            }

            object outValue;
            if (!CoreApplication.Properties.TryGetValue("clientSocket", out outValue))
            {
                rootPage.NotifyUser("Please connect before starting the test.", NotifyType.ErrorMessage);
                return;
            }
            socket = (StreamSocket)outValue;

            // Disable start button
            StartTest.IsEnabled = false;

            // Create the cancellation token
            testCancellation = new CancellationTokenSource();

            // Start the test
            testTask = RunTestAsync(testCancellation.Token);

            // Notify
            rootPage.NotifyUser("Test started.", NotifyType.StatusMessage);

            // Enable the stop button
            StopTest.IsEnabled = true;
        }

        private void StopTest_Click(object sender, RoutedEventArgs e)
        {
            // Disable the stop button
            StopTest.IsEnabled = false;

            // Cancel test
            if ((testCancellation != null) && (!testCancellation.IsCancellationRequested))
            {
                testCancellation.Cancel();
                testCancellation = null;
                testTask = null;

                // Notify
                rootPage.NotifyUser("Test stopped.", NotifyType.StatusMessage);
            }

            // Enable start button
            StartTest.IsEnabled = true;
        }
    }
}
