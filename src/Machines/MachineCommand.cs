using System;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Flow;
using Husnisse.Net;
using UnityEngine;

namespace Husnisse.Machines
{
    /// <summary>
    /// `husnisse_machines` — what the base's machines are doing, and the three things a person needs
    /// to be able to do to one from a console: pause it, ask the nisse to feed it now, ask the nisse
    /// to empty it now.
    ///
    /// The forced errands exist because a gate cannot wait for a fire to burn down or a smelter's
    /// timer to come round without making the scenario flaky. They go through the same planner, the
    /// same errand and the same visible walk as the automatic path; the only difference is who chose
    /// the moment.
    /// </summary>
    public static class MachineCommand
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_machines",
                "Show the claim's machines and their needs: husnisse_machines | pause <prefab> <on|off> [index] | feed <prefab> [index] | collect <prefab> [index].",
                delegate(Terminal.ConsoleEventArgs args) { Run(args); });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var sub = args.Args.Length > 1 ? args.Args[1].ToLowerInvariant() : "status";

            if (sub == "pause")
            {
                Pause(args);
                return;
            }

            if (sub == "feed" || sub == "collect")
            {
                Force(args, sub);
                return;
            }

            Status(args);
        }

        private static void Status(Terminal.ConsoleEventArgs args)
        {
            var claims = 0;
            var machines = 0;
            var paused = 0;
            var ready = 0;

            foreach (var claim in ClaimRegistry.All())
            {
                if (claim == null || claim.Bowl == null) continue;
                claims++;

                foreach (var machine in MachineScan.All(claim))
                {
                    machines++;
                    if (machine.Paused) paused++;
                    if (machine.Ready() > 0) ready++;
                    Emit(args, "machine claim=" + claim.BowlId + " " + machine.Describe());
                }
            }

            Emit(args, "machines total=" + machines + " claims=" + claims + " paused=" + paused +
                       " ready=" + ready + " tending=" + (HusnisseConfig.TendMachines != null && HusnisseConfig.TendMachines.Value) +
                       " fuel_cap=" + (HusnisseConfig.MachineFuelPercent != null ? HusnisseConfig.MachineFuelPercent.Value : 0) +
                       " input_cap=" + (HusnisseConfig.MachineInputPercent != null ? HusnisseConfig.MachineInputPercent.Value : 0) +
                       " " + MachineRunner.Describe());
        }

        private static void Pause(Terminal.ConsoleEventArgs args)
        {
            if (args.Args.Length < 4)
            {
                Emit(args, "usage: husnisse_machines pause <prefab> <on|off> [index]");
                return;
            }

            var prefab = args.Args[2];
            var on = args.Args[3].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                     args.Args[3].Equals("1", StringComparison.Ordinal);
            var index = 0;
            if (args.Args.Length > 4) int.TryParse(args.Args[4], out index);

            var claim = Home(out var player);
            if (claim == null)
            {
                Emit(args, "machines: no claim here");
                return;
            }

            var machine = MachineScan.Find(claim, prefab, index);
            if (machine == null)
            {
                Emit(args, "machines: no " + prefab + "[" + index + "] in claim " + claim.BowlId);
                return;
            }

            var playerId = player != null ? player.GetPlayerID() : 0L;
            RuleRpc.SendMachinePause(machine.Position, playerId, on);

            Emit(args, "machines pause prefab=" + prefab + " index=" + index + " paused=" + on +
                       " at=" + Machine.Where(machine.Position) + " claim=" + claim.BowlId + " player=" + playerId);
        }

        private static void Force(Terminal.ConsoleEventArgs args, string sub)
        {
            if (args.Args.Length < 3)
            {
                Emit(args, "usage: husnisse_machines " + sub + " <prefab> [index]");
                return;
            }

            var prefab = args.Args[2];
            var index = 0;
            if (args.Args.Length > 3) int.TryParse(args.Args[3], out index);

            var claim = Home(out var player);
            if (claim == null)
            {
                Emit(args, "machines: no claim here");
                return;
            }

            var machine = MachineScan.Find(claim, prefab, index);
            if (machine == null)
            {
                Emit(args, "machines: no " + prefab + "[" + index + "] in claim " + claim.BowlId);
                return;
            }

            MachineJob job;
            if (sub == "collect")
            {
                if (machine.Ready() <= 0)
                {
                    Emit(args, "machines: " + prefab + "[" + index + "] has nothing ready");
                    return;
                }

                job = new MachineJob
                {
                    Machine = machine,
                    Kind = MachineJobKind.Collect,
                    Amount = machine.Ready(),
                    Why = "forced",
                };
            }
            else
            {
                job = MachineScan.PlanFeed(claim, machine, player != null ? player.transform.position : claim.Center);
                if (job == null)
                {
                    Emit(args, "machines: " + prefab + "[" + index + "] wants nothing the claim can give");
                    return;
                }
                job.Why = "forced";
            }

            var nisse = NisseFactory.EnsureNisse(claim.Bowl);
            if (nisse == null)
            {
                Emit(args, "machines: no nisse in claim " + claim.BowlId);
                return;
            }

            if (MachineRunner.Has(claim))
            {
                Emit(args, "machines: the nisse is already on an errand (" + MachineRunner.Describe() + ")");
                return;
            }

            var started = MachineRunner.Start(claim, nisse, job, true);
            Emit(args, "machines " + sub + " prefab=" + prefab + " index=" + index +
                       " started=" + started + " " + job.Describe());
        }

        /// <summary>The claim to work on: the one the player stands in, else the first one there is.</summary>
        private static Claim Home(out Player player)
        {
            player = Player.m_localPlayer;
            if (player != null)
            {
                var claim = ClaimRegistry.FindFor(player.transform.position);
                if (claim != null && claim.Bowl != null) return claim;
            }

            foreach (var claim in ClaimRegistry.All())
            {
                if (claim != null && claim.Bowl != null) return claim;
            }
            return null;
        }

        private static void Emit(Terminal.ConsoleEventArgs args, string line)
        {
            Log.Info(line);
            args?.Context?.AddString(line);
            DiagnosticsFile.Line(line);
        }
    }
}
