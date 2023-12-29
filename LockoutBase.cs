using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Lockout
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class LockoutBase : BaseUnityPlugin
    {
        private const string modGUID = "com.github.somindras.lockout";
        private const string modName = "Lockout";
        private const string modVersion = "1.0.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static LockoutBase Instance;

        internal static ManualLogSource mls;

        private static bool isLockDown = false;
        private static bool lockoutAlertCalled = false;
        private static bool unlockAlertCalled = false;

        private static float timeBeforeLockout = 0.2f;
        private static float timeBeforeUnlock = 0.9f;
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo($"Plugin {modName} is loaded!");

            {
                timeBeforeLockout = Config.Bind("General", "Time Before Lockout", 0.2f, "The time of day when the building lockout (0-1)").Value;
                if (timeBeforeLockout < 0) { timeBeforeLockout = .2f; }

                timeBeforeUnlock = Config.Bind("General", "Time Before Unlock", 0.9f, "The time of day when the building unlocks (0-1)").Value;
                if (timeBeforeUnlock < 0) { timeBeforeUnlock = .9f; }

                mls.LogInfo((object)$"Time Before Lockout: {timeBeforeLockout}");
                mls.LogInfo((object)$"Time Before Unlock: {timeBeforeUnlock}");
            }

            harmony.PatchAll(typeof(EntranceTeleportPatch));
            harmony.PatchAll(typeof(HUDManagerPatch));
            harmony.PatchAll(typeof(TimeOfDayPatch));
            harmony.PatchAll(typeof(StartOfRoundPatch));
        }

        [HarmonyPatch(typeof(HUDManager))]
        internal class HUDManagerPatch : HarmonyPatch
        {
            [HarmonyPatch("DisplayTip")]
            [HarmonyPostfix]
            public static void DisplayTipPostfix(string headerText, string bodyText, bool isWarning = false, bool useSave = false, string prefsKey = "LC_Tip1")
            {
                mls.LogInfo((object)(bodyText ?? ""));
            }
        }

        [HarmonyPatch(typeof(StartOfRound))]
        internal class StartOfRoundPatch : HarmonyPatch
        {
            [HarmonyPatch("Start")]
            [HarmonyPostfix]
            public static void StartPostfix()
            {
                isLockDown = false;
                lockoutAlertCalled = false;
                unlockAlertCalled = false;
            }
        }

        [HarmonyPatch(typeof(EntranceTeleport))]
        internal class EntranceTeleportPatch : HarmonyPatch
        {
            [HarmonyPatch("TeleportPlayer")]
            [HarmonyPrefix]
            public static bool TeleportPlayerPrefix(ref bool ___isEntranceToBuilding, ref int ___entranceId, ref bool ___gotExitPoint)
            {
                mls.LogInfo((object)$"TeleportPlayerPrefix: Entrance ID: {___entranceId}");
                mls.LogInfo((object)$"TeleportPlayerPrefix: Is Entrance?: {___isEntranceToBuilding}");
                mls.LogInfo((object)$"TeleportPlayerPrefix: Got Exit Point?: {___gotExitPoint}");
                if (!___gotExitPoint)
                {
                    mls.LogInfo((object)"TeleportPlayerPrefix: Initial Sweep");
                }
                else if (___entranceId != 0) // Fire Exit
                {
                    if (___isEntranceToBuilding) // Entering
                    {
                        mls.LogInfo((object)"TeleportPlayerPrefix: Fire Exit Denied");
                        HUDManager.Instance.DisplayTip("Access Denied", "You can only open it from inside", false, false, "LC_Tip1");
                        return false;
                    }
                    else // Exiting
                    {
                        mls.LogInfo((object)"TeleportPlayerPrefix: Fire Exit Allowed");
                        return true;
                    }
                }

                if (___entranceId == 0) // Main Entrance
                {
                    if (isLockDown)
                    {
                        mls.LogInfo((object)"TeleportPlayerPrefix: Main Entrance Denied");
                        HUDManager.Instance.DisplayTip("Access Denied", "The Main Entrance is blocked during lockout", false, false, "LC_Tip1");
                    }
                    else
                    {
                        mls.LogInfo((object)"TeleportPlayerPrefix: Main Entrance Allowed");
                    }
                    return !isLockDown;
                }

                return true;
            }

            [HarmonyPatch("FindExitPoint")]
            [HarmonyPostfix]
            public static void FindExitPointPostfix(ref bool ___isEntranceToBuilding, ref int ___entranceId, ref bool ___gotExitPoint, ref bool __result)
            {
                mls.LogInfo((object)$"FindExitPointPostfix: Entrance ID: {___entranceId}");
                mls.LogInfo((object)$"FindExitPointPostfix: Is Entrance?: {___isEntranceToBuilding}");
                mls.LogInfo((object)$"FindExitPointPostfix: Got Exit Point?: {___gotExitPoint}");
                if (!___gotExitPoint)
                {
                    mls.LogInfo((object)"FindExitPointPostfix: Initial Sweep");
                }
                else if (___entranceId != 0) // Fire Exit
                {
                    if (___isEntranceToBuilding) // Entering
                    {
                        mls.LogInfo((object)"FindExitPointPostfix: Fire Exit Denied");
                        HUDManager.Instance.DisplayTip("Access Denied", "You can only open it from inside", false, false, "LC_Tip1");
                        __result = false;
                    }
                    else // Exiting
                    {
                        mls.LogInfo((object)"FindExitPointPostfix: Fire Exit Allowed");
                        __result = true;
                    }
                }
                if (___entranceId == 0) // Main Entrance
                {
                    if (isLockDown)
                    {
                        mls.LogInfo((object)"FindExitPointPostfix: Main Entrance Denied");
                        HUDManager.Instance.DisplayTip("Access Denied", "The Main Entrance is blocked during lockout", false, false, "LC_Tip1");
                    }
                    else
                    {
                        mls.LogInfo((object)"FindExitPointPostfix: Main Entrance Allowed");
                    }
                    __result = !isLockDown;
                }
            }
        }

        [HarmonyPatch(typeof(TimeOfDay))]
        internal class TimeOfDayPatch : HarmonyPatch
        {
            [HarmonyPatch("TimeOfDayEvents")]
            [HarmonyPostfix]
            public static void TimeOfDayEventsPostfix(ref float ___currentDayTime, ref float ___totalTime)
            {
                float timeRatio = ___currentDayTime / ___totalTime;
                if (timeRatio > timeBeforeLockout && timeRatio < timeBeforeLockout + .1f)
                {
                    if (!lockoutAlertCalled)
                    {
                        mls.LogInfo((object)"TimeOfDayEventsPostfix: Lockout Alert");
                        HUDManager.Instance.DisplayTip("Lockout", "The building is now lockout, you can only EXIT through the Fire Exit", false, false, "LC_Tip1");
                        lockoutAlertCalled = true;
                        isLockDown = true;
                    }
                }
                else if (timeRatio > timeBeforeUnlock && timeRatio < timeBeforeUnlock + .1f)
                {
                    if (!unlockAlertCalled)
                    {
                        mls.LogInfo((object)"TimeOfDayEventsPostfix: Unlock Alert");
                        HUDManager.Instance.DisplayTip("Unlock", "The building is now unlocked", false, false, "LC_Tip1");
                        unlockAlertCalled = true;
                        isLockDown = false;
                    }
                }
                else
                {
                    lockoutAlertCalled = false;
                    unlockAlertCalled = false;
                }
            }
        }
    }
}
