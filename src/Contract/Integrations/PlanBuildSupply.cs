using Husnisse.Compat;
using Husnisse.Core;

namespace Husnisse.Contract.Integrations
{
    /// <summary>
    /// PlanBuild asks one question — "which inventories belong to this player?" — and uses the same
    /// list for both halves of its work: the colour of a blueprint's cost and the materials it takes
    /// when the blueprint is placed. The take half is `void`, so an entry in that list that can count
    /// but not remove does not fail a placement; it makes the blueprint free.
    ///
    /// A claim entry therefore cannot exist before `TryConsume`, and `TryConsume` cannot be honest
    /// from the client alone: a container's ZDO belongs to the server (or to whoever has it open), so
    /// a client-side list entry that removed items would be undone by the next ownership write.
    ///
    /// 0.4 detects and explains; 0.5 ports the adapter from Quartermaster_Integrations (our code)
    /// with the claim as its data source.
    /// </summary>
    public static class PlanBuildBridge
    {
        public static string State { get; private set; } = "absent";

        public static bool Deferred
        {
            get { return State == "deferred-readonly"; }
        }

        public static void Bind()
        {
            State = "absent";

            if (!PluginGuards.Installed(PluginGuards.PlanBuildGuid)) return;

            if (!StorageContract.CanConsume)
            {
                State = "deferred-readonly";
                Log.Info("[Contract] PlanBuild is installed; its blueprint-cost adapter is deferred to 0.5. " +
                         "PlanBuild uses one inventory list for the cost display and for the take, so a " +
                         "read-only entry that counted the claim would place blueprints for free.");
                return;
            }

            State = "ready";
        }
    }
}
