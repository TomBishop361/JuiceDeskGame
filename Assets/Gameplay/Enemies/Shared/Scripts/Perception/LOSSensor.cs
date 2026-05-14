using Game.AI;
using UnityEngine;

namespace Game.AI {
	public sealed class LOSSensor : MonoBehaviour {
		[Header("Detection")]
		[Tooltip("Layers included in the LOS raycast, including the target and any occluding obstacles.")]
		[SerializeField] private LayerMask visionLayerMask;
		[Tooltip("Maximum distance at which the sensor can detect the target.")]
		[SerializeField] private float detectionRange = 12.0f;
		[Tooltip("Field-of-view angle (in degrees) used for target detection.")]
		[SerializeField] private float visionAngle = 120.0f;
		[Tooltip("Vertical offset applied to the ray origin when checking LOS.")]
		[SerializeField] private Transform eyes;
		//[Tooltip("Vertical offset applied to the ray origin when checking LOS.")]
		//[SerializeField] private float eyeHeight = 0.5f;

		public float DetectionRange => detectionRange;
		public float VisionAngle => visionAngle;

		// Uses detection range and vision angle properties to determine whether the target is in the agent's line of sight
		public bool HasLOS(Transform target) {
			if (target == null) {
				return false;
			}

			Vector3 origin = transform.position + Vector3.up * eyes.transform.position.y;

			// Calculate distance to target
			Vector3 toTarget = target.position - origin;
			float distanceToTarget = toTarget.magnitude;

			// Detection Radius
			if (distanceToTarget > detectionRange) {
				return false;
			}

			// Calculate direction to target
			Vector3 directionToTarget = toTarget / distanceToTarget;

			// Get Vision Cone
			float angle = Vector3.Angle(transform.forward, directionToTarget);
			// Multiply by 0.5 because the full vision cone must be divided into left/right (Vector3.Angle returns the half-angle)
			if (angle > visionAngle * 0.5f) {
				return false;
			}

			// Line of sight raycast
			if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, distanceToTarget, visionLayerMask, QueryTriggerInteraction.Ignore)) {
				return hit.transform == target || hit.transform.IsChildOf(target);
			}

			return false;
		}


		// - DEPRECATED -

		//// Uses detection range and vision angle properties to determine whether the target is in the agent's line of sight
		//public bool HasLOS(Transform target) {
		//	if (target == null) {
		//		return false;
		//	}

		//	// Calculate direction and distance to target
		//	Vector3 directionToTarget = (target.position - transform.position).normalized;
		//	float distanceToTarget = CheckDistanceToTarget(transform.position, target.position);

		//	// Detection Radius
		//	if (distanceToTarget > detectionRange) {
		//		return false;
		//	}

		//	// Get Vision Cone
		//	float angle = Vector3.Angle(transform.forward, directionToTarget);
		//	// Multiply by 0.5 because the full vision cone must be divided into left/right (Vector3.Angle returns the half-angle)
		//	if (angle > visionAngle * 0.5f) {
		//		return false;
		//	}

		//	// Line of sight raycast
		//	Vector3 origin = transform.position + Vector3.up * 0.5f;
		//	if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, detectionRange, visionLayerMask, QueryTriggerInteraction.Ignore)) {
		//		return hit.transform == target;
		//	}

		//	Debug.DrawRay(origin, directionToTarget * detectionRange, Color.red);

		//	// Success and fails - for debugging purposes
		//	if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit2, distanceToTarget, visionLayerMask, QueryTriggerInteraction.Ignore)) {
		//		bool success = hit2.transform == target;
		//		Debug.DrawRay(origin, directionToTarget * distanceToTarget, success ? Color.green : Color.red);
		//		return success;
		//	}

		//	return false;
		//}

		//// TODO: Put this in a CombatHelper.cs script
		//// Check distance between two given vectors
		//public static float CheckDistanceToTarget(Vector3 self, Vector3 target) {
		//	if (self == null || target == null) {
		//		return Mathf.Infinity;
		//	}

		//	float distance = Vector3.Distance(self, target);

		//	return distance;
		//}
	}
}