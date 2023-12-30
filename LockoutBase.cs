using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;

namespace Lockout
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class LockoutBase : BaseUnityPlugin
    {
        public const string modGUID = "com.github.somindras.lethal-company-lockout";
        public const string modName = "Lockout";
        public const string modVersion = "1.1.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static LockoutBase Instance;

        internal static new ManualLogSource Logger { get; private set; }

        public static new LockoutConfig Config { get; internal set; }

        private static bool isLocked = false;
        private static bool lockoutAlertCalled = false;
        private static bool unlockAlertCalled = false;
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

            Logger = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            Logger.LogInfo($"Plugin {modName} is loaded!");

            Config = new LockoutConfig(base.Config);

            Logger.LogInfo($"Time Before Lockout: {TimeBeforeLockout}");
            Logger.LogInfo($"Time Before Unlock: {TimeBeforeUnlock}");

            harmony.PatchAll(typeof(TimeOfDayPatch));
            harmony.PatchAll(typeof(EntranceTeleportPatch));
            harmony.PatchAll(typeof(LockoutConfig));
            harmony.PatchAll(typeof(PlayerControllerBPatch));
            harmony.PatchAll(typeof(GameNetworkManagerPatch));
        }

        [HarmonyPatch(typeof(TimeOfDay))]
        internal class TimeOfDayPatch : HarmonyPatch
        {
            [HarmonyPatch("TimeOfDayEvents")]
            [HarmonyPostfix]
            public static void TimeOfDayEventsPostfix(ref float ___currentDayTime, ref float ___totalTime)
            {
                float timeRatio = ___currentDayTime / ___totalTime;

                if (timeRatio < TimeBeforeLockout)
                {
                    isLocked = false;
                    lockoutAlertCalled = false;
                    unlockAlertCalled = false;
                }
                else if (timeRatio < TimeBeforeUnlock)
                {
                    if (!lockoutAlertCalled)
                    {
                        Logger.LogInfo((object)"TimeOfDayEventsPostfix: Lockout Alert");
                        HUDManager.Instance.ReadDialogue(lockoutDialog);
                        lockoutAlertCalled = true;
                        isLocked = true;
                    }
                }
                else
                {
                    if (!unlockAlertCalled)
                    {
                        Logger.LogInfo((object)"TimeOfDayEventsPostfix: Unlock Alert");
                        HUDManager.Instance.ReadDialogue(unlockDialog);
                        unlockAlertCalled = true;
                        isLocked = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(EntranceTeleport))]
        internal class EntranceTeleportPatch : HarmonyPatch
        {
            [HarmonyPatch("TeleportPlayer")]
            [HarmonyPrefix]
            public static bool TeleportPlayerPrefix(ref bool ___isEntranceToBuilding, ref int ___entranceId, ref bool ___gotExitPoint)
            {
                Logger.LogInfo((object)$"TeleportPlayerPrefix: Entrance ID: {___entranceId}");
                Logger.LogInfo((object)$"TeleportPlayerPrefix: Is Entrance?: {___isEntranceToBuilding}");
                Logger.LogInfo((object)$"TeleportPlayerPrefix: Got Exit Point?: {___gotExitPoint}");

                bool isMainEntrance = ___entranceId == 0;
                bool isFireExit = !isMainEntrance;
                bool isEntering = ___isEntranceToBuilding;
                bool isExiting = !isEntering;

                if (isLocked)
                {
                    if (isEntering && isMainEntrance)
                    {
                        Logger.LogInfo((object)$"TeleportPlayerPrefix: CanEnterMainEntranceDuringLockout {CanEnterMainEntranceDuringLockout}");
                        if (!CanEnterMainEntranceDuringLockout)
                            HUDManager.Instance.DisplayTip("???", "The access through the Main Entrance seems blocked", false, false, "LC_Tip1");
                        return CanEnterMainEntranceDuringLockout;
                    }
                    else if (isEntering && isFireExit)
                    {
                        Logger.LogInfo((object)$"TeleportPlayerPrefix: CanEnterFireExitDuringLockout {CanEnterFireExitDuringLockout}");
                        if (!CanEnterFireExitDuringLockout)
                            HUDManager.Instance.DisplayTip("???", "The access through the Fire Exit seems blocked", false, false, "LC_Tip1");
                        return CanEnterFireExitDuringLockout;
                    }
                    else if (isExiting && isMainEntrance)
                    {
                        Logger.LogInfo((object)$"TeleportPlayerPrefix: CanExitMainEntranceDuringLockout {CanExitMainEntranceDuringLockout}");
                        if (!CanExitMainEntranceDuringLockout)
                            HUDManager.Instance.DisplayTip("???", "Exiting through the Main Entrance seems blockedt", false, false, "LC_Tip1");
                        return CanExitMainEntranceDuringLockout;
                    }
                    else if (isExiting && isFireExit)
                    {
                        Logger.LogInfo((object)$"TeleportPlayerPrefix: CanExitFireExitDuringLockout {CanExitFireExitDuringLockout}");
                        if (!CanExitFireExitDuringLockout)
                            HUDManager.Instance.DisplayTip("???", "Exiting through the Fire Exit seems blocked", false, false, "LC_Tip1");
                        return CanExitFireExitDuringLockout;
                    }
                }
                else
                {
                    if (isEntering && isMainEntrance)
                    {
                        Logger.LogInfo((object)$"TeleportPlayerPrefix: CanEnterMainEntranceDuringUnlock {CanEnterMainEntranceDuringUnlock}");
                        if (!CanEnterMainEntranceDuringUnlock)
                            HUDManager.Instance.DisplayTip("???", "The access through the Main Entrance seems blocked", false, false, "LC_Tip1");
                        return CanEnterMainEntranceDuringUnlock;
                    }
                    else if (isEntering && isFireExit)
                    {
                        Logger.LogInfo((object)$"TeleportPlayerPrefix: CanEnterFireExitDuringUnlock {CanEnterFireExitDuringUnlock}");
                        if (!CanEnterFireExitDuringUnlock)
                            HUDManager.Instance.DisplayTip("???", "The access through the Fire Exit seems blocked", false, false, "LC_Tip1");
                        return CanEnterFireExitDuringUnlock;
                    }
                    else if (isExiting && isMainEntrance)
                    {
                        Logger.LogInfo((object)$"TeleportPlayerPrefix: CanExitMainEntranceDuringUnlock {CanExitMainEntranceDuringUnlock}");
                        if (!CanExitMainEntranceDuringUnlock)
                            HUDManager.Instance.DisplayTip("???", "Exiting through the Main Entrance seems blocked", false, false, "LC_Tip1");
                        return CanExitMainEntranceDuringUnlock;
                    }
                    else if (isExiting && isFireExit)
                    {
                        Logger.LogInfo((object)$"TeleportPlayerPrefix: CanExitFireExitDuringUnlock {CanExitFireExitDuringUnlock}");
                        if (!CanExitFireExitDuringUnlock)
                            HUDManager.Instance.DisplayTip("???", "Exiting through the Fire Exit seems blocked", false, false, "LC_Tip1");
                        return CanExitFireExitDuringUnlock;
                    }
                }

                return true;
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
                    LockoutConfig.MessageManager.RegisterNamedMessageHandler($"{modName}_OnRequestConfigSync", LockoutConfig.OnRequestSync);
                    LockoutConfig.Synced = true;

                    return;
                }

                Logger.LogInfo("ConnectClientToPlayerObjectPostfix: Client is requesting config sync");
                LockoutConfig.Synced = false;
                LockoutConfig.MessageManager.RegisterNamedMessageHandler($"{modName}_OnReceiveConfigSync", LockoutConfig.OnReceiveSync);
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
    }
}
