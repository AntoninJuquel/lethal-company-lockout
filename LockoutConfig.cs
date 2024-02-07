using BepInEx.Configuration;
using System;
using Unity.Collections;
using Unity.Netcode;

namespace Lockout
{
    [Serializable]
    public class LockoutConfig : SyncedInstance<LockoutConfig>
    {
        public float timeBeforeLockout = 0.2f;
        public float timeBeforeUnlock = 0.9f;

        public bool canEnterFireExitDuringLockout = false;
        public bool canEnterFireExitDuringUnlock = false;
        public bool canExitFireExitDuringLockout = true;
        public bool canExitFireExitDuringUnlock = true;

        public bool canEnterMainEntranceDuringLockout = false;
        public bool canEnterMainEntranceDuringUnlock = true;
        public bool canExitMainEntranceDuringLockout = false;
        public bool canExitMainEntranceDuringUnlock = true;

        public bool canPowerOffLockout = true;

        public LockoutBase.KeyUsage canUseKey = LockoutBase.KeyUsage.Inventory;

        public string lockoutMessage = "The building is now locked down.";
        public string unlockMessage = "The building is now unlocked.";

        public LockoutConfig(ConfigFile cfg)
        {
            InitInstance(this);

            timeBeforeLockout = cfg.Bind("Time", "Time Before Lockout", 0.2f, "The time of day when the building lockout (0-1)").Value;
            timeBeforeUnlock = cfg.Bind("Time", "Time Before Unlock", 0.9f, "The time of day when the building unlocks (0-1)").Value;

            canEnterFireExitDuringLockout = cfg.Bind("Fire Exit", "Can Enter Fire Exit During Lockout", false, "Can enter the fire exit during lockout").Value;
            canEnterFireExitDuringUnlock = cfg.Bind("Fire Exit", "Can Enter Fire Exit During Unlock", false, "Can enter the fire exit during unlock").Value;
            canExitFireExitDuringLockout = cfg.Bind("Fire Exit", "Can Exit Fire Exit During Lockout", true, "Can exit the fire exit during lockout").Value;
            canExitFireExitDuringUnlock = cfg.Bind("Fire Exit", "Can Exit Fire Exit During Unlock", true, "Can exit the fire exit during unlock").Value;

            canEnterMainEntranceDuringLockout = cfg.Bind("Main Entrance", "Can Enter Main Entrance During Lockout", false, "Can enter the main entrance during lockout").Value;
            canEnterMainEntranceDuringUnlock = cfg.Bind("Main Entrance", "Can Enter Main Entrance During Unlock", true, "Can enter the main entrance during unlock").Value;
            canExitMainEntranceDuringLockout = cfg.Bind("Main Entrance", "Can Exit Main Entrance During Lockout", false, "Can exit the main entrance during lockout").Value;
            canExitMainEntranceDuringUnlock = cfg.Bind("Main Entrance", "Can Exit Main Entrance During Unlock", true, "Can exit the main entrance during unlock").Value;

            canPowerOffLockout = cfg.Bind("Power", "Can Power Off Lockout", true, "Can power off the lockout").Value;

            canUseKey = (LockoutBase.KeyUsage)cfg.Bind("Key", "Can use key", 1, "Can use the key to enter/exit during lockout |0: No|1: Key in inventory slot|2: Key must be held|").Value;

            lockoutMessage = cfg.Bind("Messages", "Lockout Message", "The building is now locked down.", "The message displayed when the building is locked down.").Value;
            unlockMessage = cfg.Bind("Messages", "Unlock Message", "The building is now unlocked.", "The message displayed when the building is unlocked.").Value;

            if (timeBeforeLockout < 0 || timeBeforeLockout > 1)
            {
                LockoutBase.Logger.LogWarning($"Time Before Lockout was set to {timeBeforeLockout}. Setting to default.");
                timeBeforeLockout = 0.2f;
            }

            if (timeBeforeUnlock < 0 || timeBeforeUnlock > 1)
            {
                LockoutBase.Logger.LogWarning($"Time Before Unlock was set to {timeBeforeUnlock}. Setting to default.");
                timeBeforeUnlock = 0.9f;
            }

            if (timeBeforeLockout > timeBeforeUnlock)
            {
                LockoutBase.Logger.LogWarning($"Time Before Lockout was set to {timeBeforeLockout} and is Greater than Time Before Unlock was set to {timeBeforeUnlock}. Setting to default.");
                timeBeforeLockout = 0.2f;
                timeBeforeUnlock = 0.9f;
            }

            if (!Enum.IsDefined(typeof(LockoutBase.KeyUsage), canUseKey))
            {
                LockoutBase.Logger.LogWarning($"Can Use Key was set to {canUseKey}. Setting to default.");
                canUseKey = LockoutBase.KeyUsage.Inventory;
            }

            LockoutBase.Logger.LogInfo("Config loaded.");
            LockoutBase.Logger.LogInfo($"Time Before Lockout: {timeBeforeLockout}");
            LockoutBase.Logger.LogInfo($"Time Before Unlock: {timeBeforeUnlock}");
            LockoutBase.Logger.LogInfo($"Can Enter Fire Exit During Lockout: {canEnterFireExitDuringLockout}");
            LockoutBase.Logger.LogInfo($"Can Enter Fire Exit During Unlock: {canEnterFireExitDuringUnlock}");
            LockoutBase.Logger.LogInfo($"Can Exit Fire Exit During Lockout: {canExitFireExitDuringLockout}");
            LockoutBase.Logger.LogInfo($"Can Exit Fire Exit During Unlock: {canExitFireExitDuringUnlock}");
            LockoutBase.Logger.LogInfo($"Can Enter Main Entrance During Lockout: {canEnterMainEntranceDuringLockout}");
            LockoutBase.Logger.LogInfo($"Can Enter Main Entrance During Unlock: {canEnterMainEntranceDuringUnlock}");
            LockoutBase.Logger.LogInfo($"Can Exit Main Entrance During Lockout: {canExitMainEntranceDuringLockout}");
            LockoutBase.Logger.LogInfo($"Can Exit Main Entrance During Unlock: {canExitMainEntranceDuringUnlock}");
            LockoutBase.Logger.LogInfo($"Can Power Off Lockout: {canPowerOffLockout}");
            LockoutBase.Logger.LogInfo($"Can Use Key: {canUseKey}");
            LockoutBase.Logger.LogInfo($"Lockout Message: {lockoutMessage}");
            LockoutBase.Logger.LogInfo($"Unlock Message: {unlockMessage}");
        }

        public static void RequestSync()
        {
            if (!IsClient) return;

            using FastBufferWriter stream = new(IntSize, Allocator.Temp);
            MessageManager.SendNamedMessage($"{LockoutInfo.NAME}_OnRequestConfigSync", 0uL, stream);
        }
        public static void OnRequestSync(ulong clientId, FastBufferReader _)
        {
            if (!IsHost) return;

            LockoutBase.Logger.LogInfo($"Config sync request received from client: {clientId}");

            byte[] array = SerializeToBytes(Instance);
            int value = array.Length;

            using FastBufferWriter stream = new(value + IntSize, Allocator.Temp);

            try
            {
                stream.WriteValueSafe(in value, default);
                stream.WriteBytesSafe(array);

                MessageManager.SendNamedMessage($"{LockoutInfo.NAME}_OnReceiveConfigSync", clientId, stream);
            }
            catch (Exception e)
            {
                LockoutBase.Logger.LogInfo($"Error occurred syncing config with client: {clientId}\n{e}");
            }
        }
        public static void OnReceiveSync(ulong _, FastBufferReader reader)
        {
            if (!reader.TryBeginRead(IntSize))
            {
                LockoutBase.Logger.LogError("Config sync error: Could not begin reading buffer.");
                return;
            }

            reader.ReadValueSafe(out int val, default);
            if (!reader.TryBeginRead(val))
            {
                LockoutBase.Logger.LogError("Config sync error: Host could not sync.");
                return;
            }

            byte[] data = new byte[val];
            reader.ReadBytesSafe(ref data, val);

            SyncInstance(data);

            LockoutBase.Logger.LogInfo("Successfully synced config with host.");
        }
    }
}
