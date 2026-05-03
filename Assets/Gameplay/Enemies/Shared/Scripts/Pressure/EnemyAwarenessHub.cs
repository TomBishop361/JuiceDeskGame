using UnityEngine;

namespace Game.AI {
	public enum EnemyAwarenessSource {
		None,
		Noise,
		Sight
	}

	// Shared awareness memory
	// It only remembers where the player was last seen/heard
	// Movement may use this memory
	// Attacks should still require real LOS
	// This is shared memory: last seen/heard position only
	// Use it for movement and spawn awareness, never for allowing attacks through walls
	[DisallowMultipleComponent]
	public sealed class EnemyAwarenessHub : MonoBehaviour {
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

		public bool HasKnownPosition => Time.time <= knownPositionExpireTime;
		public Vector3 LastKnownPosition { get; private set; }
		public Vector3 LastKnownVelocity { get; private set; }
		public EnemyAwarenessSource LastSource { get; private set; }
		public float LastReportTime { get; private set; }
		public float Confidence { get; private set; }

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
			Active = this;
		}

		private void OnDisable() {
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
			importance = Mathf.Clamp01(importance);
			if (importance < minNoiseImportance) {
				return;
			}

			Vector3 estimatedVelocity = Vector3.zero;
			if (hasPreviousKnownPosition) {
				float dt = Mathf.Max(0.001f, Time.time - LastReportTime);
				estimatedVelocity = (noisePosition - previousKnownPosition) / dt;
			}

			StoreKnownPosition(noisePosition, estimatedVelocity, EnemyAwarenessSource.Noise, importance, noiseMemoryDuration);
		}

		public bool TryGetKnownPosition(out Vector3 position) {
			if (HasKnownPosition) {
				position = LastKnownPosition;
				return true;
			}

			position = default;
			return false;
		}

		public bool TryGetKnownPositionForSpawn(out Vector3 position) {
			bool freshEnoughForSpawn = LastReportTime > 0.0f && Time.time <= LastReportTime + spawnAwarenessDuration;
			if (freshEnoughForSpawn) {
				position = LastKnownPosition;
				return true;
			}

			position = default;
			return false;
		}

		// Returns a simple velocity projection with damped vertical movement
		public Vector3 PredictKnownPosition(float leadTime, float maxPredictionDistance = 4.0f) {
			Vector3 predictedOffset = LastKnownVelocity * Mathf.Max(0.0f, leadTime);
			predictedOffset = Vector3.ClampMagnitude(predictedOffset, maxPredictionDistance);
			return LastKnownPosition + predictedOffset;
		}

		private void StoreKnownPosition(Vector3 position, Vector3 velocity, EnemyAwarenessSource source, float confidence, float duration) {
			previousKnownPosition = LastKnownPosition;
			hasPreviousKnownPosition = true;

			LastKnownPosition = position;
			LastKnownVelocity = velocity;
			LastSource = source;
			LastReportTime = Time.time;
			Confidence = Mathf.Clamp01(confidence);
			knownPositionExpireTime = Time.time + Mathf.Max(0.1f, duration);
		}

		private void OnDrawGizmosSelected() {
			if (Application.isPlaying == false || HasKnownPosition == false) {
				return;
			}

			Gizmos.DrawWireSphere(LastKnownPosition, 0.45f);
			if (LastKnownVelocity.sqrMagnitude > 0.0001f) {
				Gizmos.DrawLine(LastKnownPosition, LastKnownPosition + LastKnownVelocity.normalized * 1.5f);
			}
		}
	}
}