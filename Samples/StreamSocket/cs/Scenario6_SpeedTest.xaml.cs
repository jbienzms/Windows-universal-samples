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

            // Create a DataReader if we did not create one yet. Otherwise use one that is already cached.
            DataReader reader;
            if (!CoreApplication.Properties.TryGetValue("clientDataReader", out outValue))
            {
                reader = new DataReader(socket.InputStream);
                CoreApplication.Properties.Add("clientDataReader", reader);
            }
            else
            {
                reader = (DataReader)outValue;
            }

            // Create a buffer for reading and writing data
            byte[] buffer = new byte[4096];

            // Run until canceled
            while (!ct.IsCancellationRequested)
            {
                // Randomize the buffer
                random.NextBytes(buffer);

                // Write the packet type, followed by length of the buffer as UINT32 value followed up by the data.
                // Writing data to the writer will just store data in memory.
                writer.WriteByte((byte)PacketType.SpeedTest);
                writer.WriteUInt32((UInt32)buffer.Length);
                writer.WriteBytes(buffer);

                // Write the locally buffered data to the network and read the reply.
                try
                {
                    // Send bytes
                    await writer.StoreAsync();

                    // Load reply
                    uint actualBufferLength = await reader.LoadAsync((UInt32)buffer.Length);
                    if (actualBufferLength != (UInt32)buffer.Length)
                    {
                        // The underlying socket was closed before we were able to read the whole data.
                        rootPage.NotifyUser("Received failed", NotifyType.ErrorMessage);
                        return;
                    }

                    // Read reply
                    reader.ReadBytes(buffer);

                    float iBitsK = socket.Information.BandwidthStatistics.InboundBitsPerSecond / 1024f;
                    float iInstaK = socket.Information.BandwidthStatistics.InboundBitsPerSecondInstability / 1024f;
                    float oBitsK = socket.Information.BandwidthStatistics.OutboundBitsPerSecond / 1024f;
                    float oInstaK = socket.Information.BandwidthStatistics.OutboundBitsPerSecondInstability / 1024f;
                    float rtMaxMil = socket.Information.RoundTripTimeStatistics.Max / 1000f;
                    float rtMinMil = socket.Information.RoundTripTimeStatistics.Min / 1000f;
                    float rtSumMil = socket.Information.RoundTripTimeStatistics.Sum / 1000f;
                    float rtVarMil = socket.Information.RoundTripTimeStatistics.Variance / 1000f;

                    // Update stats
                    InboundBandwidthPeaked.Text = socket.Information.BandwidthStatistics.InboundBandwidthPeaked.ToString();
                    InboundBitsPerSecond.Text = iBitsK.ToString("#,0.00# kbps");
                    InboundBitsPerSecondInstability.Text = iInstaK.ToString("#,0.00# kbps");
                    OutboundBandwidthPeaked.Text = socket.Information.BandwidthStatistics.OutboundBandwidthPeaked.ToString();
                    OutboundBitsPerSecond.Text = oBitsK.ToString("#,0.00# kbps");
                    OutboundBitsPerSecondInstability.Text = oInstaK.ToString("#,0.00# kbps");
                    RoundTripMax.Text = rtMaxMil.ToString("#,0.00# ms");
                    RoundTripMin.Text = rtMinMil.ToString("#,0.00# ms");
                    RoundTripSum.Text = rtSumMil.ToString("#,0.00# ms");
                    RoundTripVariance.Text = rtVarMil.ToString("#,0.00# ms");
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
