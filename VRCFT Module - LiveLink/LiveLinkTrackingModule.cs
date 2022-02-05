using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VRCFaceTracking;
using VRCFaceTracking.Params;

namespace VRCFT_Module___LiveLink
{
    // Example "single-eye" data response.
    public struct LiveLinkTrackingDataEye
    {
        public float eyeBlink;
        public float eyeLookDown;
        public float eyeLookIn;
        public float eyeLookOut;
        public float eyeLookUp;
        public float eyeSquint;
        public float eyeWide;
        public float eyePitch;
        public float eyeYaw;
        public float eyeRoll;
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
        public float TongueOut;
    }

    // Example "full-data" response from the external tracking system.
    public struct LiveLinkTrackingDataStruct
    {
        public LiveLinkTrackingDataEye left_eye;
        public LiveLinkTrackingDataEye right_eye;
        public LiveLinkTrackingDataLips lips;
    }

    // This class contains the overrides for any VRCFT Tracking Data struct functions
    public static class TrackingData
    {
        // This function parses the external module's single-eye data into a VRCFT-Parseable format
        public static void Update(this Eye data, LiveLinkTrackingDataEye external)
        {
            data.Look = new Vector2(external.eyeYaw, external.eyePitch);
            data.Openness = external.eyeBlink;
        }

        // This function parses the external module's full-data data into multiple VRCFT-Parseable single-eye structs
        public static void Update(this EyeTrackingData data, LiveLinkTrackingDataStruct external)
        {
            data.Right.Update(external.left_eye);
            data.Left.Update(external.right_eye);
        }
    }
    public class LiveLinkTrackingModule
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

        public UdpClient liveLinkConnection;
        public IPEndPoint liveLinkRemoteEndpoint;

        // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
        public (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            Console.WriteLine("Initializing Live Link Tracking module");
            liveLinkConnection = new UdpClient(42069);
            liveLinkRemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            ReadData(liveLinkConnection, liveLinkRemoteEndpoint);
            return (true, true);
        }

        // This will be run in the tracking thread. This is exposed so you can control when and if the tracking data is updated down to the lowest level.
        public Action GetUpdateThreadFunc()
        {
            return Update;
        }

        // The update function needs to be defined separately in case the user is running with the --vrcft-nothread launch parameter
        public void Update()
        {
            ReadData(liveLinkConnection, liveLinkRemoteEndpoint);
            Console.WriteLine("LLUpdate");
        }

        // A chance to de-initialize everything. This runs synchronously inside main game thread. Do not touch any Unity objects here.
        public void Teardown()
        {
            liveLinkConnection.Close();
            Console.WriteLine("LLTeardown");
        }

        public bool SupportsEye => true;
        public bool SupportsLip => true;

        private LiveLinkTrackingDataStruct ReadData(UdpClient liveLinkConnection, IPEndPoint liveLinkRemoteEndpoint)
        {
            try
            {
                Byte[] recieveBytes = liveLinkConnection.Receive(ref liveLinkRemoteEndpoint);
                string returnData = Encoding.ASCII.GetString(recieveBytes);

                Console.WriteLine("This is the message you received " +
                               returnData.ToString());
                Console.WriteLine("This message was sent from " +
                                            liveLinkRemoteEndpoint.Address.ToString() +
                                            " on their port number " +
                                            liveLinkRemoteEndpoint.Port.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return new LiveLinkTrackingDataStruct();
        }
    }
}
