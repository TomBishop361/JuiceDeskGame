using UnityEngine;

namespace Game.AI {
	// Enemy-side bridge from existing perception/hearing into EnemyAwarenessHub
	// Add this to enemy prefabs
	// Sight is reported automatically
	// Noise can be reported by EnemyHearingSensor
	// It reports facts only: sight position or heard noise position
	[DisallowMultipleComponent]
	[RequireComponent(typeof(EnemyAgentBase))]
	public sealed class EnemyAwarenessReporter : MonoBehaviour {
		[Header("Sight Reporting")]
		[Tooltip("How often this enemy reports visible player position to the shared awareness hub.")]
		[SerializeField] private float sightReportInterval = 0.15f;
		[Tooltip("If true, only real LOS updates awareness. Keep true to avoid psychic tracking through walls.")]
		[SerializeField] private bool reportSightOnlyWhenVisible = true;

		private EnemyAgentBase enemy;
		private float nextSightReportTime;
		
		private void Awake() {
			enemy = GetComponent<EnemyAgentBase>();
		}
		private void Update() {
			if (enemy == null || enemy.IsDead || enemy.HasTarget == false || enemy.Target == null) {
				return;
			}

			if (Time.time < nextSightReportTime) {
				return;
			}

			nextSightReportTime = Time.time + Mathf.Max(0.03f, sightReportInterval);

			if (reportSightOnlyWhenVisible && enemy.HasLineOfSight == false) {
				return;
			}

			EnemyAwarenessHub hub = EnemyAwarenessHub.Active;
			if (hub == null) {
				return;
			}

			PlayerMotionPredictor predictor = PlayerMotionPredictor.Active;
			Vector3 playerPosition = predictor != null ? predictor.Position : enemy.Target.position;
			Vector3 playerVelocity = predictor != null ? predictor.Velocity : Vector3.zero;

			hub.ReportSight(playerPosition, playerVelocity);
		}

		// Noise reports update movement memory, but attacks must still require LOS
		public void ReportNoiseHeard(Vector3 noisePosition, float importance = 1.0f) {
			EnemyAwarenessHub hub = EnemyAwarenessHub.Active;
			if (hub == null) {
				return;
			}

			hub.ReportNoise(noisePosition, importance);
		}
	}
}