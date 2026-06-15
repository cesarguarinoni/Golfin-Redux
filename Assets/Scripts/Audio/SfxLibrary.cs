using System;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Audio.Events;

namespace Golfin.Audio
{
    /// <summary>
    /// ScriptableObject holding the authoritative SfxId → AudioClip map.
    ///
    /// Assign clips in the Inspector (Asset: Assets/Audio/SfxLibrary.asset).
    /// Using a ScriptableObject (not Resources.Load) keeps clip refs explicit,
    /// allows Addressables migration, and avoids build-time Resources folder bloat.
    ///
    /// GetClip() returns null for unmapped ids — SfxPlayer logs a warning and skips.
    /// </summary>
    [CreateAssetMenu(fileName = "SfxLibrary", menuName = "Golfin/Audio/SfxLibrary")]
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct SfxEntry
        {
            public SfxId id;
            public AudioClip clip;
        }

        [SerializeField]
        private SfxEntry[] _entries = Array.Empty<SfxEntry>();

        // Runtime lookup cache (built lazily on first GetClip call).
        private Dictionary<SfxId, AudioClip> _cache;

        private void BuildCacheIfNeeded()
        {
            if (_cache != null) return;
            _cache = new Dictionary<SfxId, AudioClip>(_entries.Length);
            foreach (var e in _entries)
            {
                if (e.clip != null)
                    _cache[e.id] = e.clip;
            }
        }

        /// <summary>
        /// Returns the AudioClip mapped to the given SfxId, or null if unmapped.
        /// </summary>
        public AudioClip GetClip(SfxId id)
        {
            BuildCacheIfNeeded();
            _cache.TryGetValue(id, out var clip);
            return clip;
        }

        // Called by Unity when this SO is modified in the Inspector (Editor hot-reload).
        private void OnValidate()
        {
            _cache = null; // invalidate cache on Inspector edit
        }
    }
}
