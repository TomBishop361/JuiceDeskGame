using UnityEngine;

namespace Game.AI.Drone {
	[DisallowMultipleComponent]
	public sealed class DroneFlightMotor : MonoBehaviour {
		[Header("References")]
		[SerializeField] private FlightEnemyMotor flightMotor;

		[Header("Range")]
		[Tooltip("Minimum distance before the drone moves away from the target.")]
		[SerializeField] private float minRange = 4.0f;
		[Tooltip("Preferred distance the drone tries to maintain from the target.")]
		[SerializeField] private float desiredRange = 7.0f;
		[Tooltip("Buffer zone around desired range to prevent constant jittering (hysteresis).")]
		[SerializeField] private float rangeDeadzone = 0.75f;

		[Header("Orbit")]
		[Tooltip("Small variation added to orbit movement so drones don't overlap paths.")]
		[SerializeField] private float orbitJitter = 0.15f;

		public float CurrentSpeed => flightMotor != null ? flightMotor.LastMoveSpeed : 0.0f;
		public float DesiredRange => desiredRange;
		public float RangeDeadzone => rangeDeadzone;
		public float MinRange => minRange;

		private void Reset() {
			if (flightMotor == null) {
				flightMotor = GetComponent<FlightEnemyMotor>();
			}
		}

		// Resets flight motor module state for pooling or respawn
		public void ResetRuntime() {
			flightMotor?.ResetRuntime();
		}

		public bool TargetTooClose(float distanceToTarget) {
			return distanceToTarget < minRange;
		}

		public void TickMovement(Transform target, float distanceToTarget) {
			if (flightMotor == null || target == null) {
				return;
			}

			if (TargetTooClose(distanceToTarget)) {
				flightMotor.MoveAway(target);
				return;
			}

			flightMotor.MaintainRange(target, distanceToTarget, desiredRange, rangeDeadzone, orbitJitter);
		}
	}
}