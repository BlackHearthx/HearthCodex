using UnityEngine;

namespace CreatureCodex
{
    // Uses the game's loaded-character list, not a scene/prefab search. Physics checks
    // run only for unknown non-bosses in range and near the camera's forward direction.
    internal static class CodexObservation
    {
        private const float Interval = 0.2f;
        private static float _nextCheck;
        private static int _blockMask;
        private static Player _player;
        private static Character _target;
        private static float _startedAt;

        internal static void Tick(bool gameplayAvailable)
        {
            var player = Player.m_localPlayer;
            if (!gameplayAvailable || !PluginConfig.ObservationEnabled.Value || player == null
                || player.IsDead() || player.IsTeleporting() || player.InCutscene()
                || !CreatureCatalog.IsBuilt || GameCamera.instance == null)
            {
                Reset();
                return;
            }

            if (_player != player)
            {
                Reset();
                _player = player;
            }

            var now = Time.time;
            if (now < _nextCheck) return;
            // Do not count an extended pause/loading gap as continuous observation.
            if (now > _nextCheck + 0.5f) _target = null;
            _nextCheck = now + Interval;

            var camera = GameCamera.instance.GetComponent<Camera>();
            if (camera == null) { _target = null; return; }
            if (_blockMask == 0)
            {
                // Same obstruction layers as BaseAI's view checks in the installed game.
                _blockMask = LayerMask.GetMask("Default", "static_solid", "Default_small",
                    "piece", "terrain", "viewblock", "vehicle");
            }

            var eye = player.GetEyePoint();
            var cameraPosition = camera.transform.position;
            var forward = camera.transform.forward;
            var range = PluginConfig.ObservationDistance.Value;
            var bestAlignment = Mathf.Cos(15f * Mathf.Deg2Rad);
            Character best = null;
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character == player || character.IsPlayer() || character.IsDead()
                    || character.IsBoss() || !CreatureCatalog.TryGetSpecies(character.m_name, out var species)
                    || species.IsBoss || species.Knowledge != CodexKnowledge.Unknown) continue;

                var center = character.GetCenterPoint();
                if ((center - eye).sqrMagnitude > range * range) continue;
                var direction = center - cameraPosition;
                var alignment = Vector3.Dot(forward, direction.normalized);
                if (alignment <= bestAlignment) continue;
                var viewport = camera.WorldToViewportPoint(center);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                if (Physics.Linecast(eye, center, _blockMask, QueryTriggerInteraction.Ignore)
                    || Physics.Linecast(cameraPosition, center, _blockMask, QueryTriggerInteraction.Ignore)) continue;
                best = character;
                bestAlignment = alignment;
            }

            if (best == null) { _target = null; return; }
            if (best != _target)
            {
                _target = best;
                _startedAt = now;
                return;
            }

            if (now - _startedAt >= PluginConfig.ObservationSeconds.Value)
            {
                CodexProgress.DiscoverObservedCreature(player, best.m_name);
                _target = null;
            }
        }

        private static void Reset()
        {
            _player = null;
            _target = null;
            _nextCheck = 0f;
        }
    }
}
