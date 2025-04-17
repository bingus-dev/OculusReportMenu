// OculusReportMenu
// (C) Copyright 2024 - 2025 binx
// MIT License

// #define BUILD_WINDOWS
// #define BUILD_LINUX

using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using GorillaNetworking;
using GorillaLocomotion;
using BepInEx.Configuration;
using Valve.VR;
using System.Collections;
using OculusReportMenu.Patches;

#if (!BUILD_LINUX && !BUILD_WINDOWS)
    #error "No build target defined."
#endif

namespace OculusReportMenu {
    public class ModInfo {
        /*
        *   Why two mod infos?
        *   It's just for debugging purposes
        */
#if (BUILD_WINDOWS)
        public const string UUID = "kingbingus.oculusreportmenu.win";
        public const string Name = "orm-windows";
        public const string Version = "1.2.1";
#else if (BUILD_LINUX)
        public const string UUID = "kingbingus.oculusreportmenu.linux";
        public const string Name = "orm-linux";
        public const string Version = "1.2.1";
    }

    [BepInPlugin(ModInfo.UUID, ModInfo.Name, ModInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
#if (BUILD_WINDOWS)
        // custom stuff
        internal static ConfigEntry<string> OpenButton1, OpenButton2;
#endif

        // base things
        internal static bool Menu, ModEnabled;
        internal static GorillaMetaReport MetaReportMenu;

        internal static bool usingSteamVR;

        internal static MethodInfo CheckDistance, CheckReportSubmit;

        bool IsNull(object thing) => thing != null ? false : true;

        void Update()
        {
            if (IsNull(CheckDistance) || IsNull(CheckDistance))
            {
                CheckDistance = typeof(GorillaMetaReport).GetMethod("CheckDistance", BindingFlags.NonPublic | BindingFlags.Instance);
                CheckReportSubmit = typeof(GorillaMetaReport).GetMethod("CheckReportSubmit", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (Menu)
            {
                // hide the fact that they're in report menu to prevent comp cheating
                GTPlayer.Instance.disableMovement = false;
                GTPlayer.Instance.inOverlay = false;

                // get stuff
                GameObject occluder = GameObject.Find("Miscellaneous Scripts/MetaReporting/ReportOccluder");// (GameObject)Traverse.Create(typeof(GorillaMetaReport)).Field("occluder").GetValue()
                GameObject metaLeftHand = GameObject.Find("Miscellaneous Scripts/MetaReporting/CollisionRB/LeftHandParent"); 
                GameObject metaRightHand = GameObject.Find("Miscellaneous Scripts/MetaReporting/CollisionRB/RightHandParent");

                occluder.transform.position = GorillaTagger.Instance.mainCamera.transform.position;
                metaRightHand.transform.SetPositionAndRotation(GTPlayer.Instance.rightControllerTransform.position, GTPlayer.Instance.rightControllerTransform.rotation);
                metaLeftHand.transform.SetPositionAndRotation(GTPlayer.Instance.leftControllerTransform.position, GTPlayer.Instance.leftControllerTransform.rotation);

                CheckDistance.Invoke(MetaReportMenu, null);
                CheckReportSubmit.Invoke(MetaReportMenu, null);
            }
            else if (GetControllerPressed() && ModEnabled) { ShowMenu(); }
        }

        internal bool GetControllerPressed() { 
#if (BUILD_WINDOWS) 
                return (CheckButtonPressedStatus(OpenButton1) && CheckButtonPressedStatus(OpenButton2)) | Keyboard.current.tabKey.wasPressedThisFrame;
#else if (BUILD_LINUX)
                return (ControllerInputPoller.instance.leftControllerSecondaryButton && ControllerInputPoller.instance.rightControllerSecondaryButton) | Keyboard.current.tabKey.wasPressedThisFrame;
#endif
        }

        internal static void ShowMenu()
        {
            if (!Menu)
            {
                MetaReportMenu.gameObject.SetActive(true);
                MetaReportMenu.enabled = true;

                typeof(GorillaMetaReport).GetMethod("StartOverlay", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(MetaReportMenu, null);
                Menu = true;
            }
        }

        public void OnEnable() { ModEnabled = true; HarmonyPatches.ApplyHarmonyPatches(this); }
        public void OnDisable() { ModEnabled = false; HarmonyPatches.RemoveHarmonyPatches(); }

#if (BUILD_WINDOWS)
        void Awake()
        {
            OpenButton1 = Config.Bind("Keybinds",
                                      "OpenButton1",
                                      "LS",
                                      "One of the buttons you use to open ORM (NAN for none)");

            OpenButton2 = Config.Bind("Keybinds",
                                      "OpenButton2",
                                      "RJ",
                                      "One of the buttons you use to open ORM (NAN for none)");
        }

        // checks for the right key

        internal static bool CheckButtonPressedStatus(ConfigEntry<string> thisEntry)
        {
            bool temporarySClick = false;

            switch (thisEntry.Value.ToUpper())
            {
                // left hand
                case "LP": return ControllerInputPoller.instance.leftControllerPrimaryButton;
                case "LS": return ControllerInputPoller.instance.leftControllerSecondaryButton;
                case "LT": return ControllerInputPoller.instance.leftControllerIndexFloat > 0.5f;
                case "LG": return ControllerInputPoller.instance.leftControllerGripFloat > 0.5f;
                case "LJ":
                    if (usingSteamVR)
                        temporarySClick = SteamVR_Actions.gorillaTag_LeftJoystickClick.state;
                    else
                        InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out temporarySClick);

                    return temporarySClick;

                // right hand
                case "RP": return ControllerInputPoller.instance.rightControllerPrimaryButton;
                case "RS": return ControllerInputPoller.instance.rightControllerSecondaryButton;
                case "RT": return ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f;
                case "RG": return ControllerInputPoller.instance.rightControllerGripFloat > 0.5f;
                case "RJ":
                    if (usingSteamVR)
                        temporarySClick = SteamVR_Actions.gorillaTag_RightJoystickClick.state;
                    else
                        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out temporarySClick);

                    return temporarySClick;

                case "NAN":
                    return true;
            }

            return false;
        }
    }
#endif

    [HarmonyPatch(typeof(GorillaMetaReport), "Teardown")] // GorillaMetaReport.Teardown() is called when X is pressed
    public class CheckMenuClosed
    {
        static void Postfix()
        {
            Plugin.Menu = false;
        }
    }

    [HarmonyPatch(typeof(GorillaMetaReport), "Start")] // Getting the Script when it starts
    public class CheckMenuStart
    {
        static void Postfix(GorillaMetaReport __instance) //has to be called this
        {
            Plugin.MetaReportMenu = __instance;

            Plugin.CheckDistance = typeof(GorillaMetaReport).GetMethod("CheckDistance", BindingFlags.NonPublic | BindingFlags.Instance);
            Plugin.CheckReportSubmit = typeof(GorillaMetaReport).GetMethod("CheckReportSubmit", BindingFlags.NonPublic | BindingFlags.Instance);       
        }
    }

    [HarmonyPatch(typeof(GorillaMetaReport), "Update")] // when gorilla tag for SteamVR detects this it automatically closes it for some reason, this fixes that problem
    public class ForceDontSetHandsManually
    {
        static void Postfix()
        {
            GTPlayer.Instance.InReportMenu = false;
        }
    }

    [HarmonyPatch(typeof(GorillaComputer), "Initialise")]
    public class GetPlayfabGameVersionPatch
    {
        static void Postfix()
        {
#if (BUILD_WINDOWS)
            if (PlayFabAuthenticator.instance.platform.PlatformTag.ToLower().Contains("steam"))
            {
                Plugin.usingSteamVR = true;
            }
#else if (BUILD_LINUX)
            Plugin.usingSteamVR = true;
#endif
        }
    }
}