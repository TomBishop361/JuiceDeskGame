using Unity.VisualScripting;
using UnityEngine;

namespace Game.AI {
	// Source type for the latest shared awareness report
	// Sight is high-confidence, while noise is approximate and should only guide movement/searching
	public enum EnemyAwarenessSource {
		None,
		Noise,
		Sight
	}

	// Shared awareness memory for enemies
	// It remembers only the last seen or heard player position
	// Movement may use this memory for searching + corner-following + spawn awareness
	// Attacks should still require real LOS so enemies do not shoot or swing through walls
	[DisallowMultipleComponent]
	public sealed class EnemyAwarenessHub : MonoBehaviour {
		// Scene-wide active awareness hub used by reporters and newly spawned enemies
		public static EnemyAwarenessHub Active { get; private set; }

		[Header("Memory")]
		[Tooltip("How long a seen player position remains usable after LOS is lost.")]
		[SerializeField] private float sightMemoryDuration = 5.0f;
		[Tooltip("How long a heard noise position remains usable after the sound is reported.")]
		[SerializeField] private float noiseMemoryDuration = 3.0f;
		[Tooltip("How long newly spawned enemies may inherit the last known player position.")]
		[SerializeField] private float spawnAwarenessDuration = 6.0f;

		[Header("Noise")]
		[Tooltip("Noise reports below this normalized importance are ignored.")]
		[SerializeField] private float minNoiseImportance = 0.15f;

		// True while the current last-known position is still within its memory window
		public bool HasKnownPosition => Time.time <= knownPositionExpireTime;

		// Last shared player information known to the enemy group
		public Vector3 LastKnownPosition { get; private set; }
		public Vector3 LastKnownVelocity { get; private set; }
		public EnemyAwarenessSource LastSource { get; private set; }
		public float LastReportTime { get; private set; }
		public float Confidence { get; private set; }

		// Internal expiry and previous-position state used for memory and velocity estimation
		private float knownPositionExpireTime;
		private Vector3 previousKnownPosition;
		private bool hasPreviousKnownPosition;

		private void Awake() {
			if (Active != null && Active != this) {
				Debug.LogWarning($"Multiple {nameof(EnemyAwarenessHub)} instances found. Only one should exist per scene.", this);
			}

			Active = this;
		}

		private void OnEnable() {
			// Reassert this instance as the shared awareness hub when it is enabled
			Active = this;
		}

		private void OnDisable() {
			// Clear the static reference only if this object is the current active awareness hub
			if (Active == this) {
				Active = null;
			}
		}

		// Sight reports are high-confidence and last longer than noise reports
		public void ReportSight(Vector3 playerPosition, Vector3 playerVelocity) {
			StoreKnownPosition(playerPosition, playerVelocity, EnemyAwarenessSource.Sight, 1.0f, sightMemoryDuration);
		}

		// Noise reports update movement memory, but attacks must still require LOS
		public void ReportNoise(Vector3 noisePosition, float importance = 1.0f) {
			// Importance is normalized so callers can pass rough values safely
			importance = Mathf.Clamp01(importance);
			if (importance < minNoiseImportance) {
				return;
			}

			// Estimate movement direction from the previous awareness position when available
			Vector3 estimatedVelocity = Vector3.zero;
			if (hasPreviousKnownPosition) {
				float dt = Mathf.Max(0.001f, Time.time - LastReportTime);
				estimatedVelocity = (noisePosition - previousKnownPosition) / dt;
			}

			StoreKnownPosition(noisePosition, estimatedVelocity, EnemyAwarenessSource.Noise, importance, noiseMemoryDuration);
		}

		public bool TryGetKnownPosition(out Vector3 position) {
			// Return the last-known position only while its memory window is still valid
			if (HasKnownPosition) {
				position = LastKnownPosition;
				return true;
			}

			position = default;
			return false;
		}

		public bool TryGetKnownPositionForSpawn(out Vector3 position) {
			// Spawn awareness uses its own freshness window so newly spawned enemies can inherit recent player information
			bool freshEnoughForSpawn = LastReportTime > 0.0f && Time.time <= LastReportTime + spawnAwarenessDuration;
			if (freshEnoughForSpawn) {
				position = LastKnownPosition;
				return true;
			}

			position = default;
			return false;
		}

		// Returns a simple velocity projection with distance clamping.
		// This gives movement code a small lead without letting redundent velocity fling enemies far away
		public Vector3 PredictKnownPosition(float leadTime, float maxPredictionDistance = 4.0f) {
			Vector3 predictedOffset = LastKnownVelocity * Mathf.Max(0.0f, leadTime);
			predictedOffset = Vector3.ClampMagnitude(predictedOffset, maxPredictionDistance);
			return LastKnownPosition + predictedOffset;
		}

		private void StoreKnownPosition(Vector3 position, Vector3 velocity, EnemyAwarenessSource source, float confidence, float duration) {
			// Preserve the previous position before overwriting it so noise reports can estimate velocity next time
			previousKnownPosition = LastKnownPosition;
			hasPreviousKnownPosition = true;

			// Store the latest shared awareness state
			LastKnownPosition = position;
			LastKnownVelocity = velocity;
			LastSource = source;
			LastReportTime = Time.time;
			Confidence = Mathf.Clamp01(confidence);

			// Give every report at least a tiny lifetime to avoid instant expiry from invalid inspector values
			knownPositionExpireTime = Time.time + Mathf.Max(0.1f, duration);
		}

		private void OnDrawGizmosSelected() {
			if (Application.isPlaying == false || HasKnownPosition == false) {
				return;
			}

			// Draw the remembered position and a short direction line for the remembered velocity
			Gizmos.DrawWireSphere(LastKnownPosition, 0.45f);
			if (LastKnownVelocity.sqrMagnitude > 0.0001f) {
				Gizmos.DrawLine(LastKnownPosition, LastKnownPosition + LastKnownVelocity.normalized * 1.5f);
			}
		}
	}
}