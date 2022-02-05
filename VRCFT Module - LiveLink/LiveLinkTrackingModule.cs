using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using MelonLoader;
using VRCFaceTracking;
using VRCFaceTracking.Params;

namespace VRCFT_Module___LiveLink
{
    // Example "single-eye" data response.
    public struct LiveLinkTrackingDataEye
    {
        public float EyeBlink;
        public float EyeLookDown;
        public float EyeLookIn;
        public float EyeLookOut;
        public float EyeLookUp;
        public float EyeSquint;
        public float EyeWide;
        public float EyePitch;
        public float EyeYaw;
        public float EyeRoll;
    }

    public struct LiveLinkTrackingDataLips
    {
        public float JawForward;
        public float JawLeft;
        public float JawRight;
        public float JawOpen;
        public float MouthClose;
        public float MouthFunnel;
        public float MouthPucker;
        public float MouthLeft;
        public float MouthRight;
        public float MouthSmileLeft;
        public float MouthSmileRight;
        public float MouthFrownLeft;
        public float MouthFrownRight;
        public float MouthDimpleLeft;
        public float MouthDimpleRight;
        public float MouthStretchLeft;
        public float MouthStretchRight;
        public float MouthRollLower;
        public float MouthRollUpper;
        public float MouthShrugLower;
        public float MouthShrugUpper;
        public float MouthPressLeft;
        public float MouthPressRight;
        public float MouthLowerDownLeft;
        public float MouthLowerDownRight;
        public float MouthUpperUpLeft;
        public float MouthUpperUpRight;
        public float CheekPuff;
        public float CheekSquintLeft;
        public float CheekSquintRight;
        public float NoseSneerLeft;
        public float NoseSneerRight;
        public float TongueOut;
    }

    public struct LiveLinkTrackingDataBrow
    {
        public float BrowDownLeft;
        public float BrowDownRight;
        public float BrowInnerUp;
        public float BrowOuterUpLeft;
        public float BrowOuterUpRight;
    }

    // Example "full-data" response from the external tracking system.
    public struct LiveLinkTrackingDataStruct
    {
        public LiveLinkTrackingDataEye left_eye;
        public LiveLinkTrackingDataEye right_eye;
        public LiveLinkTrackingDataLips lips;
        public LiveLinkTrackingDataBrow brow;
    }

    // This class contains the overrides for any VRCFT Tracking Data struct functions
    public static class TrackingData
    {
        // This function parses the external module's single-eye data into a VRCFT-Parseable format
        public static void Update(this Eye data, LiveLinkTrackingDataEye external)
        {
            data.Look = new Vector2(external.EyeYaw, external.EyePitch);
            data.Openness = external.EyeBlink;
        }

        // This function parses the external module's full-data data into multiple VRCFT-Parseable single-eye structs
        public static void Update(this EyeTrackingData data, LiveLinkTrackingDataStruct external)
        {
            if (!UnifiedLibManager.EyeEnabled) return;
            MelonLogger.Msg("Left Eye Blink: " + external.left_eye.EyeBlink);
            data.Right.Update(external.left_eye);
            data.Left.Update(external.right_eye);
        }
    }
    public class LiveLinkTrackingModule : ITrackingModule
    {
        public static readonly string[] LiveLinkNames = {
            "EyeBlinkLeft",
            "EyeLookDownLeft",
            "EyeLookInLeft",
            "EyeLookOutLeft",
            "EyeLookUpLeft",
            "EyeSquintLeft",
            "EyeWideLeft",
            "EyeBlinkRight",
            "EyeLookDownRight",
            "EyeLookInRight",
            "EyeLookOutRight",
            "EyeLookUpRight",
            "EyeSquintRight",
            "EyeWideRight",
            "JawForward",
            "JawLeft",
            "JawRight",
            "JawOpen",
            "MouthClose",
            "MouthFunnel",
            "MouthPucker",
            "MouthLeft",
            "MouthRight",
            "MouthSmileLeft",
            "MouthSmileRight",
            "MouthFrownLeft",
            "MouthFrownRight",
            "MouthDimpleLeft",
            "MouthDimpleRight",
            "MouthStretchLeft",
            "MouthStretchRight",
            "MouthRollLower",
            "MouthRollUpper",
            "MouthShrugLower",
            "MouthShrugUpper",
            "MouthPressLeft",
            "MouthPressRight",
            "MouthLowerDownLeft",
            "MouthLowerDownRight",
            "MouthUpperUpLeft",
            "MouthUpperUpRight",
            "BrowDownLeft",
            "BrowDownRight",
            "BrowInnerUp",
            "BrowOuterUpLeft",
            "BrowOuterUpRight",
            "CheekPuff",
            "CheekSquintLeft",
            "CheekSquintRight",
            "NoseSneerLeft",
            "NoseSneerRight",
            "TongueOut",
            "HeadYaw",
            "HeadPitch",
            "HeadRoll",
            "LeftEyeYaw",
            "LeftEyePitch",
            "LeftEyeRoll",
            "RightEyeYaw",
            "RightEyePitch",
            "RightEyeRoll"};

        private static CancellationTokenSource _cancellationToken;

        public UdpClient liveLinkConnection;
        public IPEndPoint liveLinkRemoteEndpoint;

        // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
        public (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            MelonLogger.Msg("Initializing Live Link Tracking module");
            _cancellationToken?.Cancel();
            UnifiedTrackingData.LatestEyeData.SupportsImage = false;
            liveLinkConnection = new UdpClient(42069);
            liveLinkRemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            ReadData(liveLinkConnection, liveLinkRemoteEndpoint);
            return (true, true);
        }

        // This will be run in the tracking thread. This is exposed so you can control when and if the tracking data is updated down to the lowest level.
        public Action GetUpdateThreadFunc()
        {
            _cancellationToken = new CancellationTokenSource();
            return () =>
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    Update();
                    Thread.Sleep(10);
                }
            };
        }

        // The update function needs to be defined separately in case the user is running with the --vrcft-nothread launch parameter
        public void Update()
        {
            LiveLinkTrackingDataStruct newData = ReadData(liveLinkConnection, liveLinkRemoteEndpoint);
            //TrackingData.Update(UnifiedTrackingData.LatestEyeData, newData);

            UnifiedTrackingData.LatestEyeData.Combined.Look = new Vector2((newData.left_eye.EyeYaw + newData.right_eye.EyeYaw)/2, (newData.left_eye.EyePitch + newData.right_eye.EyePitch)/-2);
            
            UnifiedTrackingData.LatestEyeData.Left.Openness = newData.left_eye.EyeBlink;
            UnifiedTrackingData.LatestEyeData.Left.Widen = newData.left_eye.EyeWide;
            //UnifiedTrackingData.LatestEyeData.Left.Squeeze = newData.left_eye.EyeSquint;

            UnifiedTrackingData.LatestEyeData.Right.Openness = newData.right_eye.EyeBlink;
            UnifiedTrackingData.LatestEyeData.Right.Widen = newData.right_eye.EyeWide;
            //UnifiedTrackingData.LatestEyeData.Right.Squeeze = newData.right_eye.EyeSquint;

            //MelonLogger.Msg("LiveLLink Update");
        }

        // A chance to de-initialize everything. This runs synchronously inside main game thread. Do not touch any Unity objects here.
        public void Teardown()
        {
            _cancellationToken.Cancel();
            _cancellationToken.Dispose();
            liveLinkConnection.Close();
            MelonLogger.Msg("LiveLink Teardown");
        }

        public bool SupportsEye => true;
        public bool SupportsLip => true;

        private LiveLinkTrackingDataStruct ReadData(UdpClient liveLinkConnection, IPEndPoint liveLinkRemoteEndpoint)
        {
            Dictionary<string, float> values = new Dictionary<string, float>();

            try
            {
                Byte[] recieveBytes = liveLinkConnection.Receive(ref liveLinkRemoteEndpoint);

                IEnumerable<Byte> trimmedBytes = recieveBytes.Skip(Math.Max(0, recieveBytes.Count() - 244));

                List<List<Byte>> chunkedBytes = trimmedBytes
                    .Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 4)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();

                
                foreach (var item in chunkedBytes.Select((value, i) => new { i, value }))
                {
                    item.value.Reverse();
                    values.Add(LiveLinkNames[item.i], BitConverter.ToSingle(item.value.ToArray(), 0));
                }

                //var lines = values.Select(kvp => kvp.Key + ": " + kvp.Value.ToString());

                //MelonLogger.Msg("This is the message you received " +
                //               string.Join(Environment.NewLine, lines));
                //MelonLogger.Msg("This message was sent from " +
                //                            liveLinkRemoteEndpoint.Address.ToString() +
                //                            " on their port number " +
                //                            liveLinkRemoteEndpoint.Port.ToString());
            }
            catch (Exception e)
            {
                MelonLogger.Msg(e.ToString());
            }

            if (values.Count() == 61)
            {  
                return ProcessData(values);
            }
            else
            {
                return new LiveLinkTrackingDataStruct();
            }
        }

        private LiveLinkTrackingDataStruct ProcessData(Dictionary<string, float> values)
        {
            LiveLinkTrackingDataStruct processedData = new LiveLinkTrackingDataStruct();
            foreach (var field in typeof(LiveLinkTrackingDataEye).GetFields(BindingFlags.Instance |
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.Public))
            {
                string leftName, rightName = "";
                if (field.Name.Contains("Pitch") || field.Name.Contains("Yaw") || field.Name.Contains("Roll"))
                {
                    leftName = "Left" + field.Name;
                    rightName = "Right" + field.Name;
                }
                else
                {
                    leftName = field.Name + "Left";
                    rightName = field.Name + "Right";
                }
                object tempLeft = processedData.left_eye;
                object tempRight = processedData.right_eye;
                field.SetValue(tempLeft, values[leftName]);
                field.SetValue(tempRight, values[rightName]);
                processedData.left_eye = (LiveLinkTrackingDataEye)tempLeft;
                processedData.right_eye = (LiveLinkTrackingDataEye)tempRight;
            }

            //object temp = processedData.left_eye;
            //var field = typeof(LiveLinkTrackingDataEye).GetField("EyeBlink", BindingFlags.Instance |
            //                                                                 BindingFlags.NonPublic |
            //                                                                 BindingFlags.Public);
            //field.SetValue(temp, values["EyeBlinkLeft"]);
            //processedData.left_eye = (LiveLinkTrackingDataEye)temp;
            //field.SetValue(processedData.left_eye, values["EyeBlinkLeft"]);
            //processedData.left_eye.EyeBlink = values["EyeBlinkLeft"];

            //foreach (var field in typeof(LiveLinkTrackingDataLips).GetFields(BindingFlags.Instance |
            //                                                                BindingFlags.NonPublic |
            //                                                                BindingFlags.Public))
            //{
            //    field.SetValue(processedData.lips, values[field.Name]);
            //}

            //foreach (var field in typeof(LiveLinkTrackingDataBrow).GetFields(BindingFlags.Instance |
            //                                                    BindingFlags.NonPublic |
            //                                                    BindingFlags.Public))
            //{
            //    field.SetValue(processedData.brow, values[field.Name]);
            //}

            return processedData;
        }
    }
}
