// Order: reward_points_backend Slice 1 — coroutine host for fire-and-forget API calls.
using System.Collections;
using UnityEngine;

namespace Golfin.Net
{
    /// <summary>Somewhere to run a coroutine from a plain C# object. Tests supply a pump instead.</summary>
    public interface ICoroutineRunner
    {
        void Run(IEnumerator routine);
    }

    /// <summary>
    /// Lazily-created DontDestroyOnLoad host, same self-bootstrapping shape as <c>AuthService</c> so no
    /// scene wiring is required. Created on first fire-and-forget call only — with the
    /// <c>PointsBackendEnabled</c> flag OFF nothing ever touches it, so no GameObject is added to the
    /// scene and the build is behaviourally unchanged.
    /// </summary>
    internal sealed class NetCoroutineRunner : MonoBehaviour, ICoroutineRunner
    {
        private static NetCoroutineRunner _instance;

        public static NetCoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[Golfin.Net]");
                    _instance = go.AddComponent<NetCoroutineRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public void Run(IEnumerator routine)
        {
            if (routine == null) return;
            StartCoroutine(routine);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
