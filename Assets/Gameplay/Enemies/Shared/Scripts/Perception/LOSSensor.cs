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
		[Tooltip("Fallback eye height used when no eyes transform has been assigned.")]
		[SerializeField] private float fallbackEyeHeight = 0.5f;

		public float DetectionRange => detectionRange;
		public float VisionAngle => visionAngle;

		// Uses detection range and vision angle properties to determine whether the target is in the agent's line of sight
		public bool HasLOS(Transform target) {
			if (target == null) {
				return false;
			}

			// Use the eye transform's world position
			Vector3 origin = eyes != null ? eyes.position : transform.position + Vector3.up * fallbackEyeHeight;

			// Calculate distance to target
			Vector3 toTarget = target.position - origin;
			float distanceToTarget = toTarget.magnitude;

			// Detection Radius
			if (distanceToTarget < 0.0001f) {
				return true;
			}
			if (distanceToTarget > detectionRange) {
				return false;
			}

			// FOV is checked horizontally so vertical platforming height does not unfairly break sight
			Vector3 flatForward = transform.forward;
			flatForward.y = 0.0f;

			Vector3 flatToTarget = toTarget;
			flatToTarget.y = 0.0f;

			if (flatForward.sqrMagnitude > 0.0001f && flatToTarget.sqrMagnitude > 0.0001f) {
				// Get Vision Cone
				float angle = Vector3.Angle(flatForward.normalized, flatToTarget.normalized);
				// Multiply by 0.5 because the full vision cone must be divided into left/right (Vector3.Angle returns the half-angle)
				if (angle > visionAngle * 0.5f) {
					return false;
				}
			}

			// Calculate horizontal direction to target
			Vector3 directionToTarget = toTarget / distanceToTarget;

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

		//// Put this in a CombatHelper.cs script
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