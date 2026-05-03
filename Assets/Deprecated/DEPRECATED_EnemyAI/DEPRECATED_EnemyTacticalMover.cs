using UnityEngine;

// DEPRECATED


namespace Game.AI {
    // Shared movement adapter used by tactical BT nodes
    // Ground enemies use GroundEnemyMotor/NavMesh
    // Drones use FlightEnemyMotor
    [DisallowMultipleComponent]
    public sealed class EnemyTacticalMover : MonoBehaviour {
		[Header("Modules")]
		[SerializeField] private GroundEnemyMotor groundMotor;
		[SerializeField] private FlightEnemyMotor flightMotor;

		[Header("Arrival")]
		[Tooltip("Distance from destination considered arrived for tactical movement.")]
		[SerializeField] private float arrivalDistance = 1.25f;
		[Tooltip("If true, ground enemies face the supplied focus point while moving to tactical destinations.")]
		[SerializeField] private bool faceFocusWhileMoving = true;

		// Exposes the arrival distance so BT nodes can read the current movement tolerance
		public float ArrivalDistance => arrivalDistance;

		private void Reset() {
			groundMotor = GetComponent<GroundEnemyMotor>();
			flightMotor = GetComponent<FlightEnemyMotor>();
		}

		private void Awake() {
			// Cache the ground motor if this is a NavMesh-based enemy
			if (groundMotor == null) {
				groundMotor = GetComponent<GroundEnemyMotor>();
			}

			// Cache the flight motor if this is a drone/flying enemy
			if (flightMotor == null) {
				flightMotor = GetComponent<FlightEnemyMotor>();
			}
		}

		public bool TickMoveTo(Vector3 destination, Transform focus) {
			// If a focus transform exists, face it while moving
			// Otherwise, face the destination itself
			Vector3 focusPoint = focus != null ? focus.position : destination;

			return TickMoveTo(destination, focusPoint);
		}

		public bool TickMoveTo(Vector3 destination, Vector3 focusPoint) {
			// Check how close the enemy currently is to the target destination
			float distance = Vector3.Distance(transform.position, destination);

			// Ground enemies move using the NavMesh motor
			if (groundMotor != null && groundMotor.AgentReady) {
				groundMotor.Chase(destination);

				// Useful for enemies that should keep pressure/facing while repositioning
				if (faceFocusWhileMoving) {
					groundMotor.FaceTarget(focusPoint);
				}

				return distance <= arrivalDistance;
			}

			// Flying enemies move using the flight motor
			if (flightMotor != null) {
				flightMotor.MoveToPoint(destination, focusPoint, destination.y);
				return distance <= arrivalDistance;
			}

			// If no motor exists, still report arrival based on distance
			// This keeps BT nodes from hard failing in the case of incomplete prefabs
			return distance <= arrivalDistance;
		}

		public void Stop(Vector3 focusPoint) {
			// Stop NavMesh movement for ground enemies
			if (groundMotor != null) {
				groundMotor.Stop();
			}

			// Flying enemies hold position but keep facing the focus point
			if (flightMotor != null) {
				flightMotor.HoldPosition(focusPoint, transform.position.y);
			}
		}
	}
}