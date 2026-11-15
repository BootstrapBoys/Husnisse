using Husnisse.Compat;
using Husnisse.Core;

namespace Husnisse.Contract.Integrations
{
    /// <summary>
    /// EpicLoot's inventory-provider API. EpicLoot sums every registered provider into its "have"
    /// numbers **and** calls the provider's remove callbacks to pay a cost — and that payment takes
    /// from the player's pockets first, then asks the provider for the rest. A provider that counted
    /// the claim but could not take from it would therefore advertise materials it cannot pay with,
    /// and the player would lose the pocket half of a cost for nothing.
    ///
    /// There is a second reason it cannot land before 0.5: the API is synchronous and client-side,
    /// while a claim container's ZDO belongs to the server or to whoever has it open. A client can
    /// read the chest and cannot write it, so the spend needs the server round trip that `TryConsume`
    /// introduces. Registering now would be a number with no honest way to pay it.
    ///
    /// So 0.4 detects and explains; 0.5 registers the provider on top of `TryConsume`. The
    /// incompatible-mod refusal is live now, because it is about what will be registered.
    /// </summary>
    public static class EpicLootProvider
    {
        public static string State { get; private set; } = "absent";

        public static bool Deferred
        {
            get { return State == "deferred-readonly"; }
        }

        public static void Bind()
        {
            State = "absent";

            if (!PluginGuards.Installed(PluginGuards.EpicLootGuid)) return;

            if (PluginGuards.Installed(PluginGuards.EpicLootContainerBridgeGuid))
            {
                // Both would answer the same question and EpicLoot adds the answers together, so
                // every total would double. Only one may register, and it is not going to be both.
                State = "refused-bridge";
                Log.Warn("[Contract] EpicLoot is installed with EpicLootContainerBridge, which registers its own " +
                         "inventory provider. Husnisse never registers a second one beside it: EpicLoot adds " +
                         "providers together, so two would double every total. Run one of the two.");
                return;
            }

            if (!StorageContract.CanConsume)
            {
                State = "deferred-readonly";
                Log.Info("[Contract] EpicLoot is installed; its inventory provider is deferred to 0.5, because a " +
                         "provider that counts the claim must also be able to pay from it, and that spend is the " +
                         "server-authoritative `TryConsume` this build does not have. counts stay pocket-only in " +
                         "EpicLoot until then.");
                return;
            }

            State = "ready";
        }
    }
}
