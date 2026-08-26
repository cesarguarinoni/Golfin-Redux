// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the network seam, and its ApiClient-backed implementation.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §2, §4
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Golfin.InventorySync
{
    /// <summary>Outcome of a GET. <see cref="Json"/> null with <see cref="Ok"/> true is the
    /// never-synced player — a normal state, not a failure.</summary>
    public readonly struct InventoryFetch
    {
        public readonly bool Ok;
        public readonly string? Json;
        public readonly int Rev;

        public InventoryFetch(bool ok, string? json, int rev) { Ok = ok; Json = json; Rev = rev; }

        public static readonly InventoryFetch Failed = new InventoryFetch(false, null, 0);
    }

    /// <summary>
    /// Outcome of a PUT. <see cref="Stale"/> is NOT a failure: it is the rev-mismatch rule, and it
    /// carries everything needed to merge and retry without a second round trip.
    /// </summary>
    public readonly struct InventoryPutOutcome
    {
        public readonly bool Ok;
        public readonly bool Stored;
        public readonly bool Stale;
        public readonly int Rev;
        public readonly string? ServerJson;

        public InventoryPutOutcome(bool ok, bool stored, bool stale, int rev, string? serverJson)
        {
            Ok = ok; Stored = stored; Stale = stale; Rev = rev; ServerJson = serverJson;
        }

        public static readonly InventoryPutOutcome Failed =
            new InventoryPutOutcome(false, false, false, 0, null);
    }

    /// <summary>
    /// The four calls the sync makes.
    ///
    /// <para>
    /// An interface, not four <c>Action</c> fields, because the tests drive whole SCENARIOS through
    /// it — boot, stale-retry, offline, grant-drain — and a fake that answers all four coherently is
    /// the only way to test those without a socket. The shipped implementation is
    /// <see cref="ApiInventoryTransport"/>; nothing else in the assembly knows <c>ApiClient</c>
    /// exists.
    /// </para>
    /// </summary>
    public interface IInventoryTransport
    {
        void GetInventory(Action<InventoryFetch> done);
        void PutInventory(string blobJson, int rev, Action<InventoryPutOutcome> done);

        /// <summary>Null means the request FAILED. An empty list means "no grants", which is the
        /// normal case and must not be confused with it.</summary>
        void GetGrants(Action<List<InventoryGrant>?> done);

        void AckGrants(IReadOnlyList<string> grantIds, Action<bool> done);
    }

    /// <summary>The shipping transport: <see cref="ApiClient"/> plus the four
    /// <see cref="Endpoints"/> URLs. Auth, the <c>{data:…}</c> envelope, transient retries and the
    /// single 401 refresh-and-replay all come from ApiClient — nothing is re-implemented here.</summary>
    public sealed class ApiInventoryTransport : IInventoryTransport
    {
        private const string Tag = "[InventorySync]";

        public void GetInventory(Action<InventoryFetch> done)
        {
            var api = ApiClient.Instance;
            api.Run(api.Get<JObject>(Endpoints.UserGolfinInventory, result =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"{Tag} inventory fetch failed ({result.ErrorKind}, HTTP " +
                                     $"{result.StatusCode}): {result.ErrorMessage}");
                    done?.Invoke(InventoryFetch.Failed);
                    return;
                }

                JObject? data = result.Data;
                JToken? inv = data? ["inventory"];
                string? json = inv == null || inv.Type == JTokenType.Null
                    ? null
                    : inv.ToString(Formatting.None);
                int rev = (int?)data? ["rev"] ?? 0;
                done?.Invoke(new InventoryFetch(true, json, rev));
            }));
        }

        public void PutInventory(string blobJson, int rev, Action<InventoryPutOutcome> done)
        {
            // Hand-built rather than serialised from an anonymous object: `inventory` is ALREADY
            // json, and round-tripping it through a DTO only to re-emit it would be two extra parses
            // of the largest string in the session.
            string body = "{\"inventory\":" + blobJson + ",\"rev\":" + rev + "}";

            var api = ApiClient.Instance;
            api.Run(api.Put<JObject>(Endpoints.UserGolfinInventory, body, result =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"{Tag} inventory push failed ({result.ErrorKind}, HTTP " +
                                     $"{result.StatusCode}): {result.ErrorMessage}");
                    done?.Invoke(InventoryPutOutcome.Failed);
                    return;
                }

                JObject? data = result.Data;
                bool stored = (bool?)data? ["stored"] ?? false;
                int newRev = (int?)data? ["rev"] ?? rev;

                if (stored)
                {
                    done?.Invoke(new InventoryPutOutcome(true, true, false, newRev, null));
                    return;
                }

                // 200 + stored:false = the rev moved under us. The server hands back its blob so the
                // merge needs no second round trip.
                JToken? inv = data? ["inventory"];
                string? serverJson = inv == null || inv.Type == JTokenType.Null
                    ? null
                    : inv.ToString(Formatting.None);
                done?.Invoke(new InventoryPutOutcome(true, false, true, newRev, serverJson));
            }));
        }

        public void GetGrants(Action<List<InventoryGrant>?> done)
        {
            var api = ApiClient.Instance;
            api.Run(api.Get<InventoryGrantList>(Endpoints.UserGolfinGrants, result =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"{Tag} grant fetch failed ({result.ErrorKind}, HTTP " +
                                     $"{result.StatusCode}): {result.ErrorMessage}");
                    done?.Invoke(null);
                    return;
                }
                done?.Invoke(result.Data?.Grants ?? new List<InventoryGrant>());
            }));
        }

        public void AckGrants(IReadOnlyList<string> grantIds, Action<bool> done)
        {
            string body = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["grant_ids"] = grantIds,
            });

            var api = ApiClient.Instance;
            api.Run(api.Post<JObject>(Endpoints.UserGolfinGrantsAck, body, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"{Tag} grant ack failed ({result.ErrorKind}, HTTP " +
                                     $"{result.StatusCode}): {result.ErrorMessage}. The grant is " +
                                     "already applied locally and its id is in the save, so the " +
                                     "next boot re-acks rather than re-applying.");
                done?.Invoke(result.Success);
            }));
        }
    }
}
