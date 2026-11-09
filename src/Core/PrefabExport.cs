using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Husnisse.Core
{
    /// <summary>Dumps every loaded container prefab so pack mods can be identified without a decompiler.</summary>
    public static class PrefabExport
    {
        public static string Dump()
        {
            if (ZNetScene.instance == null) return null;

            var lines = new List<string>
            {
                "# Husnisse container prefabs",
                "# name\twidth\theight\tpiece",
            };

            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;

                var container = prefab.GetComponent<Container>();
                if (container == null) continue;

                var piece = prefab.GetComponent<Piece>();
                lines.Add(prefab.name + "\t" + container.m_width + "\t" + container.m_height + "\t" +
                          (piece != null ? piece.m_name : "-"));
            }

            lines.Add("# total: " + (lines.Count - 2));

            var dir = Path.Combine(Paths.BepInExRootPath, "BSBLogs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Husnisse-prefabs.txt");
            File.WriteAllLines(path, lines.OrderBy(l => l), Encoding.UTF8);
            Log.Info("prefab dump written: " + path);
            return path;
        }
    }
}
