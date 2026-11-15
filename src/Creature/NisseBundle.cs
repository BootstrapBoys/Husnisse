using System.Collections.Generic;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// The visible load a nisse carries on a supply errand: two or three of the actual materials,
    /// held in front of its chest, on every peer that can see it.
    ///
    /// The props are local objects on each machine, not networked entities. The server owns the
    /// errand and tells the peers what it picked up (a list of shared names); every peer builds the
    /// same little pile on its own copy of the nisse and clears it when the delivery lands. This is
    /// the same shape as the threshold fade: a visual the server orders, played where it can be
    /// seen, instead of an effect that runs where nobody is looking.
    /// </summary>
    public class NisseBundle : MonoBehaviour
    {
        public const string BundleRpc = "Husnisse_Bundle";

        /// <summary>Three props read as a load; ten read as a bug.</summary>
        private const int MaxProps = 3;

        /// <summary>Nothing larger than a fist is held, whatever the item is.</summary>
        private const float PropSize = 0.26f;

        private readonly List<GameObject> _props = new List<GameObject>();
        private ZNetView _view;

        public bool Showing => _props.Count > 0;

        private void Awake()
        {
            _view = GetComponent<ZNetView>();
            if (_view != null) _view.Register<string>(BundleRpc, RPC_Bundle);
        }

        private void OnDestroy()
        {
            Clear();
        }

        /// <summary>Owner side: show the load here and on every peer.</summary>
        public void Show(IList<string> sharedNames)
        {
            if (sharedNames == null || sharedNames.Count == 0) return;

            Build(sharedNames);
            Broadcast(Pack(sharedNames));
        }

        /// <summary>Owner side: the load is gone (handed over, dropped, or the errand ended).</summary>
        public void Hide()
        {
            if (!Showing) return;
            Clear();
            Broadcast(string.Empty);
        }

        private void Broadcast(string payload)
        {
            if (ZNet.instance == null || _view == null || !_view.IsValid() || !_view.IsOwner()) return;

            // Owner-addressed RPC reaches the owner — the server — so a client would never see it.
            // Every peer gets its own call, exactly like the fade.
            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (peer == null || peer.m_rpc == null) continue;
                _view.InvokeRPC(peer.m_uid, BundleRpc, payload);
            }
        }

        private void RPC_Bundle(long sender, string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                Clear();
                return;
            }

            Build(Unpack(payload));
        }

        private void Build(IList<string> sharedNames)
        {
            Clear();

            var database = ObjectDB.instance;
            if (database == null || sharedNames == null) return;

            var slots = Mathf.Min(MaxProps, sharedNames.Count);
            for (var i = 0; i < slots; i++)
            {
                var prefab = database.GetItemPrefab(sharedNames[i]);
                var prop = PropOf(prefab, i, slots);
                if (prop != null) _props.Add(prop);
            }
        }

        /// <summary>
        /// A mesh-only copy of an item prefab. The prefab itself is never instantiated: it carries a
        /// ZNetView and an ItemDrop, and a second copy would be a network object nobody asked for.
        /// </summary>
        private GameObject PropOf(GameObject prefab, int index, int slots)
        {
            if (prefab == null) return null;

            var source = prefab.GetComponentInChildren<MeshRenderer>(true);
            var filter = source != null ? source.GetComponent<MeshFilter>() : prefab.GetComponentInChildren<MeshFilter>(true);
            if (source == null || filter == null || filter.sharedMesh == null) return null;

            var prop = new GameObject("husnisse_load_" + index);
            prop.transform.SetParent(transform, false);

            // A small pile in front of the chest: low and central, angled so two of them read as
            // separate objects from the player's eye height.
            var angle = (index - (slots - 1) * 0.5f) * 0.22f;
            prop.transform.localPosition = new Vector3(angle * 0.35f, 0.86f + index * 0.03f, 0.34f);
            prop.transform.localRotation = Quaternion.Euler(0f, index * 28f - 20f, index * 9f - 8f);

            var mesh = prop.AddComponent<MeshFilter>();
            mesh.sharedMesh = filter.sharedMesh;

            var renderer = prop.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var size = Mathf.Max(filter.sharedMesh.bounds.size.x, filter.sharedMesh.bounds.size.y,
                                 filter.sharedMesh.bounds.size.z);
            if (size > 0.0001f)
            {
                var scale = Mathf.Clamp(PropSize / size, 0.08f, 1.4f);
                prop.transform.localScale = Vector3.one * scale;
            }

            return prop;
        }

        private void Clear()
        {
            for (var i = 0; i < _props.Count; i++)
            {
                if (_props[i] != null) Destroy(_props[i]);
            }
            _props.Clear();
        }

        private static string Pack(IList<string> names)
        {
            var text = string.Empty;
            for (var i = 0; i < names.Count && i < MaxProps; i++)
            {
                if (string.IsNullOrEmpty(names[i])) continue;
                if (text.Length > 0) text += "|";
                text += names[i];
            }
            return text;
        }

        private static List<string> Unpack(string payload)
        {
            var names = new List<string>(MaxProps);
            foreach (var part in payload.Split('|'))
            {
                if (!string.IsNullOrEmpty(part)) names.Add(part);
            }
            return names;
        }
    }
}
