using UnityEngine;

namespace Game.AI.Drone {
	[DisallowMultipleComponent]
	public sealed class DroneFlightMotor : MonoBehaviour {
		[Header("References")]
		[SerializeField] private FlightEnemyMotor flightMotor;

		[Header("Range")]
		[Tooltip("Minimum planar distance before the drone moves away from the target.")]
		[SerializeField] private float minRange = 4.0f;
		[Tooltip("Preferred planar distance the drone tries to maintain from the target.")]
		[SerializeField] private float desiredRange = 7.0f;
		[Tooltip("Buffer zone around desired range to prevent constant jittering (hysteresis).")]
		[SerializeField] private float rangeDeadzone = 0.75f;

		[Header("Orbit")]
		[Tooltip("Small sideways wobble added to orbiting so movement feels less circular and rigid.")]
		[SerializeField] private float orbitJitter = 0.15f;

		//[Header("Investigate")]
		//[Tooltip("How close the drone must get to a remembered point before it stops moving and just faces it.")]
		//[SerializeField] private float investigateArrivalDistance = 0.6f;

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

		public bool TargetTooClose(Transform target) {
			return GetPlanarDistanceToTarget(target) < minRange;
		}

		// Updates drone combat movement using horizontal target spacing
		// Retreats when inside minimum range, otherwise holds preferred spacing and orbit
		public void TickMovement(Transform target) {
			if (flightMotor == null || target == null) {
				return;
			}

			float planarDistanceToTarget = GetPlanarDistanceToTarget(target);
			if (planarDistanceToTarget < minRange) {
				flightMotor.MoveAway(target);
				return;
			}

			flightMotor.MaintainRange(target, planarDistanceToTarget, desiredRange, rangeDeadzone, orbitJitter);
		}

		//// Updates drone investigating movement based on its distance to the arrive point
		//// Moves towards investigation point until it is within the arrival threshold
		//public void TickInvestigateMovement(Vector3 investigatePoint) {
		//	if (flightMotor == null) {
		//		return;
		//	}

		//	float planarDistanceToPoint = GetPlanarDistanceToPoint(investigatePoint);
		//	if (planarDistanceToPoint <= investigateArrivalDistance) {
		//		flightMotor.FaceTarget(investigatePoint);
		//		return;
		//	}

		//	flightMotor.MoveTowardsPoint(investigatePoint);
		//}

		// Returns only the horizontal distance to the target
		// Ignores Y so hover bobbing and vertical offset do not affect range checks
		private float GetPlanarDistanceToTarget(Transform target) {
			if (target == null) {
				return Mathf.Infinity;
			}

			Vector3 selfPlanar = transform.position;
			Vector3 targetPlanar = target.position;

			// Ignore height when deciding combat spacing
			// This prevents hover bob / vertical offset from constantly messing up the range logic
			selfPlanar.y = 0.0f;
			targetPlanar.y = 0.0f;

			return Vector3.Distance(selfPlanar, targetPlanar);
		}

		//private float GetPlanarDistanceToPoint(Vector3 point) {
		//	Vector3 selfPlanar = transform.position;
		//	Vector3 pointPlanar = point;

		//	selfPlanar.y = 0.0f;
		//	pointPlanar.y = 0.0f;

		//	return Vector3.Distance(selfPlanar, pointPlanar);
		//}
	}
}