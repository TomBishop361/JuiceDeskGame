using UnityEngine;

namespace Game.AI {
	// Handles transform-based flying movement for enemies, including range keeping + orbiting
	// + dynamic height adjustment + obstacle abvoidance + local separation from nearby units
	[DisallowMultipleComponent]
	public sealed class FlightEnemyMotor : MonoBehaviour {
		[Header("Flight")]
		[Tooltip("Default hover height used when not matching the target's vertical position.")]
		[SerializeField] private float hoverHeight = 3.0f;
		[Tooltip("Top movement speed used for flight steering")]
		[SerializeField] private float maxMoveSpeed = 4.0f;
		[Tooltip("How quickly the drone ramps up to full speed while moving.")]
		[SerializeField] private float acceleration = 10.0f;
		[Tooltip("How quickly the drone slows down again when stopping or changing pace.")]
		[SerializeField] private float braking = 14.0f;
		[Tooltip("How quickly the enemy steers toward its desired flight direction")]
		[SerializeField] private float steeringLerpSpeed = 10.0f;
		[Tooltip("If true, the flyer tries to stay around the player's height instead of staying at a fixed hover level.")]
		[SerializeField] private bool matchPlayerHeight = true;
		[Tooltip("Vertical offset applied when matching the target's height.")]
		[SerializeField] private float heightOffsetFromPlayer = 3.0f;

		[Header("Height Fluctuation")]
		[Tooltip("Strength of the continuous hover bob added to the desired height.")]
		[SerializeField] private float heightBobAmplitude = 0.65f;
		[Tooltip("Speed of the continuous hover bob.")]
		[SerializeField] private float heightBobFrequency = 1.15f;
		[Tooltip("Random extra height offset range that changes every so often for less predictable movement.")]
		[SerializeField] private Vector2 randomHeightOffsetRange = new Vector2(-0.75f, 1.25f);
		[Tooltip("How often a new random hover height is chosen.")]
		[SerializeField] private Vector2 randomHeightRetargetInterval = new Vector2(1.0f, 2.25f);
		[Tooltip("Minimum allowed world-space Y position for the flyer.")]
		[SerializeField] private float minWorldY = -100.0f;
		[Tooltip("Maximum allowed world-space Y position for the flyer.")]
		[SerializeField] private float maxWorldY = 100.0f;

		[Header("Obstacle Probes")]
		[Tooltip("Level geometry layers the flyer should avoid.")]
		[SerializeField] private LayerMask obstacleMask = ~0;
		[Tooltip("Radius used for spherecast-based obstacle checks.")]
		[SerializeField] private float bodyRadius = 0.35f;
		[Tooltip("Vertical offset applied to the obstacle probe origin.")]
		[SerializeField] private float probeOriginLift = 0.1f;
		[Tooltip("Main forward obstacle look-ahead distance.")]
		[SerializeField] private float forwardProbeDistance = 2.25f;
		[Tooltip("Side probe distance for left/right whiskers.")]
		[SerializeField] private float sideProbeDistance = 1.6f;
		[Tooltip("Vertical escape probe distance used when trying to rise over obstacles.")]
		[SerializeField] private float verticalProbeDistance = 1.8f;
		[Tooltip("Angular spread of the side obstacle probes. Higher values moves the whiskers wider apart.")]
		[SerializeField] private float sideProbeSpread = 0.6f;
		[Tooltip("How much upward direction is added when checking whether the drone can fly over an obstacle.")]
		[SerializeField] private float upwardProbeBias = 0.75f;

		[Header("Avoidance Steering")]
		[Tooltip("How strongly obstacle avoidance influences the final steering direction.")]
		[SerializeField] private float avoidanceWeight = 2.25f;
		[Tooltip("How strongly a forward obstacle hit contributes to the avoidance steering force.")]
		[SerializeField] private float forwardProbeWeight = 1.35f;
		[Tooltip("How strongly each side obstacle probe contributes to the avoidance steering force.")]
		[SerializeField] private float sideProbeWeight = 1.0f;
		[Tooltip("How strongly the drone prefers climbing when forward space is blocked.")]
		[SerializeField] private float upwardEscapeBias = 1.35f;
		[Tooltip("How strongly the drone chooses side-stepping around obstacles.")]
		[SerializeField] private float sidewaysEscapeBias = 1.1f;
		[Tooltip("Multiplier applied to upward escape when both side routes are blocked.")]
		[SerializeField] private float boxedInUpwardFactor = 0.5f;
		[Tooltip("Maximum raw obstacle avoidance force.")]
		[SerializeField] private float maxAvoidanceForce = 2.5f;

		[Header("Ceiling Clamp")]
		[Tooltip("If true, the flyer checks for ceilings above it and clamps its height below them.")]
		[SerializeField] private bool enableCeilingClamp = true;
		[Tooltip("How far upward to check for a ceiling above the drone.")]
		[SerializeField] private float ceilingProbeDistance = 3.5f;
		[Tooltip("Minimum clearance kept below a detected ceiling.")]
		[SerializeField] private float ceilingClearance = 0.25f;

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
		[Tooltip("Should contain only the drone / enemy layer.")]
		[SerializeField] private LayerMask separationMask = ~0;
		[Tooltip("Radius used to detect nearby colliders for separation.")]
		[SerializeField] private float separationRadius = 1.75f;
		[Tooltip("Strength multiplier applied to the separation steering force.")]
		[SerializeField] private float separationWeight = 1.35f;
		[Tooltip("Maximum separation force applied in a single movement update.")]
		[SerializeField] private float maxSeparationForce = 1.15f;
		[Tooltip("How much vertical difference contributes to separation. 0 = horizontal only | 1 = full 3D.")]
		[SerializeField, Range(0.0f, 1.0f)] private float separationVerticalInfluence = 0.35f;
		[Tooltip("Extra multiplier applied when drones are already very close to each other.")]
		[SerializeField] private float closeRepelMultiplier = 2.5f;
		[Tooltip("How close drones must be, before strong repel begins.")]
		[SerializeField] private float closeRepelDistanceMultiplier = 2.2f;

		[Header("Orbit Tuning")]
		[Tooltip("How far the drone moves sideways when choosing its local orbit point around the target.")]
		[SerializeField] private float orbitStepDistance = 2.25f;
		[Tooltip("Side bias added while approaching desired range. Higher values make it arc in more, instead of flying straight at the target.")]
		[SerializeField] private float approachSideBias = 0.75f;
		[Tooltip("Speed of the small orbit wobble. Higher values make orbit motion change direction more quickly.")]
		[SerializeField] private float orbitWobbleFrequency = 1.5f;
		[Tooltip("Per-drone random range offset so not every drone tries to hold the exact same combat ring.")]
		[SerializeField] private float orbitRangeOffsetRange = 0.6f;
		[Tooltip("Per-drone random orbit step offset so orbit movement is not identical across all drones.")]
		[SerializeField] private float orbitStepOffsetRange = 0.4f;

		[Header("Retreat Tuning")]
		[Tooltip("Distance used when backing away from a nearby target.")]
		[SerializeField] private float retreatDistance = 2.5f;
		[Tooltip("Extra height added while retreating.")]
		[SerializeField] private float retreatHeightBonus = 0.4f;

		[Header("Debug Gizmos")]
		[Tooltip("Master toggle for all flight debug visualisations.")]
		[SerializeField] private bool showDebugGizmos = true;
		[Tooltip("If true, gizmos are only drawn when the drone is selected.")]
		[SerializeField] private bool drawGizmosOnlyWhenSelected = true;
		[Tooltip("Draws the drone body sphere used to determine probe thickness and ceiling clearance.")]
		[SerializeField] private bool debugDrawBodyRadius = true;
		[Tooltip("Draws the raised probe origin used for obstacle spherecasts.")]
		[SerializeField] private bool debugDrawProbeOrigin = true;
		[Tooltip("Draws the forward obstacle probe.")]
		[SerializeField] private bool debugDrawForwardProbe = true;
		[Tooltip("Draws the left and right side probes.")]
		[SerializeField] private bool debugDrawSideProbes = true;
		[Tooltip("Draws the upward-biased escape probe used to determine fly-over clearance.")]
		[SerializeField] private bool debugDrawVerticalProbe = true;
		[Tooltip("Draws the local ceiling clamp check and the resulting clearance point.")]
		[SerializeField] private bool debugDrawCeilingClamp = true;
		[Tooltip("Draws the nearby separation radius used to push drones apart.")]
		[SerializeField] private bool debugDrawSeparationRadius = true;
		[Tooltip("Draws the current smoothed move direction.")]
		[SerializeField] private bool debugDrawMoveDirection = true;
		[Tooltip("Draws the current desired hover height anchor.")]
		[SerializeField] private bool debugDrawDesiredHeight = true;

		// Cache of nearby colliders reused to avoid per-frame allocations (performance)
		private static readonly Collider[] SeparationHits = new Collider[32];

		// Smoothed move direction so steering doesn't feel twitchy
		private Vector3 currentMoveDirection = Vector3.forward;
		// Randomised hover phase so multiple drones do not bob in sync
		private float bobPhase;
		// Randomly picks clockwise or anticlockwise orbit direction
		private float orbitDirectionSign = 1.0f;
		// Current randomised vertical hover offset
		private float randomHeightOffset;
		// Time at which the random height offset is refreshed
		private float nextHeightRetargetTime;

		private float uniqueOrbitRangeOffset;
		private float uniqueOrbitStepOffset;

		// Last horizontal move speed applied by the motor
		// Useful for animation blending
		public float LastMoveSpeed { get; private set; }

		// Runtime speed that is eased with acceleration / braking instead of snapping instantly
		private float currentMoveSpeed;
		// Tracks whether a movement command was issued this frame
		private bool receivedMovementCommandThisFrame;

		private void Awake() {
			SeedRuntime();
		}

		private void LateUpdate() {
			// If the AI did not request movement this frame decrease speed back to zero (idle)
			// This is so animations and visual polish brake cleanly instead of snapping
			if (receivedMovementCommandThisFrame == false) {
				currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0.0f, braking * Time.deltaTime);
				LastMoveSpeed = currentMoveSpeed;
			}

			receivedMovementCommandThisFrame = false;
		}

		public void ResetRuntime() {
			LastMoveSpeed = 0.0f;
			currentMoveSpeed = 0.0f;
			receivedMovementCommandThisFrame = false;
			SeedRuntime();
		}

		private void SeedRuntime() {
			// Initialise movement direction so the steering slerp has a valid starting direction
			if (transform.forward.sqrMagnitude > 0.0001f) {
				currentMoveDirection = transform.forward.normalized;
			}
			else {
				currentMoveDirection = Vector3.forward;
			}

			// Give each drone unique bob timing and orbit side preference
			bobPhase = Random.Range(0.0f, Mathf.PI * 2.0f);
			orbitDirectionSign = Random.value < 0.5f ? -1.0f : 1.0f;

			// Give each drone a slightly different preferred orbit ring and orbit step size
			// This is so they do not all try to occupy the exact same space around the player
			uniqueOrbitRangeOffset = Random.Range(-orbitRangeOffsetRange, orbitRangeOffsetRange);
			uniqueOrbitStepOffset = Random.Range(-orbitStepOffsetRange, orbitStepOffsetRange);

			PickNewHeightOffset();
		}

		// Keeps the flyer near its preferred horizontal combat distance from the target
		// Too far = closes back toward the desired range
		// Too close = backs out
		// In desired range band = orbit
		public void MaintainRange(Transform target, float planarDistance, float desiredRange, float deadzone, float orbitJitter = 0.15f) {
			if (target == null) {
				return;
			}

			// Calculate direction from the flyer to the target
			Vector3 toTarget = target.position - transform.position;

			// Ignore vertical difference for range control so hover bob does not constantly make the drone think it is too close or too far
			Vector3 planarToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);

			if (planarToTarget.sqrMagnitude < 0.0001f) {
				planarToTarget = transform.forward;
			}

			Vector3 radialDir = planarToTarget.normalized;

			// Tangent direction around the player used for orbiting
			Vector3 orbitDir = Vector3.Cross(Vector3.up, radialDir) * orbitDirectionSign;

			float localDesiredRange = Mathf.Max(0.5f, desiredRange + uniqueOrbitRangeOffset);
			float localOrbitStepDistance = Mathf.Max(0.5f, orbitStepDistance + uniqueOrbitStepOffset);
			float localApproachSideBias = Mathf.Max(0.0f, approachSideBias + (uniqueOrbitStepOffset * 0.35f));

			float jitter = Mathf.Sin((Time.time * orbitWobbleFrequency) + bobPhase) * orbitJitter;

			Vector3 desiredPoint;
			if (planarDistance > desiredRange + deadzone) {
				// Too far:
				// Close toward the desired ring rather than driving straight into the target
				desiredPoint = target.position - radialDir * localDesiredRange;
				desiredPoint += orbitDir * localApproachSideBias;
			}
			else if (planarDistance < desiredRange - deadzone) {
				// Too close:
				// Pull back toward the desired ring
				desiredPoint = target.position - radialDir * localDesiredRange;
			}
			else {
				// Inside the desired ring:
				// orbit rather than moving straight in/out
				desiredPoint = transform.position + (orbitDir + transform.right * jitter) * localOrbitStepDistance;
			}

			// Apply dynamic hover height so the flyer does not remain locked at one Y level
			desiredPoint.y = GetDesiredY(target.position.y);

			MoveTowardsPoint(desiredPoint, target.position);
		}

		// Moves directly away from the target while continuing to face it
		public void MoveAway(Transform target) {
			if (target == null) {
				return;
			}

			// Calculate direction from target to the flyer
			Vector3 awayDir = transform.position - target.position;

			// Safety - ensures that an away direction exists before moving towards it
			if (awayDir.sqrMagnitude < 0.0001f) {
				awayDir = -transform.forward;
			}

			Vector3 desiredPoint = transform.position + awayDir.normalized * retreatDistance;

			// When backing away slightly favour gaining height as well
			desiredPoint.y = GetDesiredY(target.position.y) + retreatHeightBonus;

			MoveTowardsPoint(desiredPoint, target.position);
		}

		// Moves the flyer sideways around the target without trying to change range much
		public void Orbit(Transform target, float orbitDirectionSign = 1.0f) {
			if (target == null) {
				return;
			}

			// Calculate horizontal direction from the flyer to the target
			Vector3 toTarget = target.position - transform.position;
			Vector3 planarToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);

			if (planarToTarget.sqrMagnitude < 0.0001f) {
				planarToTarget = transform.forward;
			}

			// Tangent direction around the target
			Vector3 orbitDir = Vector3.Cross(Vector3.up, planarToTarget.normalized) * Mathf.Sign(orbitDirectionSign);
			float localOrbitStepDistance = Mathf.Max(0.5f, orbitStepDistance + uniqueOrbitStepOffset);

			Vector3 desiredPoint = transform.position + orbitDir * localOrbitStepDistance;
			desiredPoint.y = GetDesiredY(target.position.y);

			MoveTowardsPoint(desiredPoint, target.position);
		}

		// Moves the flyer to a specific point
		public void MoveToPoint(Vector3 worldPoint, Vector3 facePoint, float anchorY) {
			Vector3 desiredPoint = worldPoint;
			desiredPoint.y = GetDesiredY(anchorY);

			MoveTowardsPoint(desiredPoint, facePoint);
		}

		// Anchors flyer to a specific Y position to avoid clipping roofs
		public void HoldPosition(Vector3 facePoint, float anchorY) {
			receivedMovementCommandThisFrame = true;

			Vector3 pos = transform.position;

			pos.y = GetDesiredY(anchorY);
			pos.y = GetCeilingClampedY(pos);
			pos.y = Mathf.Clamp(pos.y, minWorldY, maxWorldY);

			transform.position = pos;
			currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0.0f, braking * Time.deltaTime);
			LastMoveSpeed = currentMoveSpeed;

			FaceTarget(facePoint);
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

		// Moves toward a chosen point using local steering
		// This blends intent with obstacle avoidance and drone separation
		private void MoveTowardsPoint(Vector3 desiredPoint, Vector3 facePoint) {
			receivedMovementCommandThisFrame = true;

			Vector3 toPoint = desiredPoint - transform.position;

			if (toPoint.sqrMagnitude < 0.01f) {
				currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0.0f, braking * Time.deltaTime);
				LastMoveSpeed = currentMoveSpeed;
				FaceTarget(facePoint);
				return;
			}

			// Raw intended move direction
			Vector3 desiredDir = toPoint.normalized;

			// Steering force that pushes away from nearby level geometry (using spherecast whiskers)
			Vector3 avoidance = CalculateObstacleAvoidance(desiredDir);
			// Steering force that pushes away from nearby drones so they do not stack up
			Vector3 separation = enableSeparation ? CalculateSeparation() : Vector3.zero;

			// Combine intended movement with local steering forces
			Vector3 finalDir = desiredDir + (avoidance * avoidanceWeight) + separation;
			if (finalDir.sqrMagnitude < 0.0001f) {
				finalDir = desiredDir;
			}

			// Smooth direction changes so movement stays predictable
			currentMoveDirection = Vector3.Slerp(currentMoveDirection, finalDir.normalized, Time.deltaTime * steeringLerpSpeed).normalized;

			float speedChange = currentMoveSpeed < maxMoveSpeed ? acceleration : braking;
			currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, maxMoveSpeed, speedChange * Time.deltaTime);
			transform.position += currentMoveDirection * (currentMoveSpeed * Time.deltaTime);

			// Clamp Y (local and world space) in case the drone ever gets pushed too high or too low
			Vector3 clampedPosition = transform.position;
			clampedPosition.y = GetCeilingClampedY(clampedPosition);
			clampedPosition.y = Mathf.Clamp(clampedPosition.y, minWorldY, maxWorldY);
			transform.position = clampedPosition;

			LastMoveSpeed = currentMoveSpeed;
			FaceTarget(facePoint);
		}

		//// Moves toward a world point and faces that same point
		//// Used for investigate / last-seen-position movement when LOS is lost
		//public void MoveTowardsPoint(Vector3 worldPoint) {
		//	MoveTowardsPoint(worldPoint, worldPoint);
		//}

		// Builds a local steering force using spherecast whiskers so the flyer can slide around
		// walls and choose to rise over shorter obstacles when a clear path is possible
		private Vector3 CalculateObstacleAvoidance(Vector3 desiredDirection) {
			Vector3 origin = transform.position + Vector3.up * probeOriginLift;

			// Planar forward for left/right whiskers
			Vector3 flatForward = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);
			if (flatForward.sqrMagnitude < 0.0001f) {
				flatForward = transform.forward;
			}
			flatForward.Normalize();

			Vector3 right = Vector3.Cross(Vector3.up, flatForward);
			if (right.sqrMagnitude < 0.0001f) {
				right = transform.right;
			}
			right.Normalize();

			Vector3 avoidance = Vector3.zero;

			// Probe forward and on both forward diagonals
			bool forwardBlocked = Probe(origin, desiredDirection, forwardProbeDistance, forwardProbeWeight, ref avoidance);
			bool leftBlocked = Probe(origin, (desiredDirection - right * sideProbeSpread).normalized, sideProbeDistance, sideProbeWeight, ref avoidance);
			bool rightBlocked = Probe(origin, (desiredDirection + right * sideProbeSpread).normalized, sideProbeDistance, sideProbeWeight, ref avoidance);

			if (forwardBlocked) {
				// If directly blocked try going up and over first
				bool upClear = !IsBlocked(origin, (desiredDirection + Vector3.up * upwardProbeBias).normalized, verticalProbeDistance);

				if (upClear) {
					avoidance += Vector3.up * upwardEscapeBias;
				}
				else {
					// If upward is not clear then prefer whichever side is open
					if (leftBlocked == false) {
						avoidance -= right * sidewaysEscapeBias;
					}

					if (rightBlocked == false) {
						avoidance += right * sidewaysEscapeBias;
					}
					// Worst case:
					// blocked in on both sides then still bias slightly upward
					if (leftBlocked && rightBlocked) {
						avoidance += Vector3.up * (upwardEscapeBias * boxedInUpwardFactor);
					}
				}
			}

			return Vector3.ClampMagnitude(avoidance, maxAvoidanceForce);
		}

		// Casts a sphere in the given direction and adds avoidance force if something is hit
		// Return true if blocked
		// NOTE: avoidance is passed in by ref so the caller can receive the accumulated force
		private bool Probe(Vector3 origin, Vector3 direction, float distance, float weight, ref Vector3 avoidance) {
			if (!Physics.SphereCast(origin, bodyRadius, direction, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore)) {
				return false;
			}

			// Ignore self hits
			if (hit.transform == transform) {
				return false;
			}

			// Closer obstacles contribute stronger avoidance than distant ones
			float closeness = 1.0f - Mathf.Clamp01(hit.distance / distance);
			avoidance += hit.normal * (weight * closeness);

			return true;
		}

		// Returns whether a direction is blocked by obstacle geometry
		private bool IsBlocked(Vector3 origin, Vector3 direction, float distance) {
			if (Physics.SphereCast(origin, bodyRadius, direction, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore) == false) {
				return false;
			}

			return hit.transform != transform;
		}

		// Calculates the flyer’s desired hover height
		// This can track target height + hover bob + refreshed random offset
		private float GetDesiredY(float anchorY) {
			// Occasionally refresh the random hover offset
			if (Time.time >= nextHeightRetargetTime) {
				PickNewHeightOffset();
			}

			// Track target height if enabled
			// Otherwise default to fixed hover height
			float desiredY = matchPlayerHeight ? anchorY + heightOffsetFromPlayer : hoverHeight;

			// Layer sine bob + changing random offset so the drone does not remain at a constant height
			float bob = Mathf.Sin((Time.time * heightBobFrequency) + bobPhase) * heightBobAmplitude;
			desiredY += bob + randomHeightOffset;

			return Mathf.Clamp(desiredY, minWorldY, maxWorldY);
		}

		// Picks a new random hover offset and handles the next refresh time
		private void PickNewHeightOffset() {
			randomHeightOffset = Random.Range(randomHeightOffsetRange.x, randomHeightOffsetRange.y);
			
			nextHeightRetargetTime = Time.time + Random.Range(randomHeightRetargetInterval.x, randomHeightRetargetInterval.y);
		}

		// Compute ceiling above to prevent rising into roofs
		private float GetCeilingClampedY(Vector3 position) {
			float clampedY = position.y;

			if (enableCeilingClamp == false) {
				return clampedY;
			}

			if (Physics.SphereCast(position, bodyRadius, Vector3.up, out RaycastHit hit, ceilingProbeDistance, obstacleMask, QueryTriggerInteraction.Ignore)) {
				clampedY = Mathf.Min(clampedY, position.y + hit.distance - ceilingClearance);
			}

			return clampedY;
		}

		// Calculates a soft avoidance force from nearby colliders to prevent flyers from clustering
		private Vector3 CalculateSeparation() {
			if (separationRadius <= 0.0f) {
				return Vector3.zero;
			}

			int hitCount = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, SeparationHits, separationMask, QueryTriggerInteraction.Collide);

			if (hitCount <= 0) {
				return Vector3.zero;
			}

			Vector3 separation = Vector3.zero;

			for (int i = 0; i < hitCount; i++) {
				Collider hit = SeparationHits[i];
				if (hit == null) {
					continue;
				}

				// Ignore this drone and any of its child colliders
				if (hit.transform == transform || hit.transform.IsChildOf(transform)) {
					continue;
				}

				Vector3 otherPosition = hit.bounds.center;
				Vector3 awayDir = transform.position - hit.transform.position;

				// Keep separation mostly horizontal but still allow some vertical push
				// This is so drones do not sit inside each other while ascending or descending
				awayDir.y *= separationVerticalInfluence;

				float sqrDistance = awayDir.sqrMagnitude;
				if (sqrDistance < 0.0001f) {
					continue;
				}

				float distance = Mathf.Sqrt(sqrDistance);
				float falloff = 1.0f - Mathf.Clamp01(distance / separationRadius);

				// Stronger repel when drones are already very close
				float closeRepelDistance = bodyRadius * closeRepelDistanceMultiplier;
				if (distance < closeRepelDistance) {
					falloff *= closeRepelMultiplier;
				}

				separation += awayDir.normalized * falloff;
			}

			if (separation.sqrMagnitude < 0.0001f) {
				return Vector3.zero;
			}

			return Vector3.ClampMagnitude(separation * separationWeight, maxSeparationForce);
		}

		// - DEBUG GIZMOS - 

		private void OnDrawGizmos() {
			if (drawGizmosOnlyWhenSelected || showDebugGizmos == false) {
				return;
			}

			DrawDebugGizmos();
		}

		private void OnDrawGizmosSelected() {
			if (showDebugGizmos == false) {
				return;
			}

			DrawDebugGizmos();
		}

		private void DrawDebugGizmos() {
			Vector3 origin = transform.position + Vector3.up * probeOriginLift;
			Vector3 moveDir = GetDebugMoveDirection();
			Vector3 flatForward = Vector3.ProjectOnPlane(moveDir, Vector3.up);
			if (flatForward.sqrMagnitude < 0.0001f) {
				flatForward = transform.forward;
			}
			flatForward.Normalize();

			Vector3 right = Vector3.Cross(Vector3.up, flatForward);
			if (right.sqrMagnitude < 0.0001f) {
				right = transform.right;
			}
			right.Normalize();

			if (debugDrawBodyRadius) {
				Gizmos.color = new Color(0.15f, 0.9f, 1.0f, 0.85f);
				Gizmos.DrawWireSphere(transform.position, bodyRadius);
			}

			if (debugDrawProbeOrigin) {
				Gizmos.color = new Color(1.0f, 1.0f, 1.0f, 0.9f);
				Gizmos.DrawLine(transform.position, origin);
				Gizmos.DrawWireSphere(origin, bodyRadius * 0.5f);
			}

			if (debugDrawForwardProbe) {
				DrawProbeGizmo(origin, moveDir, forwardProbeDistance, new Color(1.0f, 0.35f, 0.35f, 0.9f));
			}

			if (debugDrawSideProbes) {
				DrawProbeGizmo(origin, (moveDir - right * sideProbeSpread).normalized, sideProbeDistance, new Color(1.0f, 0.7f, 0.2f, 0.9f));
				DrawProbeGizmo(origin, (moveDir + right * sideProbeSpread).normalized, sideProbeDistance, new Color(1.0f, 0.7f, 0.2f, 0.9f));
			}

			if (debugDrawVerticalProbe) {
				DrawProbeGizmo(origin, (moveDir + Vector3.up * upwardProbeBias).normalized, verticalProbeDistance, new Color(0.45f, 1.0f, 0.45f, 0.9f));
			}

			if (debugDrawCeilingClamp) {
				DrawCeilingClampGizmo();
			}

			if (debugDrawSeparationRadius && enableSeparation) {
				Gizmos.color = new Color(0.75f, 0.3f, 1.0f, 0.85f);
				Gizmos.DrawWireSphere(transform.position, separationRadius);
			}

			if (debugDrawMoveDirection) {
				Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 1.0f);
				Gizmos.DrawLine(transform.position, transform.position + moveDir * 2.0f);
			}

			if (debugDrawDesiredHeight) {
				float desiredY = GetDesiredY(Application.isPlaying ? GetCurrentHeightAnchor() : transform.position.y);
				Vector3 a = transform.position;
				Vector3 b = new Vector3(transform.position.x, desiredY, transform.position.z);
				Gizmos.color = new Color(0.25f, 1.0f, 1.0f, 0.9f);
				Gizmos.DrawLine(a, b);
				Gizmos.DrawWireSphere(b, Mathf.Max(0.05f, bodyRadius * 0.35f));
			}
		}

		private void DrawProbeGizmo(Vector3 origin, Vector3 direction, float distance, Color color) {
			Gizmos.color = color;
			Gizmos.DrawLine(origin, origin + direction * distance);
			Gizmos.DrawWireSphere(origin + direction * distance, bodyRadius);
		}

		private void DrawCeilingClampGizmo() {
			Vector3 origin = transform.position;
			Gizmos.color = new Color(0.85f, 0.2f, 1.0f, 0.9f);
			Gizmos.DrawLine(origin, origin + Vector3.up * ceilingProbeDistance);

			if (enableCeilingClamp == false) {
				return;
			}

			if (Physics.SphereCast(origin, bodyRadius, Vector3.up, out RaycastHit hit, ceilingProbeDistance, obstacleMask, QueryTriggerInteraction.Ignore)) {
				Vector3 hitPoint = origin + Vector3.up * hit.distance;
				Vector3 clampedPoint = new Vector3(origin.x, origin.y + hit.distance - ceilingClearance, origin.z);

				Gizmos.color = new Color(1.0f, 0.1f, 1.0f, 0.95f);
				Gizmos.DrawWireSphere(hitPoint, bodyRadius);
				Gizmos.DrawLine(hitPoint, clampedPoint);

				Gizmos.color = new Color(1.0f, 0.55f, 1.0f, 0.95f);
				Gizmos.DrawWireCube(clampedPoint, new Vector3(bodyRadius * 2.0f, 0.03f, bodyRadius * 2.0f));
			}
		}

		private Vector3 GetDebugMoveDirection() {
			if (Application.isPlaying && currentMoveDirection.sqrMagnitude > 0.0001f) {
				return currentMoveDirection.normalized;
			}

			if (transform.forward.sqrMagnitude > 0.0001f) {
				return transform.forward.normalized;
			}

			return Vector3.forward;
		}

		private float GetCurrentHeightAnchor() {
			return transform.position.y - heightOffsetFromPlayer;
		}

		// - DEPRECATED - 

		//// Applies horizontal transform movement in the given direction with optional separation steering added
		//public void MoveInDirection(Vector3 baseDirection) {
		//	if (baseDirection.sqrMagnitude < 0.0001f) {
		//		LastMoveSpeed = 0.0f;
		//		return;
		//	}

		//	Vector3 desiredDirection = baseDirection.normalized;

		//	if (enableSeparation) {
		//		desiredDirection += CalculateSeparation();
		//	}

		//	desiredDirection.y = 0.0f;

		//	if (desiredDirection.sqrMagnitude < 0.0001f) {
		//		desiredDirection = baseDirection.normalized;
		//	}

		//	transform.position += desiredDirection.normalized * (maxMoveSpeed * Time.deltaTime);
		//	LastMoveSpeed = maxMoveSpeed;
		//}

		//// Adjusts the enemy's vertical position toward either a fixed hover height or a target-relative height
		//private void TickHeight(Transform target) {
		//	float desiredY = transform.position.y;

		//	if (matchPlayerHeight && target != null) {
		//		desiredY = target.position.y + heightOffsetFromPlayer;
		//	}
		//	else {
		//		desiredY = hoverHeight;
		//	}

		//	// Keep flyer within world bounds
		//	desiredY = Mathf.Clamp(desiredY, minWorldY, maxWorldY);

		//	SetHeight(desiredY, heightLerpSpeed);
		//}

		//// Sets the height that the flyer should attempt to maintain
		//// Called above in TickHeight() function
		//private void SetHeight(float desiredY, float followSpeed) {
		//	Vector3 pos = transform.position;

		//	pos.y = Mathf.Lerp(pos.y, desiredY, Time.deltaTime * followSpeed);

		//	transform.position = pos;
		//}
	}
}