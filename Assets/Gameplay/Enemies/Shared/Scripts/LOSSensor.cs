using Game.AI;
using UnityEngine;

public class LOSSensor : MonoBehaviour {
	[SerializeField] private Transform target;
	[SerializeField] private LayerMask visionLayerMask;

	// TODO: Add to a interface (IVisionSettings)
	[SerializeField] public float detectionRange = 12.0f;
	[SerializeField] public float visionAngle = 120.0f;
	//[SerializeField] private float detectionRange { get; }
	//[SerializeField] private float visionAngle { get; }

	private IEnemyAgent agent;


	private void Awake() {
		//// Attempt to get the AI Controller Interface
		//IAIController aiControllerInterface = GetComponent<IAIController>();
		//if (aiControllerInterface != null) {
		//	sensorBlackboardInterface = aiControllerInterface.SensorBlackboard;
		//	sensorSettingsInterface = aiControllerInterface.SensorSettings;
		//}

		//if (sensorBlackboardInterface == null) {
		//	sensorBlackboardInterface = GetComponent<ISensorBlackboard>();
		//}
		//if (sensorSettingsInterface == null) {
		//	sensorSettingsInterface = GetComponent<ISensorSettings>();
		//}

		//// Error checks
		//if (sensorBlackboardInterface == null) {
		//	Debug.LogError("No ISensorBlackboard found on " + gameObject.name);
		//}
		//// Error checks
		//if (sensorSettingsInterface == null) {
		//	Debug.LogError("No ISensorSettings found on " + gameObject.name);
		//}

		agent = GetComponent<IEnemyAgent>();
	}

	private void Update() {
		//if (sensorBlackboardInterface == null || sensorBlackboardInterface.Target == null) {
		//	return;
		//}

		// PROTOTYPE: Auto acquire target
		if (target == null) {
			TryFindPlayer();
		}

		bool hasLOS = HasLOS();

		// Update blackboard runtime values
		//sensorBlackboardInterface.HasLineOfSight = hasLOS;
	}

	// Uses detection range and vision angle properties to determine whether the target is in the agent's line of sight
	public bool HasLOS() {
		if (target == null) {
			return false;
		}

		// Calculate direction and distance to target
		Vector3 directionToTarget = (target.position - transform.position).normalized;
		float distanceToTarget = CheckDistanceToTarget(transform.position, target.position);

		// Detection Radius
		if (distanceToTarget > detectionRange) {
			return false;
		}

		// Get Vision Cone
		float angle = Vector3.Angle(transform.forward, directionToTarget);
		// Multiply by 0.5 because the full vision cone must be divided into left/right (Vector3.Angle returns the half-angle)
		if (angle > visionAngle * 0.5f) {
			return false;
		}

		// Line of sight raycast
		Vector3 origin = transform.position + Vector3.up * 0.5f;
		if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, detectionRange, visionLayerMask, QueryTriggerInteraction.Ignore)) {
			return hit.transform == target;
		}

		Debug.DrawRay(origin, directionToTarget * detectionRange, Color.red);

		// Success and fails - for debugging purposes
		if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit2, distanceToTarget, visionLayerMask, QueryTriggerInteraction.Ignore)) {
			bool success = hit2.transform == target;
			Debug.DrawRay(origin, directionToTarget * distanceToTarget, success ? Color.green : Color.red);
			return success;
		}

		return false;
	}

	// PROTOTYPE: Find player automatically
	private void TryFindPlayer() {
		GameObject player = GameObject.FindGameObjectWithTag("Player");
		if (player != null) {
			target = player.transform;
		}
	}

	// TODO: Put this in a CombatHelper.cs script
	// Check distance between two given vectors
	public static float CheckDistanceToTarget(Vector3 self, Vector3 target) {
		if (self == null || target == null) {
			return Mathf.Infinity;
		}

		float distance = Vector3.Distance(self, target);

		return distance;
	}
}