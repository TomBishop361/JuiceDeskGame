using System.Collections;
using UnityEngine;
using Game.Audio;

namespace Game.AI.Sword {
	// Explicit lunge phases make it easier to control damage reactions + movement + animation events + recovery separately
	public enum SwordLungePhase {
		None,
		Windup,
		Dash,
		Recovery
	}

	// Handles the sword enemy's lunge attack, including cooldown checks + locked windup aiming + clamped dash movement + hitbox control
	// + visible telegraphing + hit reactions + recovery
	[DisallowMultipleComponent]
	public sealed class SwordLungeAttack : MonoBehaviour {
		[Header("References")]
		[Tooltip("Hitbox used during the lunge attack active frames.")]
		[SerializeField] private MeleeHitbox lungeHitbox;
		[Tooltip("Rigidbody used to drive the physical lunge dash.")]
		[SerializeField] private Rigidbody lungeRB;

		[Header("Damage")]
		[Tooltip("Attack data applied by the lunge hitbox.")]
		[SerializeField] private AttackData lungeAttackData = new AttackData();

		[Header("Range")]
		[Tooltip("Minimum distance required before the sword enemy can start a lunge.")]
		[SerializeField] private float lungeMinRange = 2.8f;
		[Tooltip("Maximum distance at which the sword enemy can still start a lunge.")]
		[SerializeField] private float lungeMaxRange = 6.5f;

		[Header("Chance")]
		[Tooltip("Delay before the enemy is allowed to roll lunge chance again after deciding not to lunge. This prevents chance checks from happening every frame while in range.")]
		[SerializeField] private float failedLungeChanceRetryDelay = 0.65f;
		[Tooltip("Chance that the enemy will actually commit to a lunge when all normal lunge requirements are valid. 1 = always lunge, 0 = never lunge.")]
		[SerializeField][Range(0.0f, 1.0f)] private float lungeChance = 0.65f;

		[Header("Timing")]
		[Tooltip("Cooldown after a completed or attempted lunge.")]
		[SerializeField] private float lungeCooldown = 2.2f;
		[Tooltip("Windup time before the lunge dash begins. The aim point is locked at the start of this phase.")]
		[SerializeField] private float lungeWindupTime = 0.30f;
		[Tooltip("Maximum duration of the active dash movement before the code forcibly ends the dash.")]
		[SerializeField] private float lungeDuration = 0.22f;
		[Tooltip("\"Recovery time after the lunge dash has finished. The enemy is punishable during this phase.")]
		[SerializeField] private float lungeRecoveryTime = 0.4f;
		[Tooltip("Small extra lock time added after Windup + Dash + Recovery. This is a safety buffer, not the main attack lock duration.")]
		[SerializeField] private float lungeAttackLockBuffer = 0.05f;
		[Tooltip("Extra time allowed past lungeDuration before the dash is force-ended if the animation dash-end event does not fire.")]
		[SerializeField] private float lungeEventFailsafeBuffer = 0.05f;

		[Header("Aim Fairness")]
		[Tooltip("If true, the lunge aims at where the player was a short time ago instead of where they are now.")]
		[SerializeField] private bool usePreviousTargetPosition = true;
		[Tooltip("How far back in time to sample the player's position when locking the lunge aim point. Higher values make the lunge easier to dodge.")]
		[SerializeField] private float lungeAimLagTime = 0.20f;
		[Tooltip("How far short of the locked aim point the lunge should aim. This prevents the enemy from landing perfectly on the player.")]
		[SerializeField] private float lungeUndershootDistance = 0.75f;
		[Tooltip("Vertical offset used when creating the target aim point.")]
		[SerializeField] private float lungeAimHeight = 1.0f;
		[Tooltip("Minimum time between stored target-position history samples.")]
		[SerializeField] private float targetHistorySampleInterval = 0.05f;

		[Header("Movement")]
		[Tooltip("Maximum world-space distance the enemy can travel during the dash. The dash will stop early if it reaches this distance.")]
		[SerializeField] private float lungeDashDistance = 5.0f;
		[Tooltip("Units per second travelled during the dash. If this is zero or less, speed is derived from lungeDashDistance / lungeDuration.")]
		[SerializeField] private float lungeDashSpeed = 24.0f;
		[Tooltip("Maximum horizontal angle from the enemy's current facing direction allowed at launch. If the locked aim point is outside this angle, the enemy lunges forward instead.")]
		[SerializeField] private float lungeMaxLaunchAngle = 45.0f;
		[Tooltip("How close the Rigidbody must be to the clamped dash endpoint before the dash ends early.")]
		[SerializeField] private float dashEndpointTolerance = 0.05f;

		[Header("Hit Reaction")]
		[Tooltip("If true, the lunge immediately stops when the lunge hitbox successfully hits a target.")]
		[SerializeField] private bool stopLungeOnHit = true;
		[Tooltip("If stopLungeOnHit is false, this multiplier slows the remaining dash after the lunge hits a target.")]
		[SerializeField] private float hitSlowSpeedMultiplier = 0.35f;
		[Tooltip("If stopLungeOnHit is false, this is how long the dash stays slowed after hitting a target.")]
		[SerializeField] private float hitSlowDuration = 0.12f;

		[Header("Windup Telegraph")]
		[Tooltip("Optional visual object enabled only during windup. Use this for a glow mesh, particle effect, or warning light.")]
		[SerializeField] private GameObject windupGlowVisual;
		[Tooltip("Optional renderers to tint during windup. Assign sword/body renderers that should glow before launch.")]
		[SerializeField] private Renderer[] windupGlowRenderers;
		[Tooltip("Shader color property used for windup glow.")]
		[SerializeField] private string windupGlowColorProperty = "_EmissionColor";
		[Tooltip("Color applied to the assigned windup glow renderers during windup.")]
		[SerializeField] private Color windupGlowColor = Color.cyan;
		[Tooltip("Multiplier applied to windupGlowColor to create a stronger emission-style warning.")]
		[SerializeField] private float windupGlowIntensity = 2.0f;

		[Header("Animation")]
		[Tooltip("Animator trigger name used to start the lunge animation.")]
		[SerializeField] private string lungeTriggerName = "Lunge";

		[Header("SFX")]
		[Tooltip("Played when the lunge dash actually begins.")]
		[SerializeField] private SFXDefinition lungeDashSFX;
		[Tooltip("Optional point where the lunge sound should play from. If empty, the enemy root is used.")]
		[SerializeField] private Transform lungeSFXPoint;
		[Tooltip("If true, the lunge SFX plays at the exact dash-start moment.")]
		[SerializeField] private bool playLungeSFXOnDashStart = true;

		// How many target-position samples are cached for lagged lunge aiming.
		private const int TargetHistoryCapacity = 16;

		// Sword lunge attack cooldown/state timers

		// Cooldown timer for when the next lunge is allowed to start
		private float nextLungeTime = -Mathf.Infinity;
		private Coroutine lungeRoutine;
		private int lungeTriggerHash;

		// Current lunge phase
		public SwordLungePhase CurrentPhase { get; private set; } = SwordLungePhase.None;

		// Sword Enemy Lunge Attack Specific Properties

		// True while the lunge attack is currently in progress
		public bool IsLunging => CurrentPhase != SwordLungePhase.None;
		// True only during the active dash movement
		// Usef for animation + VFX + dash-specific checks
		public bool IsDashPhase => CurrentPhase == SwordLungePhase.Dash;
		// True when normal damage should still apply but must not cancel the committed lunge or trigger hit stun
		public bool BlocksDamageInterrupts => ShouldBlockDamageInterrupts();
		// Minimum valid distance for the lunge attack
		public float LungeMinRange => lungeMinRange;
		// Maximum valid distance for the lunge attack
		public float LungeMaxRange => lungeMaxRange;

		// Cached locked aim and dash data
		// These are built once per lunge so the dash does not track the player mid-attack
		private Vector3 lockedLungeAimPoint;
		private Vector3 lungeDirection = Vector3.forward;
		private Vector3 dashStartPosition;
		private Vector3 dashEndPosition;
		private float dashStartTime = -Mathf.Infinity;
		private float currentDashSpeed;
		private float hitSlowEndTime = -Mathf.Infinity;
		private bool hasAppliedHitReaction;
		private bool hasExternalPressureAimPoint;
		private Vector3 externalPressureAimPoint;

		// Runtime owner reference used by hit callbacks and animation-event failsafes
		private SwordEnemy activeOwner;

		// Small circular target-position history buffer used to aim at the player's previous position.
		private readonly TargetHistorySample[] targetHistory = new TargetHistorySample[TargetHistoryCapacity];
		private int targetHistoryCount;
		private int targetHistoryNextIndex;
		private float lastTargetHistorySampleTime = -Mathf.Infinity;
		private Transform trackedTarget;

		// MaterialPropertyBlock lets us tint assigned renderers without instantiating materials at runtime
		private MaterialPropertyBlock glowPropertyBlock;
		private int windupGlowColorPropertyHash;

		private struct TargetHistorySample {
			public float Time;
			public Vector3 Position;
		}

		// Caches runtime references + prepares the lunge hitbox + and ensures the lunge rigidbody starts in a kinematic state
		private void Awake() {
			lungeTriggerHash = Animator.StringToHash(lungeTriggerName); // Trigger
			windupGlowColorPropertyHash = Shader.PropertyToID(windupGlowColorProperty);
			glowPropertyBlock = new MaterialPropertyBlock();

			if (lungeRB == null) {
				lungeRB = GetComponent<Rigidbody>();
			}

			if (lungeRB != null) {
				lungeRB.isKinematic = true;
			}

			if (lungeHitbox != null) {
				lungeHitbox.Initialise(lungeAttackData);
				lungeHitbox.Disable();
				lungeHitbox.OnMeleeHit += HandleLungeHit;
			}

			SetWindupGlowVisible(false);
		}

		// Unsubscribes from hit callbacks to avoid redundant event references if this component is destroyed
		private void OnDestroy() {
			if (lungeHitbox != null) {
				lungeHitbox.OnMeleeHit -= HandleLungeHit;
			}
		}

		// Clears lunge cooldown and state so the module is ready for pooling or respawn
		public void ResetRuntime(SwordEnemy owner) {
			nextLungeTime = -Mathf.Infinity;
			ClearTargetHistory();
			CancelLunge(owner, true);

			if (lungeRB != null) {
				lungeRB.isKinematic = true;
			}

			DisableLungeHitbox();
			SetWindupGlowVisible(false);
		}

		// Returns true if the supplied target distance is inside the valid lunge range band
		public bool InRange(float distanceToTarget) {
			return distanceToTarget >= lungeMinRange && distanceToTarget <= lungeMaxRange;
		}

		// Returns true if the owner is allowed to begin a lunge right now
		public bool CanLunge(SwordEnemy owner) {
			return owner != null
				&& owner.IsDead == false
				&& owner.IsStunned == false
				&& owner.IsAttacking == false
				&& Time.time >= nextLungeTime
				&& IsLunging == false
				&& lungeChance > 0.0f;
		}

		public bool TryStartLungeAt(SwordEnemy owner, Animator animator, Vector3 predictedAimPoint) {
			// Cache the pressure aim before starting the existing lunge pipeline
			// TryStartLunge() will call LockLungeAimPoint(), where this point is consumed
			hasExternalPressureAimPoint = true;
			externalPressureAimPoint = predictedAimPoint;

			bool started = TryStartLunge(owner, animator);

			// If the original lunge rejected the start because of cooldown/state/range/etc,
			// clear the pending external aim so it cannot leak into a later normal lunge
			if (started == false) {
				hasExternalPressureAimPoint = false;
			}

			return started;
		}

		// Attempts to start the lunge attack by locking the aim point + deriving attack lock time + triggering the animation
		public bool TryStartLunge(SwordEnemy owner, Animator animator) {
			if (owner == null || owner.HasTarget == false) {
				return false;
			}

			if (CanLunge(owner) == false || InRange(owner.DistanceToTarget) == false) {
				return false;
			}

			// Roll lunge chance only when the enemy actually attempts to start a lunge
			if (ShouldCommitToLunge() == false) {
				// A short retry delay prevents the enemy from rolling again immediately next frame while still in range
				nextLungeTime = Time.time + failedLungeChanceRetryDelay;
				return false;
			}

			activeOwner = owner;
			nextLungeTime = Time.time + lungeCooldown;

			// Derive the attack lock from the configured phase timings so it cannot move away from the actual lunge duration
			owner.BeginAttackLock(GetDerivedAttackLockTime());

			SetupLungeData(owner);
			LockLungeAimPoint(owner);

			if (animator != null) {
				animator.SetTrigger(lungeTriggerHash);
			}

			if (lungeRoutine != null) {
				StopCoroutine(lungeRoutine);
			}

			// Begin lunge attack Coroutine (PhysX method)
			lungeRoutine = StartCoroutine(LungeRoutine(owner));

			return true;
		}


		// Records target history every frame and handles phase-specific facing
		// The enemy stops tracking the player during Dash
		public void TickLunge(SwordEnemy owner) {
			if (owner == null) {
				return;
			}

			if (owner.HasTarget && owner.Target != null) {
				RecordTargetSample(owner.Target);
			}

			if (CurrentPhase == SwordLungePhase.Windup) {
				owner.StopMove();
				owner.FaceTarget(lockedLungeAimPoint);
			}
		}

		// Cancels the current lunge + optionally forcing the shared attack lock to clear immediately
		public void CancelLunge(SwordEnemy owner, bool forceClearAttackLock = false) {
			if (owner == null) {
				owner = GetComponent<SwordEnemy>();
				if (owner == null) {
					Debug.LogError("SwordLungeAttack.CancelLunge: SwordEnemy owner is null.");
					return;
				}
			}

			// Stop current lunge routine
			if (lungeRoutine != null) {
				StopCoroutine(lungeRoutine);
				lungeRoutine = null;
			}

			SetPhase(SwordLungePhase.None);
			activeOwner = null;
			dashStartTime = -Mathf.Infinity;
			hitSlowEndTime = -Mathf.Infinity;
			hasAppliedHitReaction = false;

			DisableLungeHitbox();
			SetWindupGlowVisible(false);

			// Reset Physics so no velocity leaks into normal NavMesh movement after the lunge is cancelled
			if (lungeRB != null) {
				lungeRB.linearVelocity = Vector3.zero;
				lungeRB.angularVelocity = Vector3.zero;
				lungeRB.isKinematic = true;
			}

			// If lunge has disabled NavMesh movement, restore it here
			owner.EnableNavAgent();

			if (forceClearAttackLock && owner != null) {
				owner.EndAttackLock();
			}
		}

		// Reinitialises lunge attack data before the active lunge frames begin (used for animation event)
		public void SetupLungeData() {
			SetupLungeData(activeOwner);
		}

		// Reinitialises lunge attack data and makes sure attacker/faction are valid before the hitbox deals damage
		public void SetupLungeData(SwordEnemy owner) {
			lungeAttackData.Attacker = owner != null ? owner.gameObject : gameObject;
			lungeAttackData.AttackerFaction = owner != null ? owner.OwnerFaction : Faction.Enemy;
			lungeAttackData.Type = DamageType.Melee;

			if (lungeHitbox != null) {
				lungeHitbox.Initialise(lungeAttackData);
			}
		}

		// Enables the lunge hitbox during the active damage frames
		public void EnableLungeHitbox() {
			if (lungeHitbox != null) {
				lungeHitbox.Enable();
			}
		}

		// Disables the lunge hitbox after the active damage frames end
		public void DisableLungeHitbox() {
			if (lungeHitbox != null) {
				lungeHitbox.Disable();
			}
		}

		// Animation event entry point
		// Starts the dash using the aim point locked at windup start
		public void StartLungeDashFromOwner(SwordEnemy owner) {
			BeginDash(owner != null ? owner : activeOwner);
		}

		// Animation event entry point
		// Ends the dash cleanly and enters recovery
		public void EndLungeDashFromOwner(SwordEnemy owner) {
			EndDash(owner != null ? owner : activeOwner);
		}

		// Returns true when this attempt should actually commit to the lunge animation
		private bool ShouldCommitToLunge() {
			if (lungeChance >= 1.0f) {
				return true;
			}

			if (lungeChance <= 0.0f) {
				return false;
			}

			return Random.value <= lungeChance;
		}

		// Locks a fair lunge aim point once at the start of windup
		// The dash never updates this to the player's live position
		private void LockLungeAimPoint(SwordEnemy owner) {
			if (owner == null || owner.Target == null) {
				lockedLungeAimPoint = transform.position + transform.forward;
				lungeDirection = transform.forward;
				return;
			}

			Vector3 aimPoint;
			if (hasExternalPressureAimPoint) {
				// Use the pressure prediction once, then clear it
				// BuildAimPoint() should still apply the existing height/grounding logic
				aimPoint = BuildAimPoint(externalPressureAimPoint);
				hasExternalPressureAimPoint = false;
			}
			else {
				// Fall back to the original lunge aim behaviour for non-pressure lunges
				aimPoint = usePreviousTargetPosition ? GetHistoricalAimPoint(owner.Target) : BuildAimPoint(owner.Target.position);
			}

			// Undershoot the locked target point so the sword enemy threatens the player without landing perfectly on them
			Vector3 flatToAim = aimPoint - transform.position;
			flatToAim.y = 0.0f;

			if (flatToAim.sqrMagnitude > 0.0001f) {
				Vector3 flatDirection = flatToAim.normalized;
				float distance = flatToAim.magnitude;
				float undershoot = Mathf.Min(lungeUndershootDistance, Mathf.Max(0.0f, distance - 0.1f));
				aimPoint -= flatDirection * undershoot;
			}

			lockedLungeAimPoint = aimPoint;
			lungeDirection = BuildLaunchDirectionFromLockedAim();
		}

		// Starts the dash phase
		// If the animation event is missing, LungeRoutine calls this automatically after windup
		private void BeginDash(SwordEnemy owner) {
			if (CurrentPhase != SwordLungePhase.Windup || owner == null || lungeRB == null) {
				return;
			}

			lungeDirection = BuildLaunchDirectionFromLockedAim();
			if (lungeDirection.sqrMagnitude < 0.0001f) {
				lungeDirection = transform.forward;
			}

			// Snap to the committed dash direction once, then stop rotating toward the player for the rest of the dash
			transform.rotation = Quaternion.LookRotation(lungeDirection, Vector3.up);

			dashStartPosition = lungeRB.position;
			float distanceToLockedAim = Vector3.Distance(Flatten(dashStartPosition), Flatten(lockedLungeAimPoint));
			float clampedDashDistance = Mathf.Min(lungeDashDistance, distanceToLockedAim);
			dashEndPosition = dashStartPosition + lungeDirection * clampedDashDistance;
			dashEndPosition.y = dashStartPosition.y;

			currentDashSpeed = lungeDashSpeed > 0.0f ? lungeDashSpeed : lungeDashDistance / Mathf.Max(0.01f, lungeDuration);

			dashStartTime = Time.time;
			hitSlowEndTime = -Mathf.Infinity;
			hasAppliedHitReaction = false;

			SetWindupGlowVisible(false);
			SetPhase(SwordLungePhase.Dash);

			if (playLungeSFXOnDashStart) {
				PlayLungeDashSFX(owner);
			}

			owner.DisableNavAgent();

			lungeRB.isKinematic = false;
			lungeRB.linearVelocity = Vector3.zero;
			lungeRB.angularVelocity = Vector3.zero;
		}

		// Ends dash movement and moves into recovery
		// This is safe to call from animation events, hit reactions or failsafes
		private void EndDash(SwordEnemy owner) {
			if (CurrentPhase != SwordLungePhase.Dash && CurrentPhase != SwordLungePhase.Windup) {
				return;
			}

			if (lungeRB != null) {
				lungeRB.linearVelocity = Vector3.zero;
				lungeRB.angularVelocity = Vector3.zero;
				lungeRB.isKinematic = true;
			}

			DisableLungeHitbox();
			SetWindupGlowVisible(false);
			SetPhase(SwordLungePhase.Recovery);

			if (owner != null && owner.IsDead == false) {
				owner.EnableNavAgent();
				owner.StopMove();
			}
		}

		// Main phase controller
		// Animation events can still control exact timing, but this prevents the lunge from getting stuck
		private IEnumerator LungeRoutine(SwordEnemy owner) {
			SetPhase(SwordLungePhase.Windup);
			SetWindupGlowVisible(true);

			float windupEndTime = Time.time + lungeWindupTime;
			while (CurrentPhase == SwordLungePhase.Windup && Time.time < windupEndTime) {
				if (owner == null || owner.IsDead || owner.HasTarget == false) {
					CancelLunge(owner, true);
					yield break;
				}

				owner.StopMove();
				owner.FaceTarget(lockedLungeAimPoint);
				yield return null;
			}

			// Failsafe: if the dash-start animation event did not fire, start the dash from code after windup
			if (CurrentPhase == SwordLungePhase.Windup) {
				BeginDash(owner);
			}

			while (CurrentPhase == SwordLungePhase.Dash) {
				if (owner == null || owner.IsDead) {
					CancelLunge(owner, true);
					yield break;
				}

				MoveDashStep(owner);

				// Failsafe: if the dash-end animation event did not fire, force the dash to end after the configured duration
				if (Time.time >= dashStartTime + lungeDuration + lungeEventFailsafeBuffer) {
					EndDash(owner);
				}

				yield return new WaitForFixedUpdate();
			}

			if (CurrentPhase == SwordLungePhase.Recovery) {
				float recoveryEndTime = Time.time + lungeRecoveryTime;
				while (Time.time < recoveryEndTime) {
					if (owner == null || owner.IsDead) {
						CancelLunge(owner, true);
						yield break;
					}

					owner.StopMove();
					yield return null;
				}
			}

			FinishLunge(owner);
		}

		// Moves the Rigidbody toward the locked endpoint and clamps travel so the dash cannot overshoot
		private void MoveDashStep(SwordEnemy owner) {
			if (lungeRB == null || CurrentPhase != SwordLungePhase.Dash) {
				return;
			}

			float speed = currentDashSpeed;
			if (Time.time < hitSlowEndTime) {
				speed *= hitSlowSpeedMultiplier;
			}

			Vector3 currentPosition = lungeRB.position;
			Vector3 nextPosition = Vector3.MoveTowards(currentPosition, dashEndPosition, speed * Time.fixedDeltaTime);
			nextPosition.y = dashStartPosition.y;

			lungeRB.MovePosition(nextPosition);
			lungeRB.linearVelocity = Vector3.zero;

			if ((nextPosition - dashEndPosition).sqrMagnitude <= dashEndpointTolerance * dashEndpointTolerance) {
				EndDash(owner);
			}
		}

		// Finishes the lunge attack and releases the shared attack lock
		private void FinishLunge(SwordEnemy owner) {
			SetPhase(SwordLungePhase.None);
			lungeRoutine = null;
			activeOwner = null;
			dashStartTime = -Mathf.Infinity;
			hitSlowEndTime = -Mathf.Infinity;
			hasAppliedHitReaction = false;
			DisableLungeHitbox();
			SetWindupGlowVisible(false);

			if (owner != null && owner.IsDead == false) {
				owner.EndAttackLock();
			}
		}

		// Called by MeleeHitbox when the lunge successfully damages a target
		private void HandleLungeHit(GameObject attacker, Vector3 hitPoint, GameObject hitObject) {
			if (CurrentPhase != SwordLungePhase.Dash || hasAppliedHitReaction) {
				return;
			}

			hasAppliedHitReaction = true;

			if (stopLungeOnHit) {
				EndDash(activeOwner);
				return;
			}

			// If we do not stop completely, slow the remaining dash briefly to reduce sliding through the player
			hitSlowEndTime = Time.time + hitSlowDuration;
		}

		// Calculates launch direction from the locked aim point, applying the launch-angle fairness clamp
		private Vector3 BuildLaunchDirectionFromLockedAim() {
			Vector3 flatDirection = lockedLungeAimPoint - transform.position;
			flatDirection.y = 0.0f;

			if (flatDirection.sqrMagnitude < 0.0001f) {
				flatDirection = transform.forward;
			}

			Vector3 normalizedDirection = flatDirection.normalized;
			float angleFromFacing = Vector3.Angle(transform.forward, normalizedDirection);
			if (angleFromFacing > lungeMaxLaunchAngle) {
				normalizedDirection = Flatten(transform.forward).normalized;
			}

			return normalizedDirection;
		}

		// Builds a target aim point using a vertical offset
		private Vector3 BuildAimPoint(Vector3 targetPosition) {
			return targetPosition + Vector3.up * lungeAimHeight;
		}

		// Returns a target aim point from the position history, falling back to the current target position when no history exists yet
		private Vector3 GetHistoricalAimPoint(Transform target) {
			Vector3 fallback = BuildAimPoint(target.position);

			if (usePreviousTargetPosition == false || lungeAimLagTime <= 0.0f || targetHistoryCount <= 0) {
				return fallback;
			}

			float desiredSampleTime = Time.time - lungeAimLagTime;
			TargetHistorySample bestSample = default(TargetHistorySample);
			bool foundSampleAtOrBeforeDesiredTime = false;

			for (int i = 0; i < targetHistoryCount; i++) {
				TargetHistorySample sample = targetHistory[i];
				if (sample.Time <= desiredSampleTime && (foundSampleAtOrBeforeDesiredTime == false || sample.Time > bestSample.Time)) {
					bestSample = sample;
					foundSampleAtOrBeforeDesiredTime = true;
				}
			}

			if (foundSampleAtOrBeforeDesiredTime) {
				return BuildAimPoint(bestSample.Position);
			}

			// If all samples are newer than the desired time, use the oldest sample available
			TargetHistorySample oldestSample = targetHistory[0];
			for (int i = 1; i < targetHistoryCount; i++) {
				if (targetHistory[i].Time < oldestSample.Time) {
					oldestSample = targetHistory[i];
				}
			}

			return BuildAimPoint(oldestSample.Position);
		}

		// Stores the target's current position so future lunges can aim at a previous position for fairer dodging
		private void RecordTargetSample(Transform target) {
			if (target == null) {
				return;
			}

			if (trackedTarget != target) {
				ClearTargetHistory();
				trackedTarget = target;
			}

			if (Time.time < lastTargetHistorySampleTime + targetHistorySampleInterval) {
				return;
			}

			targetHistory[targetHistoryNextIndex] = new TargetHistorySample {
				Time = Time.time,
				Position = target.position
			};

			targetHistoryNextIndex = (targetHistoryNextIndex + 1) % TargetHistoryCapacity;
			targetHistoryCount = Mathf.Min(targetHistoryCount + 1, TargetHistoryCapacity);
			lastTargetHistorySampleTime = Time.time;
		}

		// Clears target history when the enemy respawns or changes target
		private void ClearTargetHistory() {
			targetHistoryCount = 0;
			targetHistoryNextIndex = 0;
			lastTargetHistorySampleTime = -Mathf.Infinity;
			trackedTarget = null;
		}

		// Shows or hides the optional windup glow telegraph
		private void SetWindupGlowVisible(bool visible) {
			if (windupGlowVisual != null) {
				windupGlowVisual.SetActive(visible);
			}

			if (windupGlowRenderers == null || windupGlowRenderers.Length == 0) {
				return;
			}

			Color glowColor = windupGlowColor * windupGlowIntensity;
			for (int i = 0; i < windupGlowRenderers.Length; i++) {
				Renderer glowRenderer = windupGlowRenderers[i];
				if (glowRenderer == null) {
					continue;
				}

				glowRenderer.GetPropertyBlock(glowPropertyBlock);
				glowPropertyBlock.SetColor(windupGlowColorPropertyHash, visible ? glowColor : Color.black);
				glowRenderer.SetPropertyBlock(glowPropertyBlock);
			}
		}

		// Returns whether incoming damage should be allowed to interrupt this lunge phase
		// Fixes the bug where the players bullets could intefere with the sword enemy lunge attack
		private bool ShouldBlockDamageInterrupts() {
			switch (CurrentPhase) {
				case SwordLungePhase.Windup:
				case SwordLungePhase.Dash:
					return true;

				case SwordLungePhase.Recovery:
					return true;

				default:
					return false;
			}
		}

		// Updates the current lunge phase in one place so future debugging or VFX hooks can be added safely
		private void SetPhase(SwordLungePhase newPhase) {
			CurrentPhase = newPhase;
		}

		// Attack lock is derived from the actual phase timings so inspector values cannot accidentally fall out of sync
		private float GetDerivedAttackLockTime() {
			return lungeWindupTime + lungeDuration + lungeRecoveryTime + lungeAttackLockBuffer;
		}

		// Returns the supplied vector projected onto the XZ plane
		private Vector3 Flatten(Vector3 value) {
			value.y = 0.0f;
			return value;
		}

		// Plays the lunge dash SFX from the assigned point if available, otherwise from the enemy root
		private void PlayLungeDashSFX(SwordEnemy owner) {
			if (lungeDashSFX == null) {
				return;
			}

			Transform sfxTarget = lungeSFXPoint;

			if (sfxTarget == null && owner != null) {
				sfxTarget = owner.transform;
			}

			if (sfxTarget == null) {
				sfxTarget = transform;
			}

			SFXManager.PlayAttached(lungeDashSFX, sfxTarget);
		}

		// Draws the locked aim point and clamped dash endpoint for quick tuning in the Scene view
		private void OnDrawGizmosSelected() {
			if (CurrentPhase == SwordLungePhase.None) {
				return;
			}

			Gizmos.DrawWireSphere(lockedLungeAimPoint, 0.2f);
			Gizmos.DrawLine(transform.position, lockedLungeAimPoint);

			if (CurrentPhase == SwordLungePhase.Dash || CurrentPhase == SwordLungePhase.Recovery) {
				Gizmos.DrawWireSphere(dashEndPosition, 0.2f);
				Gizmos.DrawLine(dashStartPosition, dashEndPosition);
			}
		}

		private void OnValidate() {
			lungeMinRange = Mathf.Max(0.0f, lungeMinRange);
			lungeMaxRange = Mathf.Max(lungeMinRange, lungeMaxRange);
			lungeCooldown = Mathf.Max(0.0f, lungeCooldown);
			failedLungeChanceRetryDelay = Mathf.Max(0.0f, failedLungeChanceRetryDelay);
			lungeChance = Mathf.Clamp01(lungeChance);
			lungeWindupTime = Mathf.Max(0.0f, lungeWindupTime);
			lungeDuration = Mathf.Max(0.01f, lungeDuration);
			lungeRecoveryTime = Mathf.Max(0.0f, lungeRecoveryTime);
			lungeAttackLockBuffer = Mathf.Max(0.0f, lungeAttackLockBuffer);
			lungeEventFailsafeBuffer = Mathf.Max(0.0f, lungeEventFailsafeBuffer);
			lungeAimLagTime = Mathf.Max(0.0f, lungeAimLagTime);
			lungeUndershootDistance = Mathf.Max(0.0f, lungeUndershootDistance);
			lungeAimHeight = Mathf.Max(0.0f, lungeAimHeight);
			targetHistorySampleInterval = Mathf.Max(0.0f, targetHistorySampleInterval);
			lungeDashDistance = Mathf.Max(0.0f, lungeDashDistance);
			lungeDashSpeed = Mathf.Max(0.0f, lungeDashSpeed);
			lungeMaxLaunchAngle = Mathf.Clamp(lungeMaxLaunchAngle, 0.0f, 180.0f);
			dashEndpointTolerance = Mathf.Max(0.001f, dashEndpointTolerance);
			hitSlowSpeedMultiplier = Mathf.Clamp01(hitSlowSpeedMultiplier);
			hitSlowDuration = Mathf.Max(0.0f, hitSlowDuration);
			windupGlowIntensity = Mathf.Max(0.0f, windupGlowIntensity);
		}

		// - DEPRECATED -

		//// Begins the dash phase of the lunge using the prepared lunge direction
		//// Calculate dash direction at exact launch moment + ensure enemy is facing within a given angle
		//public void OnLungeDashStart(SwordEnemy owner) {
		//	// Stop lunge
		//	if (owner == null || owner.Target == null || lungeRB == null) {
		//		return;
		//	}

		//	// Compute the lunge dash direction (this is locked at launch to prevent aimbot-like tracking)
		//	Vector3 predictedPosition = PredictAimPoint(owner.Target);
		//	Vector3 flatDirection = predictedPosition - transform.position;
		//	flatDirection.y = 0.0f;

		//	if (flatDirection.sqrMagnitude < 0.0001f) {
		//		flatDirection = transform.forward;
		//	}

		//	//// Check if sword enemy is facing enough within a given angle
		//	//// If not, snap rotation to the dash direction before launch
		//	//if (IsFacingDirection(owner.Target, lungeDirection) == false) {
		//	//	Quaternion desiredRotation = Quaternion.LookRotation(lungeDirection);
		//	//	transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
		//	//}

		//	float angle = Vector3.Angle(transform.forward, flatDirection.normalized);
		//	if (angle > lungeMaxLaunchAngle) {
		//		flatDirection = transform.forward;
		//	}

		//	lungeDirection = flatDirection.normalized;

		//	owner.DisableNavAgent();

		//	//// Disable NavMesh agent (for physX lunge movement)
		//	//if (navMeshAgent != null) {
		//	//	navMeshAgent.enabled = false;
		//	//}

		//	lungeRB.isKinematic = false;
		//	lungeRB.linearVelocity = Vector3.zero;
		//	//lungeRB.angularVelocity = Vector3.zero;

		//	// Dash in the same direction used for enemy facing 
		//	lungeRB.AddForce(lungeDirection * lungeForce, ForceMode.VelocityChange);
		//}

		//private void SetLayerRecursively(GameObject obj, int layer) {
		//	obj.layer = layer;
		//	foreach (Transform child in obj.transform)
		//		SetLayerRecursively(child.gameObject, layer);
		//}

		//// Predict the player position using player velocity alongside a time lead (predicat ahead of time)
		//private Vector3 PredictAimPoint(Transform target) {
		//	// Aim at players chest (0.8 - 1.2 for the multiplier depending on player height)
		//	Vector3 baseAim = target.position + target.forward /*Vector3.up * 1.0f*/;

		//	if (target.TryGetComponent(out Rigidbody rb)) {
		//		return baseAim + rb.linearVelocity * lungeLeadTime;
		//	}

		//	// TODO: Expose velocity & use that instead
		//	return baseAim;
		//}

		//// Ensure that enemy is facing their targets direction within a given angle (prevents side-ways lunging)
		//private bool IsFacingDirection(Transform target, Vector3 worldDirection) {
		//	if (target == null) {
		//		return false;
		//	}

		//	worldDirection.y = 0.0f;
		//	if (worldDirection.sqrMagnitude < 0.001f) {
		//		return true;
		//	}

		//	float facingAngle = Vector3.Angle(transform.forward, worldDirection);

		//	return facingAngle <= lungeMaxLaunchAngle;
		//}

		//// Ends the dash phase and transitions the lunge into recovery
		//public void OnLungeDashEnd(SwordEnemy owner) {
		//	if (owner == null) {
		//		owner = GetComponent<SwordEnemy>();
		//		if (owner == null) {
		//			Debug.LogError("SwordLungeAttack.OnLungeDashEnd: SwordEnemy owner is null.");
		//			return;
		//		}
		//	}

		//	if (lungeRB != null) {
		//		lungeRB.linearVelocity = Vector3.zero;
		//		//lungeRB.angularVelocity = Vector3.zero;
		//		lungeRB.isKinematic = true;
		//	}

		//	owner.EnableNavAgent();

		//	if (owner != null && gameObject.activeInHierarchy) {
		//		StartCoroutine(RecoveryRoutine(owner));
		//	}
		//}

		//private IEnumerator LungeWindupRoutine(SwordEnemy owner) {
		//	IsLunging = true;

		//	float endTime = Time.time + lungeWindupTime;
		//	while (Time.time < endTime) {
		//		if (owner == null || owner.IsDead || owner.HasTarget == false) {
		//			CancelLunge(owner, true);
		//			yield break;
		//		}

		//		owner.StopMove();
		//		owner.FaceTarget(owner.Target.position);
		//		yield return null;
		//	}

		//	float dashEndTime = Time.time + lungeDuration;
		//	while (Time.time < dashEndTime) {
		//		if (owner == null || owner.IsDead) {
		//			CancelLunge(owner, true);
		//			yield break;
		//		}

		//		yield return null;
		//	}
		//}
	}
}
