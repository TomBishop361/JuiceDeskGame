using UnityEngine;

namespace Game.AI {
	// Handles transform-based flying movement for enemies, including range keeping + orbiting
	// + height adjustment + facing + local separation from nearby units
	[DisallowMultipleComponent]
	public sealed class FlightEnemyMotor : MonoBehaviour {
		[Header("Flight")]
		[Tooltip("Default hover height used when not matching the target's vertical position.")]
		[SerializeField] private float hoverHeight = 3.0f;
		[Tooltip("Horizontal movement speed used for flight movement")]
		[SerializeField] private float moveSpeed = 4.0f;
		[Tooltip("How quickly the enemy lerps toward its desired hover height")]
		[SerializeField] private float heightLerpSpeed = 8.0f;
		[Tooltip("If true, the flyer tracks the target's height instead of staying at a fixed hover level.")]
		[SerializeField] private bool matchPlayerHeight = true;
		[Tooltip("Vertical offset applied when matching the target's height.")]
		[SerializeField] private float heightOffsetFromPlayer = 3.0f;
		[Tooltip("Minimum allowed world-space Y position for the flyer.")]
		[SerializeField] private float minWorldY = -100.0f;
		[Tooltip("Maximum allowed world-space Y position for the flyer.")]
		[SerializeField] private float maxWorldY = 100.0f;

		[Header("Rotation")]
		[Tooltip("Toggle between smooth rotation (Slerp) and snappy rotation (RotateTowards)")]
		[SerializeField] private bool toggleSmoothRotation = true;
		[Tooltip("Turn speed in degrees per second when smooth rotation is disabled.")]
		[SerializeField] private float rotationSpeed = 300.0f;
		[Tooltip("Slerp multiplier used when smooth rotation is enabled.")]
		[SerializeField] private float rotationSmoothing = 8.0f;

		[Header("Separation")]
		[Tooltip("If true, nearby flyers push away from each other to reduce overlap.")]
		[SerializeField] private bool enableSeparation = true;
		[Tooltip("Radius used to detect nearby colliders for separation.")]
		[SerializeField] private float separationRadius = 1.75f;
		[Tooltip("Strength multiplier applied to the separation steering force.")]
		[SerializeField] private float separationWeight = 1.35f;
		[Tooltip("Maximum separation force applied in a single movement update.")]
		[SerializeField] private float maxSeparationForce = 1.15f;

		// Cache of nearby colliders reused to avoid per-frame allocations (performance)
		private static readonly Collider[] SeparationHits = new Collider[16];

		// Last horizontal move speed applied by the motor
		// Useful for animation blending
		public float LastMoveSpeed { get; private set; }

		public void ResetRuntime() {
			LastMoveSpeed = 0.0f;
		}

		// Moves the enemy toward, away from, or around the target to stay within the desired distance band
		public void MaintainRange(Transform target, float distance, float desiredRange, float deadzone, float orbitJitter = 0.15f) {
			if (target == null) {
				return;
			}

			// Calculate direction from the flyer to the target
			Vector3 toTarget = target.position - transform.position;
			toTarget.y = 0.0f;

			if (toTarget.sqrMagnitude < 0.0001f) {
				return;
			}

			Vector3 moveDirection;
			if (distance > desiredRange + deadzone) {
				moveDirection = toTarget.normalized;  // move closer
			}
			else if (distance < desiredRange - deadzone) {
				moveDirection = -toTarget.normalized; // move away
			}
			else {
				// Orbit sideways, with a tiny per-flyer variation so they don't all collapse into same path
				// Inside desired range band, orbit instead of pushing directly in or out
				Vector3 orbitDirection = Vector3.Cross(Vector3.up, toTarget.normalized);
				float jitter = Mathf.Sin((Time.time * 1.5f) + transform.GetInstanceID()) * orbitJitter;
				moveDirection = (orbitDirection + transform.right * jitter).normalized;

				// Create vector perpendicular to target direction (circles rather than moving directly in/out)
				//direction = Vector3.Cross(Vector3.up, toTarget.normalized); // orbit sideways
			}

			MoveInDirection(moveDirection);
			FaceTarget(target.position);
			TickHeight(target);
		}

		// Moves directly away from the target while continuing to face it
		public void MoveAway(Transform target) {
			if (target == null) {
				return;
			}

			// Calculate direction from target to the flyer
			// (flyer - target = away from target)
			Vector3 awayDirection = transform.position - target.position;
			awayDirection.y = 0.0f;

			// Safety - ensures that an away direction exists before moving towards it
			if (awayDirection.sqrMagnitude < 0.0001f) {
				awayDirection = transform.forward;
			}

			MoveInDirection(awayDirection.normalized);
			FaceTarget(target.position);
			TickHeight(target);
		}

		// Moves around the target in an orbital path while maintaining facing and hover height.
		public void Orbit(Transform target, float orbitDirectionSign = 1.0f) {
			if (target == null) {
				return;
			}

			// Calculate direction from the flyer to the target
			// (target - flyer  = toward target)
			Vector3 toTarget = target.position - transform.position;
			toTarget.y = 0.0f;

			if (toTarget.sqrMagnitude < 0.0001f) {
				return;
			}

			Vector3 orbitDirection = Vector3.Cross(Vector3.up, toTarget.normalized) * Mathf.Sign(orbitDirectionSign);
			MoveInDirection(orbitDirection.normalized);
			FaceTarget(target.position);
			TickHeight(target);
		}

		// Rotates the enemy to face a world position using either smooth or turn-speed-based rotation
		public void FaceTarget(Vector3 worldPosition) {
			Vector3 flatDirection = worldPosition - transform.position;
			flatDirection.y = 0.0f;

			// Safety - ensures that a direction exists before rotating towards it
			if (flatDirection.sqrMagnitude < 0.0001f) {
				return;
			}

			Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);

			if (toggleSmoothRotation) {
				// Smooth rotation towards target direction (using Slerp)
				// [BETTER FOR DRONE since floaty hovering]
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothing);
			}
			else {
				// Responsive rotation towards target direction (using RotateTowards)
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}

		// Applies horizontal transform movement in the given direction with optional separation steering added
		public void MoveInDirection(Vector3 baseDirection) {
			if (baseDirection.sqrMagnitude < 0.0001f) {
				LastMoveSpeed = 0.0f;
				return;
			}

			Vector3 desiredDirection = baseDirection.normalized;

			if (enableSeparation) {
				desiredDirection += CalculateSeparation();
			}

			desiredDirection.y = 0.0f;

			if (desiredDirection.sqrMagnitude < 0.0001f) {
				desiredDirection = baseDirection.normalized;
			}

			transform.position += desiredDirection.normalized * (moveSpeed * Time.deltaTime);
			LastMoveSpeed = moveSpeed;
		}

		// Adjusts the enemy's vertical position toward either a fixed hover height or a target-relative height
		private void TickHeight(Transform target) {
			float desiredY = transform.position.y;

			if (matchPlayerHeight && target != null) {
				desiredY = target.position.y + heightOffsetFromPlayer;
			}
			else {
				desiredY = hoverHeight;
			}

			// Keep flyer within world bounds
			desiredY = Mathf.Clamp(desiredY, minWorldY, maxWorldY);

			SetHeight(desiredY, heightLerpSpeed);
		}

		// Sets the height that the flyer should attempt to maintain
		// Called above in TickHeight() function
		private void SetHeight(float desiredY, float followSpeed) {
			Vector3 pos = transform.position;

			pos.y = Mathf.Lerp(pos.y, desiredY, Time.deltaTime * followSpeed);

			transform.position = pos;
		}

		// Calculates a soft avoidance force from nearby colliders to prevent flyers from clustering
		private Vector3 CalculateSeparation() {
			if (separationRadius <= 0.0f) {
				return Vector3.zero;
			}

			int hitCount = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, SeparationHits, Physics.AllLayers, QueryTriggerInteraction.Ignore);

			if (hitCount <= 0) {
				return Vector3.zero;
			}

			Vector3 separation = Vector3.zero;

			for (int i = 0; i < hitCount; i++) {
				Collider hit = SeparationHits[i];
				if (hit == null || hit.transform == transform) {
					continue;
				}

				Vector3 away = transform.position - hit.transform.position;
				away.y = 0.0f;

				float sqrDistance = away.sqrMagnitude;
				if (sqrDistance < 0.0001f) {
					continue;
				}

				float distance = Mathf.Sqrt(sqrDistance);
				float falloff = 1.0f - Mathf.Clamp01(distance / separationRadius);
				separation += away.normalized * falloff;
			}

			if (separation.sqrMagnitude < 0.0001f) {
				return Vector3.zero;
			}

			return Vector3.ClampMagnitude(separation * separationWeight, maxSeparationForce);
		}
	}
}