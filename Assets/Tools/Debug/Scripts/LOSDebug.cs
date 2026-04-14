using UnityEngine;

public class LOSDebug : MonoBehaviour {
	public bool showVisionGizmo = true;
	[SerializeField] private GameObject eyes;
	[SerializeField] private LOSSensor losSensor;

	private void OnValidate() {
		//// Null checks
		//if (minionSettings == null) {
		//	minionSettings = GetComponent<MinionSettings>();
		//}
		//if (minionBlackboard == null) {
		//	minionBlackboard = GetComponent<MinionBlackboard>();
		//}
		if (eyes == null) {
			eyes = gameObject;
		}
		if (losSensor == null) {
			losSensor = GetComponent<LOSSensor>();
		}
	}

	private void OnDrawGizmos() {
		//// Null checks
		//if (minionSettings == null || minionBlackboard == null) {
		//	return;
		//}

		Transform agentEyes = gameObject.transform;

		// Vision Debugging
		if (showVisionGizmo == true) {
			// Detection radius - use detectionRange
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(agentEyes.position, losSensor.detectionRange);

			// FOV boundaries - use vision cone
			Gizmos.color = Color.magenta;
			Vector3 left = DirectionFromAngle(agentEyes.eulerAngles.y, -losSensor.visionAngle * 0.5f);
			Vector3 right = DirectionFromAngle(agentEyes.eulerAngles.y, losSensor.visionAngle * 0.5f);

			Gizmos.DrawLine(agentEyes.position, agentEyes.position + left * losSensor.detectionRange);
			Gizmos.DrawLine(agentEyes.position, agentEyes.position + right * losSensor.detectionRange);
		}

		//// Hearing Debugging
		//if (showHearingGizmo == true) {
		//	// Hearing radius - use hearingRange
		//	Gizmos.color = Color.cyan;
		//	Gizmos.DrawWireSphere(agentEyes.position, minionSettings.HearingRange);
		//}

		//// Noise Debugging
		//if (showNoiseGizmo == true && minionBlackboard.HeardNoise == true) {
		//	// Last Heard Noise location + path from the minion to the noise location - uses lastHeardPosition
		//	Gizmos.color = Color.blue;
		//	Gizmos.DrawSphere(minionBlackboard.LastHeardNoisePosition, 0.25f);
		//	Gizmos.DrawLine(agentEyes.position, minionBlackboard.LastHeardNoisePosition);
		//}
	}

	// Takes angle (in degrees) and converts it into a direction vector (used for field of view boundaries)
	private Vector3 DirectionFromAngle(float yRotation, float angle) {
		float radians = Mathf.Deg2Rad * (angle + yRotation);

		// Return direction on x and z plane (360 degrees is possible with these axis)
		return new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
	}
}