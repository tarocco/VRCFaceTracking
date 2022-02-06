using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using MelonLoader;
using ViveSR.anipal.Lip;
using VRCFaceTracking;
using VRCFaceTracking.Params;

namespace VRCFT_Module___LiveLink
{
    // Live Link Single-Eye tracking data
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

    // Live Link lip tracking data
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

    // Live Link brow tracking data
    public struct LiveLinkTrackingDataBrow
    {
        public float BrowDownLeft;
        public float BrowDownRight;
        public float BrowInnerUp;
        public float BrowOuterUpLeft;
        public float BrowOuterUpRight;
    }

    // All Live Link tracking data
    public struct LiveLinkTrackingDataStruct
    {
        public LiveLinkTrackingDataEye left_eye;
        public LiveLinkTrackingDataEye right_eye;
        public LiveLinkTrackingDataLips lips;
        public LiveLinkTrackingDataBrow brow;
    }

    // This class contains the overrides for any VRCFT Tracking Data struct functions
    // This class is unusead right now, probably a much better idea to move what I'm doing in LiveLinkTrackinModule.Update
    // to here.
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
        // The proper names of each ARKit blendshape (Note most eyes are "Eye___[Left/Right]" while
        // pitch/yaw/roll are "[Left/Right]Eye___")
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

        // Starts listening on port 42069 and waits for the first packet to come in to initialize
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

        // Update the face pose every 10ms, this is the same frequency that Pimax and SRanipal use
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
        // Currently doing all data processing in this Update function, should probably move into TrackingData
        public void Update()
        {
            LiveLinkTrackingDataStruct newData = ReadData(liveLinkConnection, liveLinkRemoteEndpoint);
            //TrackingData.Update(UnifiedTrackingData.LatestEyeData, newData);

            // Combined eye tracking
            UnifiedTrackingData.LatestEyeData.Combined.Look = new Vector2((newData.left_eye.EyeYaw + newData.right_eye.EyeYaw) / 2, (newData.left_eye.EyePitch + newData.right_eye.EyePitch) / -2);
            UnifiedTrackingData.LatestEyeData.Combined.Openness = (newData.left_eye.EyeBlink + newData.right_eye.EyeBlink) / -2;
            UnifiedTrackingData.LatestEyeData.Combined.Widen = (newData.left_eye.EyeWide + newData.right_eye.EyeWide) / 2;
            //UnifiedTrackingData.LatestEyeData.Combined.Squeeze = (newData.left_eye.EyeSquint + newData.right_eye.EyeSquint)/2;

            // Left eye tracking
            UnifiedTrackingData.LatestEyeData.Left.Look = new Vector2(newData.left_eye.EyeYaw, -1 * newData.left_eye.EyePitch);
            UnifiedTrackingData.LatestEyeData.Left.Openness = -1*newData.left_eye.EyeBlink;
            UnifiedTrackingData.LatestEyeData.Left.Widen = newData.left_eye.EyeWide;
            //UnifiedTrackingData.LatestEyeData.Left.Squeeze = newData.left_eye.EyeSquint;

            // Right eye tracking
            UnifiedTrackingData.LatestEyeData.Right.Look = new Vector2(newData.right_eye.EyeYaw, -1 * newData.right_eye.EyePitch);
            UnifiedTrackingData.LatestEyeData.Right.Openness = -1*newData.right_eye.EyeBlink;
            UnifiedTrackingData.LatestEyeData.Right.Widen = newData.right_eye.EyeWide;
            //UnifiedTrackingData.LatestEyeData.Right.Squeeze = newData.right_eye.EyeSquint;

            // Lip tracking
            Dictionary<LipShape_v2, float> lipShapes = new Dictionary<LipShape_v2, float>{
                { LipShape_v2.JawRight, newData.lips.JawRight }, // +JawX
                { LipShape_v2.JawLeft, newData.lips.JawLeft }, // -JawX
                { LipShape_v2.JawForward, newData.lips.JawForward },
                { LipShape_v2.JawOpen, newData.lips.JawOpen },
                { LipShape_v2.MouthApeShape, 0 },
                { LipShape_v2.MouthUpperRight, newData.lips.MouthRight }, // +MouthUpper
                { LipShape_v2.MouthUpperLeft, newData.lips.MouthLeft }, // -MouthUpper
                { LipShape_v2.MouthLowerRight, newData.lips.MouthRight }, // +MouthLower
                { LipShape_v2.MouthLowerLeft, newData.lips.MouthLeft }, // -MouthLower
                { LipShape_v2.MouthUpperOverturn, newData.lips.MouthShrugUpper },
                { LipShape_v2.MouthLowerOverturn, newData.lips.MouthShrugLower },
                { LipShape_v2.MouthPout, 0 },
                { LipShape_v2.MouthSmileRight, newData.lips.MouthSmileRight }, // +SmileSadRight
                { LipShape_v2.MouthSmileLeft, newData.lips.MouthSmileLeft }, // +SmileSadLeft
                { LipShape_v2.MouthSadRight, newData.lips.MouthFrownRight }, // -SmileSadRight
                { LipShape_v2.MouthSadLeft, newData.lips.MouthFrownLeft }, // -SmileSadLeft
                { LipShape_v2.CheekPuffRight, newData.lips.CheekPuff },
                { LipShape_v2.CheekPuffLeft, newData.lips.CheekPuff },
                { LipShape_v2.CheekSuck, 0 },
                { LipShape_v2.MouthUpperUpRight, newData.lips.MouthUpperUpRight },
                { LipShape_v2.MouthUpperUpLeft, newData.lips.MouthUpperUpLeft },
                { LipShape_v2.MouthLowerDownRight, newData.lips.MouthLowerDownRight },
                { LipShape_v2.MouthLowerDownLeft, newData.lips.MouthLowerDownLeft },
                { LipShape_v2.MouthUpperInside, newData.lips.MouthRollUpper },
                { LipShape_v2.MouthLowerInside, newData.lips.MouthRollLower },
                { LipShape_v2.MouthLowerOverlay, 0 },
                { LipShape_v2.TongueLongStep1, newData.lips.TongueOut },
                { LipShape_v2.TongueLongStep2, newData.lips.TongueOut },
                { LipShape_v2.TongueDown, 0 }, // -TongueY
                { LipShape_v2.TongueUp, 0 }, // +TongueY
                { LipShape_v2.TongueRight, 0 }, // +TongueX
                { LipShape_v2.TongueLeft, 0 }, // -TongueX
                { LipShape_v2.TongueRoll, 0 },
                { LipShape_v2.TongueUpLeftMorph, 0 },
                { LipShape_v2.TongueUpRightMorph, 0 },
                { LipShape_v2.TongueDownLeftMorph, 0 },
                { LipShape_v2.TongueDownRightMorph, 0 },
            };

            // Brow tracking??

            UnifiedTrackingData.LatestLipShapes = lipShapes;
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

        // Read the data from the LiveLink UDP stream and place it into a LiveLinkTrackingDataStruct
        private LiveLinkTrackingDataStruct ReadData(UdpClient liveLinkConnection, IPEndPoint liveLinkRemoteEndpoint)
        {
            Dictionary<string, float> values = new Dictionary<string, float>();

            try
            {
                // Grab the packet
                Byte[] recieveBytes = liveLinkConnection.Receive(ref liveLinkRemoteEndpoint);

                // There is a bunch of static data at the beginning of the packet, is may be variable length because it includes phone name
                // So grab the last 244 bytes of the packet sent using some Linq magic, since that's where our blendshapes live
                IEnumerable<Byte> trimmedBytes = recieveBytes.Skip(Math.Max(0, recieveBytes.Count() - 244));

                // More Linq magic, this splits our 244 bytes into 61, 4-byte chunks which we can then turn into floats
                List<List<Byte>> chunkedBytes = trimmedBytes
                    .Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 4)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();

                // Process each float in out chunked out list
                foreach (var item in chunkedBytes.Select((value, i) => new { i, value }))
                {
                    // First, reverse the list because the data will be in big endian, then convert it to a float
                    item.value.Reverse();
                    values.Add(LiveLinkNames[item.i], BitConverter.ToSingle(item.value.ToArray(), 0));
                }

                //// Logging to spam console with each blendshape every update
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

            // Check that we got all 61 values before we go processing things
            if (values.Count() == 61)
            {  
                return ProcessData(values);
            }
            else
            {
                // If tracking cuts out, it might be a good idea for it to just freeze in place instead of everything getting set to
                // an empty struct
                return new LiveLinkTrackingDataStruct();
            }
        }

        // This is all terrible, I am almost certain that there is no need to use relfection for any of this
        private LiveLinkTrackingDataStruct ProcessData(Dictionary<string, float> values)
        {
            LiveLinkTrackingDataStruct processedData = new LiveLinkTrackingDataStruct();

            // For eacch of the eye tracking blendshapes
            foreach (var field in typeof(LiveLinkTrackingDataEye).GetFields(BindingFlags.Instance |
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.Public))
            {
                // Eye pitch, yaw, and roll have left/right at the start while all other eye fields have them at the end for some reason.
                // I could just rename my blendshapes to make this a lot easier but I wanted them to still match ARKit names
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

                // Values have to be boxed before they're set otherwise it won't actually get written
                object tempLeft = processedData.left_eye;
                object tempRight = processedData.right_eye;
                field.SetValue(tempLeft, values[leftName]);
                field.SetValue(tempRight, values[rightName]);
                processedData.left_eye = (LiveLinkTrackingDataEye)tempLeft;
                processedData.right_eye = (LiveLinkTrackingDataEye)tempRight;
            }

            // For each of the lip tracking blendshapes
            foreach (var field in typeof(LiveLinkTrackingDataLips).GetFields(BindingFlags.Instance |
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.Public))
            {
                // Box them and set them
                object temp = processedData.lips;
                field.SetValue(temp, values[field.Name]);
                processedData.lips = (LiveLinkTrackingDataLips)temp;
            }

            // For each of the brow tracking blendshapes
            foreach (var field in typeof(LiveLinkTrackingDataBrow).GetFields(BindingFlags.Instance |
                                                                BindingFlags.NonPublic |
                                                                BindingFlags.Public))
            {
                // Box them and set them
                object temp = processedData.brow;
                field.SetValue(processedData.brow, values[field.Name]);
                processedData.brow = (LiveLinkTrackingDataBrow)temp;
            }

            return processedData;
        }
    }
}
