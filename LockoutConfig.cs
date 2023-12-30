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

            if (timeBeforeLockout < 0 || timeBeforeLockout > 1)
            {
                timeBeforeLockout = 0.2f;
            }

            if (timeBeforeUnlock < 0 || timeBeforeUnlock > 1)
            {
                timeBeforeUnlock = 0.9f;
            }
        }

        public static void RequestSync()
        {
            if (!IsClient) return;

            using FastBufferWriter stream = new(IntSize, Allocator.Temp);
            MessageManager.SendNamedMessage($"{LockoutBase.modName}_OnRequestConfigSync", 0uL, stream);
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

                MessageManager.SendNamedMessage($"{LockoutBase.modName}_OnReceiveConfigSync", clientId, stream);
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
