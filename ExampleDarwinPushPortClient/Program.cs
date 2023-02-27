﻿using System.Collections.Concurrent;
using OpenRailMessaging;
using RttiPPT;

namespace ExampleDarwinPushPortClient
{
    /*
     * This sample illustrates how to use .Net generally and C# specifically to 
     * receive and process messages from the Darwin Push Port.  Originally written by Chris Bailiss.
     * The message schemas are included in the project for information only.
     * The classes in the rttiPPT.cs file were autogenerated from these schemas.
     * This sample makes use of the Apache NMS Messaging API - http://activemq.apache.org/nms/
     * This sample was built against v2.0.1 of the API.  
     * The Apache.NMS and Apache.NMS.ActiveMQ assemblies can be downloaded/restored into the solution using NuGet
     */

    class Program
    {
        static void Main(string[] args)
        {
            // CONNECTION SETTINGS:  In your code, move these into some form of configuration file / table
            // *** change the lines below to match your personal details *** 
            const string sConnectUrl = "activemq:tcp://InsertYourHostHere.nationalrail.co.uk:61616?connection.watchTopicAdvisories=false";
            const string sUser = "InsertYourUserNameHere";
            const string sPassword = "InsertYourPasswordHere";
            const string sTopic = "darwin.pushport-v16";

            if ((sUser == "InsertYourUserNameHere") || (sPassword == "InsertYourPasswordHere") || (sConnectUrl.Contains("InsertYourHostHere")))
            {
                Console.WriteLine("DARWIN PUSH PORT RECEIVER SAMPLE: ");
                Console.WriteLine();
                Console.WriteLine("ERROR:  Please update the source code (in the Program.cs file) to use your user name and password!");
                Console.ReadLine();
                return;
            }

            // create the shared queues (into which the receiver will enqueue messages/errors)
            ConcurrentQueue<OpenRailMessage> oMessageQueue = new ConcurrentQueue<OpenRailMessage>();
            ConcurrentQueue<OpenRailException> oErrorQueue = new ConcurrentQueue<OpenRailException>();

            // create the receiver
            OpenRailDarwinPushPortReceiver oDarwinReceiver = new OpenRailDarwinPushPortReceiver(
                sConnectUrl, sUser, sPassword, sTopic, oMessageQueue, oErrorQueue, 100);

            // Start the receiver
            oDarwinReceiver.Start();

            // Running: process the output from the receiver (in the queues) and display progress
            DateTime dtRunUntilUtc = DateTime.UtcNow.AddSeconds(120);
            DateTime dtNextUiUpdateTime = DateTime.UtcNow;
            int iTextMessageCount = 0;
            int iBytesMessageCount = 0;
            int iUnsupportedMessageCount = 0;
            string msLastBytesMessageContent = null;
            int iErrorCount = 0;
            string msLastErrorInfo = null;
            while (DateTime.UtcNow < dtRunUntilUtc)
            {
                // attempt to dequeue and process any errors that occurred in the receiver
                while ((oErrorQueue.Count > 0) && (DateTime.UtcNow < dtNextUiUpdateTime))
                {
                    OpenRailException oOpenRailException = null;
                    if (oErrorQueue.TryDequeue(out oOpenRailException))
                    {
                        // the code here simply counts the errors, and captures the details of the last 
                        // error - your code may log details of errors to a database or log file
                        iErrorCount++;
                        msLastErrorInfo = OpenRailException.GetShortErrorInfo(oOpenRailException);
                    }
                }

                // attempt to dequeue and process some messages
                while ((oMessageQueue.Count > 0) && (DateTime.UtcNow < dtNextUiUpdateTime))
                {
                    OpenRailMessage oMessage = null;
                    if (oMessageQueue.TryDequeue(out oMessage))
                    {
                        // Darwin should not be sending text messages (code is here just in case)
                        OpenRailTextMessage oTextMessage = oMessage as OpenRailTextMessage;
                        if (oTextMessage != null) iTextMessageCount++;

                        // All Darwin push port messages should be byte messages
                        OpenRailBytesMessage oBytesMessage = oMessage as OpenRailBytesMessage;
                        if (oBytesMessage != null)
                        {
                            iBytesMessageCount++;

                            // the processing here simply deserializes the message to objects
                            // and gets a description of each object.  Your code here
                            // could write to a database, files, etc.
                            Pport oPPort = DarwinMessageHelper.GetMessageAsObjects(oBytesMessage.Bytes);
                            msLastBytesMessageContent = DarwinMessageHelper.GetMessageDescription(oPPort);
                        }

                        // Darwin should not be sending any other message types (code is here just in case)
                        OpenRailUnsupportedMessage oUnsupportedMessage = oMessage as OpenRailUnsupportedMessage;
                        if (oUnsupportedMessage != null) iUnsupportedMessageCount++;
                    }
                }

                if (dtNextUiUpdateTime < DateTime.UtcNow)
                {
                    Console.Clear();
                    Console.WriteLine("DARWIN PUSH PORT RECEIVER SAMPLE: ");
                    Console.WriteLine();
                    Console.WriteLine("Remaining Run Time = " + dtRunUntilUtc.Subtract(DateTime.UtcNow).TotalSeconds.ToString("###0.0") + " seconds");
                    Console.WriteLine();
                    Console.WriteLine("Receiver Status:");
                    Console.WriteLine("  Message Receiver Running = " + oDarwinReceiver.IsRunning.ToString());
                    Console.WriteLine("  Message Receiver Connected To Data Feed = " + oDarwinReceiver.IsConnected.ToString());
                    Console.WriteLine("  Size of local In-Memory Queue = " + oMessageQueue.Count.ToString()); // i.e. messages received from the feed but not yet processed locally
                    Console.WriteLine("  Last Message Received At = " + oDarwinReceiver.LastMessageReceivedAtUtc.ToLocalTime().ToString("HH:mm:ss.fff ddd dd MMM yyyy"));
                    Console.WriteLine("  Total Messages Received = " + oDarwinReceiver.MessageCount.ToString());
                    Console.WriteLine();
                    Console.WriteLine("Processing Status:");
                    Console.WriteLine("  Text Message Count = " + iTextMessageCount.ToString());
                    Console.WriteLine("  Bytes Message Count = " + iBytesMessageCount.ToString());
                    Console.WriteLine("  Unsupported Message Count = " + iUnsupportedMessageCount.ToString());
                    Console.WriteLine("  Last Bytes Message Parsed = " + (msLastBytesMessageContent == null ? "" : msLastBytesMessageContent));
                    Console.WriteLine();
                    Console.WriteLine("Errors:");
                    Console.WriteLine("  Total Errors = " + iErrorCount.ToString());
                    Console.WriteLine("  Last Error = " + (msLastErrorInfo == null ? "" : msLastErrorInfo));
                    Console.WriteLine();
                    dtNextUiUpdateTime = DateTime.UtcNow.AddMilliseconds(500);
                }

                if (oMessageQueue.Count < 10) Thread.Sleep(50);
            }

            Console.WriteLine("Stopping Receiver...");

            oDarwinReceiver.RequestStop();

            while (oDarwinReceiver.IsRunning)
            {
                Thread.Sleep(50);
            }

            Console.WriteLine("Receiver stopped.");
            Console.WriteLine("Finished.");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}
