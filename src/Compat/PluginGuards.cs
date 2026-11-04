using BepInEx.Bootstrap;

namespace Husnisse.Compat
{
    /// <summary>
    /// Startup refusals. A storage mod that runs beside another storage mod on the same chests is how
    /// items vanish, so the guard is a hard stop with a readable reason rather than a warning.
    /// </summary>
    public static class PluginGuards
    {
        public const string QuartermasterGuid = "local.valheim.quartermaster";

        public static bool QuartermasterInstalled()
        {
            return Chainloader.PluginInfos.ContainsKey(QuartermasterGuid);
        }

        public static string RefusalReason()
        {
            if (QuartermasterInstalled())
            {
                return "Quartermaster is installed. Two storage systems managing the same chests " +
                       "will lose items, so Husnisse refuses to run. Remove Quartermaster and restart.";
            }

            return null;
        }
    }
}
