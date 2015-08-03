﻿using AdjustSdk;
using AdjustSdk.Pcl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace AdjustSdk
{
    /// <summary>
    ///  The main interface to Adjust.
    ///  Use the methods of this class to tell Adjust about the usage of your app.
    ///  See the README for details.
    /// </summary>
    public class Adjust
    {
        private static readonly DeviceUtil DeviceUtil = new UtilWS();
        private static readonly AdjustInstance AdjustInstance = new AdjustInstance();

        private static bool firstVisibilityChanged = true;

        /// <summary>
        ///  Tell Adjust that the application is activated (brought to foreground) or deactivated (sent to background).
        /// </summary>
        private static void VisibilityChanged(CoreWindow sender, VisibilityChangedEventArgs args)
        {
            if (firstVisibilityChanged)
            {
                firstVisibilityChanged = false;
                return;
            }
            if (args.Visible)
            {
                AdjustInstance.ApplicationActivated();
            }
            else
            {
                AdjustInstance.ApplicationDeactivated();
            }
        }

        public static void SetupLogging(Action<String> logDelegate, LogLevel? logLevel = null)
        {
            LogConfig.SetupLogging(logDelegate, logLevel);
        }

        /// <summary>
        ///  Tell Adjust that the application was launched.
        ///
        ///  This is required to initialize Adjust. Call this in the Application_Launching
        ///  method of your Windows.UI.Xaml.Application class.
        /// </summary>
        /// <param name="adjustConfig">
        ///   The object that configures the adjust SDK. <seealso cref="AdjustConfig"/>
        /// </param>
        public static void ApplicationLaunching(AdjustConfig adjustConfig)
        {
            AdjustInstance.ApplicationLaunching(adjustConfig, DeviceUtil);
            try
            {
                Window.Current.CoreWindow.VisibilityChanged += VisibilityChanged;
            }
            catch (Exception)
            {
                AdjustFactory.Logger.Debug("Not possible to detect automatically if the app goes to the background");
            }

        }
        
        /// <summary>
        ///  Tell Adjust that the application is activated (brought to foreground).
        ///
        ///  This is used to keep track of the current session state.
        ///  This should only be used if the VisibilityChanged mechanism doesn't work
        /// </summary>
        public static void ApplicationActivated()
        {
            AdjustInstance.ApplicationActivated();
        }

        /// <summary>
        ///  Tell Adjust that the application is deactivated (sent to background).
        ///
        ///  This is used to calculate session attributes like session length and subsession count.
        ///  This should only be used if the VisibilityChanged mechanism doesn't work
        /// </summary>
        public static void ApplicationDeactivated()
        {
            AdjustInstance.ApplicationDeactivated();
        }
                
        /// <summary>
        ///  Tell Adjust that a particular event has happened.
        /// </summary>
        /// <param name="adjustEvent">
        ///  The object that configures the event. <seealso cref="AdjustEvent"/>
        /// </param>
        public static void TrackEvent(AdjustEvent adjustEvent)
        {
            AdjustInstance.TrackEvent(adjustEvent);
        }
        
        /// <summary>
        /// Enable or disable the adjust SDK
        /// </summary>
        /// <param name="enabled">The flag to enable or disable the adjust SDK</param>
        public static void SetEnabled(bool enabled)
        {
            AdjustInstance.SetEnabled(enabled);
        }

        /// <summary>
        /// Check if the SDK is enabled or disabled
        /// </summary>
        /// <returns>true if the SDK is enabled, false otherwise</returns>
        public static bool IsEnabled()
        {
            return AdjustInstance.IsEnabled();
        }

        /// <summary>
        /// Puts the SDK in offline or online mode
        /// </summary>
        /// <param name="enabled">The flag to enable or disable the adjust SDK</param>
        public static void SetOfflineMode(bool offlineMode)
        {
            AdjustInstance.SetOfflineMode(offlineMode);
        }

        /// <summary>
        /// Read the URL that opened the application to search for
        /// an adjust deep link
        /// </summary>
        /// <param name="url">The url that open the application</param>
        public static void AppWillOpenUrl(Uri uri)
        {
            AdjustInstance.AppWillOpenUrl(uri);
        }
        /*
        /// <summary>
        /// Special method used by SDK wrappers
        /// </summary>
        /// <param name="sdkPrefix">The SDK prefix to be added</param>
        public static void SetSdkPrefix(string sdkPrefix)
        {
            AdjustApi.SetSdkPrefix(sdkPrefix);
        }

        /// <summary>
        /// Delegate method to get the log messages of the adjust SDK
        /// </summary>
        /// <param name="logDelegate"></param>
        public static void SetLogDelegate(Action<String> logDelegate)
        {
            AdjustApi.SetLogDelegate(logDelegate);
        }
        */
    }
}