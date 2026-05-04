using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

namespace Game.AI {
	// Enemy-side bridge from existing perception/hearing into EnemyAwarenessHub
	// Add this to enemy prefabs
	// Sight is reported automatically on an interval
	// Noise can be reported by another component, such as an EnemyHearingSensor
	// This component reports facts only: visible player position or heard noise position
	[DisallowMultipleComponent]
	[RequireComponent(typeof(EnemyAgentBase))]
	public sealed class EnemyAwarenessReporter : MonoBehaviour {
		[Header("Sight Reporting")]
		[Tooltip("How often this enemy reports visible player position to the shared awareness hub.")]
		[SerializeField] private float sightReportInterval = 0.15f;
		[Tooltip("If true, only real LOS updates awareness. Keep true to avoid psychic tracking through walls.")]
		[SerializeField] private bool reportSightOnlyWhenVisible = true;

		// Cached enemy reference used to read target, death state, and line-of-sight state
		private EnemyAgentBase enemy;

		// Time gate for sight reports so every enemy does not write to the awareness hub every frame
		private float nextSightReportTime;
		
		private void Awake() {
			enemy = GetComponent<EnemyAgentBase>();
		}
		private void Update() {
			// Dead enemies + untargeted enemies + enemies without a valid target should not update shared awareness
			if (enemy == null || enemy.IsDead || enemy.HasTarget == false || enemy.Target == null) {
				return;
			}

			// Limit reports to a configurable interval instead of reporting every frame
			if (Time.time < nextSightReportTime) {
				return;
			}

			// Clamp the interval to a small positive value so invalid inspector values cannot cause constant updates
			nextSightReportTime = Time.time + Mathf.Max(0.03f, sightReportInterval);

			// Keep shared awareness grounded in real visibility when this option is enabled
			if (reportSightOnlyWhenVisible && enemy.HasLineOfSight == false) {
				return;
			}

			EnemyAwarenessHub hub = EnemyAwarenessHub.Active;
			if (hub == null) {
				return;
			}

			// Prefer PlayerMotionPredictor if present because it provides both position and velocity.
			// Fall back to the target transform position when no predictor exists
			PlayerMotionPredictor predictor = PlayerMotionPredictor.Active;
			Vector3 playerPosition = predictor != null ? predictor.Position : enemy.Target.position;
			Vector3 playerVelocity = predictor != null ? predictor.Velocity : Vector3.zero;

			// Push the current sight information into the shared awareness hub
			hub.ReportSight(playerPosition, playerVelocity);
		}

		// Noise reports update movement memory, but attacks must still require LOS
		public void ReportNoiseHeard(Vector3 noisePosition, float importance = 1.0f) {
			EnemyAwarenessHub hub = EnemyAwarenessHub.Active;
			if (hub == null) {
				return;
			}

			// Forward the heard location to the shared awareness hub so other enemies can search or move toward it
			hub.ReportNoise(noisePosition, importance);
		}
	}
}