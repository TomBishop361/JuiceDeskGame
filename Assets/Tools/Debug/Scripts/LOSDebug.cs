using UnityEngine;

namespace Game.AI {
	public sealed class LOSDebug : MonoBehaviour {
		[Header("References")]
		[SerializeField] private Transform eyes;
		[SerializeField] private LOSSensor losSensor;
		[SerializeField] private Transform debugTarget;

		[Header("Debug")]
		[SerializeField] private bool showVisionGizmo = true;
		[SerializeField] private bool showSightLine = true;

		private void OnValidate() {
			if (eyes == null) {
				eyes = transform;
			}
			if (losSensor == null) {
				losSensor = GetComponent<LOSSensor>();
			}
		}

		// Detection + Vision Debugging
		private void OnDrawGizmos() {
			if (showVisionGizmo == false || losSensor == null || eyes == null) {
				return;
			}

			Vector3 origin = eyes.position;

			// Detection radius - uses detection range
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(origin, losSensor.DetectionRange);

			// FOV boundaries - uses vision cone
			Gizmos.color = Color.magenta;
			Vector3 left = DirectionFromAngle(eyes.eulerAngles.y, -losSensor.VisionAngle * 0.5f);
			Vector3 right = DirectionFromAngle(eyes.eulerAngles.y, losSensor.VisionAngle * 0.5f);

			Gizmos.DrawLine(origin, origin + left * losSensor.DetectionRange);
			Gizmos.DrawLine(origin, origin + right * losSensor.DetectionRange);

			// Optional LOS line to target
			if (showSightLine == true && debugTarget != null) {
				bool hasLOS = losSensor.HasLOS(debugTarget);

				Gizmos.color = hasLOS ? Color.green : Color.red;
				Gizmos.DrawLine(origin, debugTarget.position);
				Gizmos.DrawSphere(debugTarget.position, 0.12f);
			}
		}

		// Takes angle (in degrees) and converts it into a direction vector (used for field of view boundaries)
		private Vector3 DirectionFromAngle(float yRotation, float angle) {
			float radians = Mathf.Deg2Rad * (yRotation + angle);

			// Return direction on x and z plane (360 degrees is possible with these axis)
			return new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
		}
	}
}