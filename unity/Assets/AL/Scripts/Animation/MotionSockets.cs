using System;
using System.Collections.Generic;
using UnityEngine;

namespace AL.Motion
{
    public sealed class MotionSocketDefinition
    {
        public MotionSocketDefinition(string socketId, string canonicalPath)
        {
            if (string.IsNullOrWhiteSpace(socketId) ||
                string.IsNullOrWhiteSpace(canonicalPath))
            {
                throw new ArgumentException("Socket ID and canonical path are required.");
            }

            SocketId = socketId;
            CanonicalPath = canonicalPath;
        }

        public string SocketId { get; }
        public string CanonicalPath { get; }
    }

    public sealed class MotionSocketAlias
    {
        public MotionSocketAlias(string alias, string socketId)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(socketId))
            {
                throw new ArgumentException("Socket alias and target ID are required.");
            }

            Alias = alias;
            SocketId = socketId;
        }

        public string Alias { get; }
        public string SocketId { get; }
    }

    public sealed class MotionSocketRegistry
    {
        private readonly Dictionary<string, Transform> _sockets =
            new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _aliases =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public MotionSocketRegistry(
            Transform skeletonRoot,
            IEnumerable<MotionSocketDefinition> definitions,
            IEnumerable<MotionSocketAlias> aliases = null)
        {
            if (skeletonRoot == null)
            {
                throw new ArgumentNullException(nameof(skeletonRoot));
            }

            foreach (MotionSocketDefinition definition in definitions ??
                     throw new ArgumentNullException(nameof(definitions)))
            {
                if (definition == null || _sockets.ContainsKey(definition.SocketId))
                {
                    throw new InvalidOperationException(
                        "Socket definitions must be non-null and unique.");
                }

                Transform socket = skeletonRoot.Find(definition.CanonicalPath);
                if (socket == null)
                {
                    throw new InvalidOperationException(
                        "Canonical socket path is missing: " + definition.CanonicalPath);
                }

                Vector3 localScale = socket.localScale;
                if (Mathf.Abs(localScale.x - 1f) > 0.0001f ||
                    Mathf.Abs(localScale.y - 1f) > 0.0001f ||
                    Mathf.Abs(localScale.z - 1f) > 0.0001f)
                {
                    throw new InvalidOperationException(
                        "Canonical socket must have identity local scale: " +
                        definition.CanonicalPath);
                }

                _sockets.Add(definition.SocketId, socket);
            }

            foreach (MotionSocketAlias alias in aliases ?? Array.Empty<MotionSocketAlias>())
            {
                if (alias == null || !_sockets.ContainsKey(alias.SocketId) ||
                    !_aliases.TryAdd(alias.Alias, alias.SocketId))
                {
                    throw new InvalidOperationException(
                        "Socket aliases must be explicit, unique, and target a known socket.");
                }
            }
        }

        public bool TryResolve(string socketId, out Transform socket)
        {
            return _sockets.TryGetValue(socketId ?? string.Empty, out socket);
        }

        public bool TryResolveAlias(string alias, out Transform socket)
        {
            socket = null;
            return _aliases.TryGetValue(alias ?? string.Empty, out string socketId) &&
                   _sockets.TryGetValue(socketId, out socket);
        }

        public bool Attach(Transform attachment, string socketId)
        {
            if (attachment == null || !TryResolve(socketId, out Transform socket))
            {
                return false;
            }

            attachment.SetParent(socket, false);
            attachment.localPosition = Vector3.zero;
            attachment.localRotation = Quaternion.identity;
            attachment.localScale = Vector3.one;
            return true;
        }
    }
}
