using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System;

namespace Lockout
{
    [BepInPlugin(LockoutInfo.GUID, LockoutInfo.NAME, LockoutInfo.VERSION)]
    public class LockoutBase : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(LockoutInfo.GUID);
        private static LockoutBase Instance;
        internal static new ManualLogSource Logger { get; private set; }
        public static new LockoutConfig Config { get; internal set; }

        public enum KeyUsage
        {
            No,
            HoldOnHand,
            InInventory,
        }

        private static float timeRatio = 0f;

        private static bool isLocked = false;
        private static bool powerOn = true;
        private static float TimeBeforeLockout { get => LockoutConfig.Instance.timeBeforeLockout; }
        private static float TimeBeforeUnlock { get => LockoutConfig.Instance.timeBeforeUnlock; }

        private static bool CanEnterFireExitDuringLockout { get => LockoutConfig.Instance.canEnterFireExitDuringLockout; }
        private static bool CanEnterFireExitDuringUnlock { get => LockoutConfig.Instance.canEnterFireExitDuringUnlock; }
        private static bool CanExitFireExitDuringLockout { get => LockoutConfig.Instance.canExitFireExitDuringLockout; }
        private static bool CanExitFireExitDuringUnlock { get => LockoutConfig.Instance.canExitFireExitDuringUnlock; }

        private static bool CanEnterMainEntranceDuringLockout { get => LockoutConfig.Instance.canEnterMainEntranceDuringLockout; }
        private static bool CanEnterMainEntranceDuringUnlock { get => LockoutConfig.Instance.canEnterMainEntranceDuringUnlock; }
        private static bool CanExitMainEntranceDuringLockout { get => LockoutConfig.Instance.canExitMainEntranceDuringLockout; }
        private static bool CanExitMainEntranceDuringUnlock { get => LockoutConfig.Instance.canExitMainEntranceDuringUnlock; }

        private static bool CanPowerOffLockout { get => LockoutConfig.Instance.canPowerOffLockout; }

        private static KeyUsage CanUseKey { get => LockoutConfig.Instance.canUseKey; }

        private static readonly DialogueSegment[] lockoutDialog =
        [
            new()
            {
                speakerText = "Facility AI",
                bodyText = "The building is now locked down.",
                waitTime = 3f,
            }
        ];

        private static readonly DialogueSegment[] unlockDialog =
        [
            new()
            {
                speakerText = "Facility AI",
                bodyText = "The building is now unlocked.",
                waitTime = 3f,
            }
        ];

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            Logger = BepInEx.Logging.Logger.CreateLogSource(LockoutInfo.GUID);
            Logger.LogInfo($"Plugin {LockoutInfo.NAME} is loaded!");

            Config = new LockoutConfig(base.Config);

            harmony.PatchAll(typeof(TimeOfDayPatch));
            harmony.PatchAll(typeof(EntranceTeleportPatch));
            harmony.PatchAll(typeof(LockoutConfig));
            harmony.PatchAll(typeof(PlayerControllerBPatch));
            harmony.PatchAll(typeof(GameNetworkManagerPatch));
            harmony.PatchAll(typeof(RoundManagerPatch));
        }

        [HarmonyPatch(typeof(TimeOfDay))]
        internal class TimeOfDayPatch : HarmonyPatch
        {
            [HarmonyPatch("TimeOfDayEvents")]
            [HarmonyPostfix]
            public static void TimeOfDayEventsPostfix(float ___currentDayTime, float ___totalTime)
            {
                timeRatio = ___currentDayTime / ___totalTime;
                /*
                Logger.LogInfo((object)$"TimeOfDayEventsPostfix: Current Day Time: {___currentDayTime}");
                Logger.LogInfo((object)$"TimeOfDayEventsPostfix: Total Time: {___totalTime}");
                Logger.LogInfo((object)$"TimeOfDayEventsPostfix: Time Ratio: {timeRatio}");
                Logger.LogInfo((object)$"TimeOfDayEventsPostfix: PowerOn: {powerOn}");
                Logger.LogInfo((object)$"TimeOfDayEventsPostfix: IsLocked: {isLocked}");
                */
                if (timeRatio < TimeBeforeLockout || (CanPowerOffLockout && !powerOn))
                {
                    if (isLocked)
                    {
                        Logger.LogInfo((object)"TimeOfDayEventsPostfix: Unlock Alert");
                        HUDManager.Instance.ReadDialogue(unlockDialog);
                        isLocked = false;
                    }
                }
                else if (timeRatio < TimeBeforeUnlock)
                {
                    if (!isLocked)
                    {
                        Logger.LogInfo((object)"TimeOfDayEventsPostfix: Lockout Alert");
                        HUDManager.Instance.ReadDialogue(lockoutDialog);
                        isLocked = true;
                    }
                }
                else
                {
                    if (isLocked)
                    {
                        Logger.LogInfo((object)"TimeOfDayEventsPostfix: Unlock Alert");
                        HUDManager.Instance.ReadDialogue(unlockDialog);
                        isLocked = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(EntranceTeleport))]
        internal class EntranceTeleportPatch : HarmonyPatch
        {
            private static bool UseKey()
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                switch (CanUseKey)
                {
                    case KeyUsage.HoldOnHand:
                        bool holdingKey = localPlayer.currentlyHeldObjectServer && localPlayer.currentlyHeldObjectServer is KeyItem;
                        if (holdingKey)
                        {
                            localPlayer.DespawnHeldObject();
                        }
                        Logger.LogInfo($"UseKey: was ({(holdingKey ? "ABLE" : "UNABLE")}) to use currently held key");
                        return holdingKey;
                    case KeyUsage.InInventory:
                        int keyIndex = Array.FindIndex(localPlayer.ItemSlots, item => item && item is KeyItem);
                        bool hasKeyInInventory = keyIndex != -1;
                        if (hasKeyInInventory)
                        {
                            localPlayer.isHoldingObject = true;
                            localPlayer.DestroyItemInSlotAndSync(keyIndex);
                        }
                        Logger.LogInfo($"UseKey: was ({(hasKeyInInventory ? "ABLE" : "UNABLE")}) to use key in inventory got slot {keyIndex}");
                        return hasKeyInInventory;
                    case KeyUsage.No:
                    default:
                        Logger.LogInfo($"UseKey: config DISABLED key usage: {CanUseKey}");
                        return false;
                }
            }
            private static bool CanEnter(bool isMainEntrance)
            {
                if (isLocked)
                {
                    if (isMainEntrance)
                    {
                        return CanEnterMainEntranceDuringLockout;
                    }
                    else
                    {
                        return CanEnterFireExitDuringLockout;
                    }
                }
                else
                {
                    if (isMainEntrance)
                    {
                        return CanEnterMainEntranceDuringUnlock;
                    }
                    else
                    {
                        return CanEnterFireExitDuringUnlock;
                    }
                }
            }
            private static bool CanExit(bool isMainEntrance)
            {
                if (isLocked)
                {
                    if (isMainEntrance)
                    {
                        return CanExitMainEntranceDuringLockout;
                    }
                    else
                    {
                        return CanExitFireExitDuringLockout;
                    }
                }
                else
                {
                    if (isMainEntrance)
                    {
                        return CanExitMainEntranceDuringUnlock;
                    }
                    else
                    {
                        return CanExitFireExitDuringUnlock;
                    }
                }
            }

            [HarmonyPatch("TeleportPlayer")]
            [HarmonyPrefix]
            public static bool TeleportPlayerPrefix(bool ___isEntranceToBuilding, int ___entranceId)
            {
                bool isMainEntrance = ___entranceId == 0;
                bool isEntering = ___isEntranceToBuilding;
                bool allowed = (isEntering ? CanEnter(isMainEntrance) : CanExit(isMainEntrance)) || UseKey();

                Logger.LogInfo($"Player is {(allowed ? "ALLOWED" : "UNALLOWED")} to {(isEntering ? "ENTER" : "EXIT")} through the {(isMainEntrance ? "MAIN ENTRANCE" : "FIRE EXIT")}");

                if (!allowed)
                {
                    HUDManager.Instance.DisplayTip("???", $"{(isEntering ? "ENTERING" : "EXITING")} through the {(isMainEntrance ? "MAIN ENTRANCE" : "FIRE EXIT")} seems blocked", false, false, "LC_Tip1");
                }

                return allowed;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB))]
        internal class PlayerControllerBPatch : HarmonyPatch
        {
            [HarmonyPatch("ConnectClientToPlayerObject")]
            [HarmonyPostfix]
            public static void ConnectClientToPlayerObjectPostfix()
            {
                if (LockoutConfig.IsHost)
                {
                    Logger.LogInfo("ConnectClientToPlayerObjectPostfix: Host is syncing config");
                    LockoutConfig.MessageManager.RegisterNamedMessageHandler($"{LockoutInfo.NAME}_OnRequestConfigSync", LockoutConfig.OnRequestSync);
                    LockoutConfig.Synced = true;

                    return;
                }

                Logger.LogInfo("ConnectClientToPlayerObjectPostfix: Client is requesting config sync");
                LockoutConfig.Synced = false;
                LockoutConfig.MessageManager.RegisterNamedMessageHandler($"{LockoutInfo.NAME}_OnReceiveConfigSync", LockoutConfig.OnReceiveSync);
                LockoutConfig.RequestSync();
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager))]
        internal class GameNetworkManagerPatch : HarmonyPatch
        {
            [HarmonyPatch("StartDisconnect")]
            [HarmonyPostfix]
            public static void StartDisconnectPostfix()
            {
                Logger.LogInfo("StartDisconnectPostfix: Disconnecting");
                LockoutConfig.RevertSync();
            }
        }

        [HarmonyPatch(typeof(RoundManager))]
        internal class RoundManagerPatch : HarmonyPatch
        {
            [HarmonyPatch("Start")]
            [HarmonyPostfix]
            private static void StartPostfix()
            {
                Logger.LogInfo("Start: Registering event handlers");
                RoundManager.Instance.onPowerSwitch.AddListener(OnPowerSwitch);
            }

            [HarmonyPatch("OnDestroy")]
            [HarmonyPostfix]
            private static void OnDestroy()
            {
                Logger.LogInfo("OnDestroy: Unregistering event handlers");
                RoundManager.Instance.onPowerSwitch.RemoveListener(OnPowerSwitch);
            }

            [HarmonyPatch("SetLevelObjectVariables")]
            [HarmonyPrefix]
            private static void SetLevelObjectVariablesPrefix()
            {
                OnPowerSwitch(true);
                isLocked = false;
            }

            private static void OnPowerSwitch(bool on)
            {
                Logger.LogInfo((object)$"OnPowerSwitch: {on}");
                powerOn = on;
            }
        }
    }
}
