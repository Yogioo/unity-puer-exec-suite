using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityPuerExec
{
    /// <summary>
    /// Removes the per-request records an abandoned exec leaves behind.
    ///
    /// The CLI persists an in-flight exec under Temp/UnityPuerExec/pending_exec so a
    /// later `wait-for-exec` can resume it. When the caller gives up (timeout, killed
    /// agent, crashed CLI) nothing deletes that record: it stays in whatever non-terminal
    /// phase it last wrote (`refreshing`, `compiling`, ...). Those corpses make the next
    /// investigation read as "several requests are stuck" when the service is healthy, so
    /// this sweep drops the ones that are clearly older than any live wait, and /health
    /// reports how many are left.
    ///
    /// Only records with a readable <c>updated_at_ms</c> are considered -- an unparseable
    /// or missing field means an unknown age, which is not evidence of abandonment.
    /// </summary>
    internal static class UnityPuerExecPendingExecHousekeeping
    {
        internal const string RelativeDirectory = "pending_exec";

        // Comfortably longer than any CLI wait window (the verify gate budgets minutes,
        // not hours), so a live request is never mistaken for an abandoned one.
        private const long StaleAfterMs = 2L * 60 * 60 * 1000;

        private static readonly Regex UpdatedAtRegex = new(
            @"""updated_at_ms""\s*:\s*(?<ms>\d+)",
            RegexOptions.Compiled);

        internal sealed class Snapshot
        {
            internal int Total;
            internal int Removed;
        }

        /// <summary>Directory the CLI uses for in-flight records of this project.</summary>
        internal static string DirectoryPath()
        {
            var root = UnityPuerExecEndpointPublication.DirectoryPath();
            return string.IsNullOrEmpty(root)
                ? ""
                : Path.Combine(root, RelativeDirectory);
        }

        /// <summary>
        /// Delete records older than <see cref="StaleAfterMs"/>. Never throws: housekeeping
        /// is not allowed to fail the service, and a locked file simply survives the sweep.
        /// </summary>
        internal static Snapshot Sweep()
        {
            var snapshot = new Snapshot();
            try
            {
                var directory = DirectoryPath();
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return snapshot;
                }

                var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - StaleAfterMs;
                foreach (var path in Directory.GetFiles(directory, "*.json"))
                {
                    snapshot.Total++;

                    if (!TryReadUpdatedAtMs(path, out var updatedAtMs) || updatedAtMs > cutoff)
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(path);
                        snapshot.Removed++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UnityPuerExec] Could not remove stale pending_exec record {Path.GetFileName(path)}: {ex.Message}");
                    }
                }

                if (snapshot.Removed > 0)
                {
                    Debug.Log(
                        $"[UnityPuerExec] Removed {snapshot.Removed} stale pending_exec record(s) " +
                        $"left by an abandoned request ({snapshot.Total} in the directory).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityPuerExec] pending_exec housekeeping failed: {ex.Message}");
            }

            return snapshot;
        }

        private static bool TryReadUpdatedAtMs(string path, out long updatedAtMs)
        {
            updatedAtMs = 0;
            try
            {
                var match = UpdatedAtRegex.Match(File.ReadAllText(path));
                return match.Success && long.TryParse(match.Groups["ms"].Value, out updatedAtMs);
            }
            catch
            {
                return false;
            }
        }
    }
}
