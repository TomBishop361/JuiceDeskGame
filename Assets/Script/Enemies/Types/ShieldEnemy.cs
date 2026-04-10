using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Game.AI; // IEnemyAgent namespace
using Game.AI.Behavior.Shield;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace Game.AI.Shield {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	[RequireComponent(typeof(Hurtbox))]
	public class ShieldEnemy : EnemyCombat, IEnemyAgent, IFactionOwner, IHealthSettings {
		// Implement IFactionOwner
		public Faction OwnerFaction => Faction.Enemy;
		// Implement IHealthSettings
		public float MaxHealth => maxHealth;
		public float LowHealthThreshold => 1.0f; // TODO: CHANGE THIS FROM MAGIC NUMBER

		[Header("References")]
		[SerializeField] private Transform target;
		[SerializeField] private NavMeshAgent navMeshAgent;
		[SerializeField] private HitScanHitbox hitScanHitbox; // Shared HitScan Hitbox (on root object)
		[SerializeField] private Animator animator;
		[SerializeField] private LOSSensor losSensor;
		[SerializeField] private Health healthComponent;

		[Header("Stats")]
		[SerializeField] private int maxHealth = 5;

		[Header("Combat")]
		[SerializeField] private float damage = 1.0f; // TODO: punch dmg + slam dmg
		[SerializeField] private AttackData minigunAttackData = new AttackData();
		[SerializeField] private AttackData shockwaveAttackData = new AttackData();

		[Header("Ranges")]
		[Tooltip("Minimum range that shield enemy will consider firing the minigun (should be less than 'minigunMaxRange')")]
		[SerializeField] private float fireMinRange = 3.0f; // in this range -> can fireminigunMaxRange
		//[Tooltip("Desired range that shield enemy will prioritise remaining within when attacking the player")]
		//[SerializeField] private float desiredRange = 15.0f; // aim to remain within this range
		[Tooltip("Minimum range that the shield enemy will consider performing a slam attack")]
		[SerializeField] private float slamRange = 4.0f;

		[Header("Cooldowns")]
		[SerializeField] private float slamCooldown = 3.0f;

		[Header("Movement")]
		[SerializeField] private bool toggleSmoothRotation = false; // toggle between reactive rotation & smooth rotation
		[SerializeField] private float rotationSpeed = 180.0f; // degrees per second (120 - 240 good range)
		[SerializeField] private float rotationSmoothing = 6.0f; // smoothing multiplier (6 - 8 good range)

		[Header("Stun")]
		[SerializeField] private float hitStunDuration = 0.4f;

		[Header("Minigun")]
		[SerializeField] private Transform minigunBarrel;
		[SerializeField] private Transform minigunMuzzle;
		[SerializeField] private LineRenderer minigunBulletTracer;
		[SerializeField] private float minigunBulletLifetime = 0.06f;
		[Tooltip("Fire rate of minigun (shots per second)")]
		[SerializeField] private float minigunFireRate = 5.0f; // shots per second
		[Tooltip("0m -> this value: 100% damage.")]
		[SerializeField] private float minigunFullDamageRange = 20.0f;
		[Tooltip("FullDamageRange -> this value: 75% damage.")]
		[SerializeField] private float minigunMediumDamageRange = 35.0f;
		[Tooltip("MediumDamageRange -> this value: 50% damage. Also the maximum firing range. (should be greater than 'fireMinRange')")]
		[SerializeField] private float minigunMaxRange = 45.0f;
		[SerializeField] private LayerMask minigunHitMask;

		[Header("Minigun Aim")]
		[Tooltip("Time delay before aiming at the player's previous position. Controls how much the minigun 'lags behind' the player.")]
		[SerializeField] private float minigunAimDelay = 0.18f;
		[Tooltip("Maximum random offset applied to the aim point. Controls how far shots deviate from the target.")]
		[SerializeField] private float minigunAimOffsetRadius = 0.6f;
		[Tooltip("Small random spread applied per shot. Adds micro variation to the minigun stream.")]
		[SerializeField] private float minigunInaccuracyDegrees = 2.0f;
		[Tooltip("Limits how quickly the aim can correct itself between shots. Prevents snapping directly to the player.")]
		[SerializeField] private float minigunMaxAimSnapPerShot = 1.2f;

		[Header("Minigun Heat Visuals")]
		[SerializeField] private Renderer minigunRenderer;
		[Tooltip("Base emission when not firing")]
		[SerializeField] private Color baseEmissionColor = Color.black;
		[Tooltip("Maximum emission color when fully overheated")]
		[SerializeField] private Color maxEmissionColor = new Color(1.5f, 0.4f, 0.1f); // bright orange

		[Header("Slam Shockwave")]
		[SerializeField] private float shockwaveRadius = 6.2f;
		[SerializeField] private LayerMask shockwaveHitMask; // TEMP SO I CAN SET FLOOR SO VISUAL SLAM IS SHOWN
		[SerializeField] private LayerMask shockwaveDamageMask;

		[Header("Shockwave Debug FX")]
		[SerializeField] private bool spawnShockwaveDebugRing = true;
		[SerializeField] private ShockwaveDebugRing shockwaveRingPrefab;
		[SerializeField] private float shockwaveRingExpandDuration = 0.6f;
		[SerializeField] private float shockwaveRingHoldDuration = 0.5f;
		[SerializeField] private float shockwaveRingYThickness = 0.05f;

		[Header("Exposure")]
		[Tooltip("How long the shield enemy is exposed after minigun overheating")]
		[SerializeField] private float minigunExposeDuration = 2.0f;
		[Tooltip("How long the shield enemy is exposed after performing the slam shockwave")]
		[SerializeField] private float slamExposeDuration = 1.25f;
		[Tooltip("How long the minigun can fire continuously before overheating")]
		[SerializeField] private float minigunOverheatDuration = 3.0f;


		[Header("Shield Block")]
		[SerializeField] private Collider shieldBlockCollider; // put on a child object in front of enemy
		[SerializeField] private float blockArcDegrees = 120.0f; // frontal cone for blocking

		[Header("Attack Lock Times (prevents spam)")]
		[SerializeField] private float slamLockTime = 1.0f; // anim length (approx)

		[Header("Grapple Window")]
		[SerializeField] private float grappleWindowDuration = 1.0f;

		[Header("LOS Memory")]
		[SerializeField] private float targetMemoryDuration = 1.0f;

		[SerializeField] private float postExposeRecoveryTime = 0.4f;

		private float postExposeEndTime = -Mathf.Infinity;

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; }
		//public bool HasTarget => target != null;
		public bool HasTarget { get; private set; }
		//public bool HasLOS { get; private set; }

		public float DistanceToTarget { get; private set; }
		public bool InAttackRange => HasTarget && InSlamRange; // shared 'InAttackRange' node for shield enemy can mean 'InSlamRange' (secondary attack)

		public bool CanAttack => CanFireMinigun; // shared 'CanAttack' node for shield enemy can mean 'CanFireMinigun' (default primary attack)
		public bool IsAttacking { get; private set; }
		//public bool IsTargetTooClose { get; private set; }

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [END] -

		// - Shield Enemy Specific Properties [START] -

		public bool InSlamRange => HasTarget && DistanceToTarget <= slamRange;
		public bool InFireRange => HasTarget && DistanceToTarget >= fireMinRange && DistanceToTarget <= minigunMaxRange;
		//public bool CanPunch => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextPunchTime;
		public bool CanSlam => !IsDead && !IsStunned && !IsExposed && Time.time >= postExposeEndTime && !IsAttacking && Time.time >= nextSlamTime;
		public bool CanFireMinigun => !IsDead && !IsStunned && !IsExposed && Time.time >= postExposeEndTime && !IsAttacking && HasTarget;
		//public bool HasMuzzleLOS => IsTargetInMuzzleLOS(); /*{ get; private set; }*/
		public bool GrappleWindowOpen { get; private set; }
		public bool PlayerInFront => IsPlayerInFront();
		public bool ShieldRaised { get; private set; }
		public bool HasLineOfSight { get; private set; }

		// - Shield Enemy Specific Properties [END] -

		// Properties
		// TODO: MAKE A LOT OF VARIABLES IN THIS AND OTHER SCRIPTS USE PROPERTIES LIKE THIS e.g ShieldEnemy can only change 'IsFiringMinigun'
		public bool IsFiringMinigun { get; private set; } // NOTE: Can use for Minigun firing animation + can another attack occur etc
		public bool IsExposed { get; private set; }

		// Exposed params (visible in the inspector)
		/*[SerializeField]*/
		private float grappleWindowEndTime;

		// Cooldown/State timers
		private float nextPunchTime = -Mathf.Infinity;
		private float nextSlamTime = -Mathf.Infinity;
		private float nextMinigunShotTime = -Mathf.Infinity;
		private float nextBlockReactTime = -Mathf.Infinity;
		private float stunEndTime = -Mathf.Infinity;
		private float attackEndTime = -Mathf.Infinity; // enforce min attack time (safety for anim not firing)
		private float lastSeenTime = -Mathf.Infinity;
		private float exposedEndTime = -Mathf.Infinity;
		private float minigunFireStartTime = -Mathf.Infinity;

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed"); // Float
		private static readonly int AnimPunch = Animator.StringToHash("Punch"); // Trigger
		private static readonly int AnimSlam = Animator.StringToHash("Slam"); // Trigger
		private static readonly int AnimFire = Animator.StringToHash("IsFiring"); // Bool
		private static readonly int AnimShieldRaised = Animator.StringToHash("ShieldRaised"); // Bool
		private static readonly int AnimBlockReact = Animator.StringToHash("BlockReact"); // Trigger
		private static readonly int AnimExposed = Animator.StringToHash("IsExposed"); // Bool
		private static readonly int AnimHit = Animator.StringToHash("Hit"); // Trigger
		private static readonly int AnimDie = Animator.StringToHash("Die"); // Trigger

		// Structs
		// Stores historical target positions with timestamps - so the minigun can aim at a delayed target position
		private struct AimSample {
			public Vector3 Position;
			public float Time;

			public AimSample(Vector3 position, float time) {
				Position = position;
				Time = time;
			}
		}
		// Queue of recent target positions (FIFO)
		private readonly Queue<AimSample> minigunAimHistory = new Queue<AimSample>();

		// Runtime params
		/*[SerializeField]*/
		private float currentHealth;
		/*[SerializeField]*/ private float previousHealthValue = 0.0f;
		/*[SerializeField]*/ private float lastDamageTime = -Mathf.Infinity; // TODO: use for invunerability window
		/*[SerializeField]*/ private bool shockwaveActive = false;
		private Vector3 lastMinigunAimPoint;
		private bool hasLastMinigunAimPoint = false;
		private Material minigunMaterial;
		[SerializeField] private string emissionColorProperty = "_EmissionColor";

		// Reset to default values
		private void Reset() {
			navMeshAgent = GetComponent<NavMeshAgent>();
			animator = GetComponentInChildren<Animator>();
		}

		private void Awake() {
			// Sync health
			currentHealth = maxHealth;

			if (navMeshAgent == null) {
				navMeshAgent = GetComponent<NavMeshAgent>();
			}

			if (animator == null) {
				animator = GetComponentInChildren<Animator>();
			}

			if (losSensor == null) {
				losSensor = GetComponent<LOSSensor>();
			}

			if (minigunRenderer != null) {
				minigunMaterial = minigunRenderer.material;
			}
		}

		private void Update() {
			if (IsDead == true) {
				return;
			}

			// LOS checker
			UpdateTargetAwareness();

			//// PROTOTYPE: Auto acquire target
			//if (target == null) {
			//	TryFindPlayer();
			//}

			// Fetch distance to target (if target is valid)
			if (HasTarget == true) {
				DistanceToTarget = Vector3.Distance(transform.position, target.position);
			}

			if (HasTarget && target != null) {
				RecordMinigunAimSample();
			} 
			else {
				ClearMinigunAimHistory();
			}

			// Handle stun timer
			if (IsStunned == true && Time.time >= stunEndTime) {
				IsStunned = false;
			}

			// Handle exposed timer
			if (IsExposed == true && Time.time >= exposedEndTime) {
				ExitExposedState();
			}

			// Handle grapple window timer
			// NOTE: SHIELD ENEMY WILL DECIDE WHETHER PLAYER CAN GRAPPLE
			// IDEA: GRAPPLING COULD BE LIKE DEALING DAMAGE -> THE SHIELD ENEMY'S HURTBOX CAN DECIDE THE OUTCOME
			if (GrappleWindowOpen == true && Time.time >= grappleWindowEndTime) {
				GrappleWindowOpen = false;
			}

			// Dynamically update shield blocking state
			//UpdateShieldBlockState();

			// Handle Attack lock timer (prevents immediate re-trigger spam even if anim event misfires)
			// NOTE: THIS ANIM EVENT IS A SAFETY NET IN CASE ANIM DOES NOT FIRE OR ISN'T WIRED CORRECTLY
			if (IsAttacking && Time.time >= attackEndTime) {
				IsAttacking = false;
			}

			UpdateMinigunEmission();

			// Animator movement speed
			if (navMeshAgent != null && animator != null) {
				animator.SetFloat(AnimMoveSpeed, navMeshAgent.velocity.magnitude);
			}
		}

		// Update drone enemy awareness state (uses LOS to determine this)
		private void UpdateTargetAwareness() {
			// PROTOTYPE: Auto acquire target
			if (target == null) {
				TryFindPlayer();
			}

			if (target == null) {
				HasLineOfSight = false;
				HasTarget = false;
				DistanceToTarget = Mathf.Infinity;
				return;
			}

			DistanceToTarget = Vector3.Distance(transform.position, target.position);

			if (losSensor != null && losSensor.HasLOS()) {
				HasLineOfSight = true;
				HasTarget = true;
				lastSeenTime = Time.time;
			}
			else {
				// fallback if sensor missing
				HasLineOfSight = false;
				HasTarget = (Time.time - lastSeenTime) <= targetMemoryDuration;
			}
		}

		// Updates the current target position into a time-based history buffer
		// This allows the minigun to aim at a delayed position instead of the live target
		private void RecordMinigunAimSample() {
			Vector3 samplePos = target.position;

			// Store position + timestamp
			minigunAimHistory.Enqueue(new AimSample(samplePos, Time.time));

			// Remove old samples beyond the history window (acts like a rolling buffer)
			// Ensures delayed lookup remains relevant
			while (minigunAimHistory.Count > 0 && Time.time - minigunAimHistory.Peek().Time > 0.75f) {
				minigunAimHistory.Dequeue();
			}
		}

		// Clears all stored aim history
		// Called when target is lost or firing stops to prevent redundant aim data
		private void ClearMinigunAimHistory() {
			minigunAimHistory.Clear();

			// Reset smoothing so next shot doesn't interpolate from an old position
			hasLastMinigunAimPoint = false;
		}

		private void LateUpdate() {
			if (minigunBarrel == null) {
				return;
			}

			if (IsFiringMinigun == false) {
				return;
			}

			float heatPercent = GetMinigunHeatPercent();
			float spinSpeed = Mathf.Lerp(1500f, 3000f, heatPercent);

			minigunBarrel.Rotate(0.0f, 0.0f, spinSpeed * Time.deltaTime, Space.Self);
		}

		// Enable blocking only if the shield is raised and the player is in front
		private void UpdateShieldBlockState() {
			if (shieldBlockCollider == null) {
				return;
			}

			bool shouldBlock = ShieldRaised && PlayerInFront;

			shieldBlockCollider.enabled = shouldBlock;
		}

		private void UpdateMinigunEmission() {
			if (minigunMaterial == null) {
				return;
			}

			float heatPercent = GetMinigunHeatPercent();

			// Smooth curve - quadratic ramp
			heatPercent = heatPercent * heatPercent;

			Color finalEmission = Color.Lerp(baseEmissionColor, maxEmissionColor, heatPercent);

			// Pulse near overheat
			if (heatPercent > 0.65f) {
				float pulse = Mathf.Sin(Time.time * 20f) * 0.2f + 1f;
				finalEmission *= pulse;
			}

			minigunMaterial.SetColor(emissionColorProperty, finalEmission);

			// Enable emission keyword
			minigunMaterial.EnableKeyword("_EMISSION");
		}

		private float GetMinigunHeatPercent() {
			if (IsFiringMinigun == false || minigunFireStartTime == -Mathf.Infinity) {
				return 0.0f;
			}

			float elapsed = Time.time - minigunFireStartTime;

			return Mathf.Clamp01(elapsed / minigunOverheatDuration);
		}

		// TODO: SUBHEADING FOR THESE -> THEY ARE CONDITIONS BUT DON'T JUST USE SIMPLE BOOLEAN VARIABLES - THEY USE FUNCTION LOGIC TO DETECT WHETHER TRUE/FALSE INSTEAD

		// Check if the player is in front of the shield enemy (used to initiate shield raised for blocking action)
		private bool IsPlayerInFront() {
			if (HasTarget == false) {
				return false;
			}

			Vector3 toTarget = target.position - transform.position;
			toTarget.y = 0.0f;

			if (toTarget.sqrMagnitude < 0.0001f) {
				return false;
			}

			float angle = Vector3.Angle(transform.forward, toTarget.normalized);

			// Half to get the front angle
			return angle <= blockArcDegrees * 0.5f;
		}

		//// Uses the muzzle to determine whether the shield enemy should fire the minigun based on a raycast
		//private bool IsTargetInMuzzleLOS() {
		//	if (HasTarget == false) {
		//		return false;
		//	}

		//	// Check if minigun muzzle has been assigned in inspector otherwise use the shield enemy as the ray origin
		//	Transform origin = minigunMuzzle != null ? minigunMuzzle : transform;
		//	// Get target chest height
		//	// TODO: Might need to change this as player height may differ in future (or just don't have this line))
		//	Vector3 targetPos = target.position + Vector3.up * 1.0f;

		//	Vector3 direction = targetPos - origin.position;
		//	float distance = direction.magnitude;

		//	// Target is very close so don't bother raycasting
		//	if (distance < 0.01f) {
		//		return true;
		//	}

		//	// Raycast to target from muzzle origin
		//	// If something blocks that ray then there is no LOS
		//	if (Physics.Raycast(origin.position, direction.normalized, out RaycastHit hit, distance, minigunHitMask, QueryTriggerInteraction.Ignore)) {
		//		// LOS only if the ray hits the player
		//		// TODO: Adjust tag/layer matrix for this to work properly - for now using exclude/include layers on the colliders
		//		return hit.collider.CompareTag("Player");
		//	}

		//	// Nothing hit - No muzzle LOS
		//	return false;
		//} 

		// PROTOTYPE: Find player automatically
		private void TryFindPlayer() {
			GameObject player = GameObject.FindGameObjectWithTag("Player");
			if (player != null) {
				target = player.transform;
			}
		}

		// - Implement IEnemyAgent Methods [START] -

		public void AcquireTarget() {
			TryFindPlayer();
		}

		public void Die() {
			if (IsDead == true) {
				return;
			}

			IsDead = true;
			IsStunned = false;
			IsAttacking = false;
			GrappleWindowOpen = false;
			ExitExposedState();

			// Disable nav mesh agent upon death
			if (navMeshAgent != null) {
				navMeshAgent.isStopped = true;
				navMeshAgent.enabled = false;
			}

			// Play death animation for shield enemy
			if (animator != null) {
				animator.SetBool(AnimExposed, false);
				animator.SetTrigger(AnimDie);
			}

			TrackDeath();

			// NOTE: This is temporary
			//Destroy(gameObject);
		}

		public void RecoverTick() {
			if (IsDead == true) {
				return;
			}

			StopMove();
		}

		// Shared 'Chase' action node uses this (so we need to implement it)
		// For shield enemy specific movement, we are using 'AdvanceRaised' action node
		public void ChaseTargetTick() {
			AdvanceRaisedTick();
		}

		public void StopMove() {
			if (navMeshAgent != null) {
				navMeshAgent.isStopped = true;
			}
		}
		// Shared action node 'PrimaryAttack' can be used
		// But for sword map primary attack to 'Swing' instead

		// Shared action node 'PrimaryAttack' can be used
		// TODO: Use this later and remove ShieldSlamShockwave
		public bool TryStartPrimaryAttack() {
			return TryStartSlamShockwave();
		}

		// - Implement IEnemyAgent Methods [END] -

		// - Shield Enemy Specifc BT Action Node Execution -

		public void AdvanceRaisedTick() {
			// Advanced raised interrupts
			if (IsDead == true || IsStunned == true || IsExposed == true || IsAttacking == true) {
				return;
			}
			// No target
			if (HasTarget == false || navMeshAgent == null) {
				return;
			}

			navMeshAgent.isStopped = false;
			navMeshAgent.SetDestination(target.position);

			// Continuously adjust direction when advanceing towards target
			FaceTarget(target.position);

			// Play shield raised animation whilst advancing
			SetShieldRaised(true);
		}

		//// NOTE: NOT BEING USED ATM - SHIELD ENEMY FIRES MINIGUN INSTEAD AS OF RIGHT NOW
		//// TODO: THESE GATES CAN BE SIMPLIFIED TO USE THE BOOLEANS AT THE TOP OF THE SCRIPTS - FOR ALL ENEMIES!
		//public bool TryStartPunch() {
		//	// Punch interrupts
		//	if (IsDead == true || IsStunned == true || IsAttacking == true) {
		//		return false;
		//	}
		//	// No target
		//	if (HasTarget == false) {
		//		return false;
		//	}
		//	//// Punch is out of range
		//	//if (InPunchRange == false) {
		//	//	return false;
		//	//}
		//	// Punch is still on cooldown
		//	if (Time.time < nextPunchTime) {
		//		return false;
		//	}

		//	StopMove();

		//	IsAttacking = true;
		//	nextPunchTime = Time.time + punchCooldown;
		//	attackEndTime = Time.time + punchLockTime;

		//	if (animator != null) {
		//		animator.SetBool(AnimShieldRaised, true);
		//		animator.SetTrigger(AnimPunch);
		//	}

		//	return true;
		//}

		// NOTE: NOT BEING USED ATM - SHIELD ENEMY USES SHIELD SLAM SHOCKWAVE - RATHER THAN THE GRAPPLE OPENER SLAM AS OF RIGHT NOW
		public bool TryStartSlam() {
			// Slam interrupts
			if (IsDead == true || IsStunned == true || IsAttacking == true) {
				return false;
			}
			// No target
			if (HasTarget == false) {
				return false;
			}
			// Slam is out of range
			if (InSlamRange == false) {
				return false;
			}
			// Slam is still on cooldown
			if (Time.time < nextSlamTime) {
				return false;
			}

			StopMove();

			IsAttacking = true;
			nextSlamTime = Time.time + slamCooldown;
			attackEndTime = Time.time + slamLockTime;

			// PROTOTYPE: Open grapple window immediately (or trigger it via anim event)
			GrappleWindowOpen = true;
			grappleWindowEndTime = Time.time + grappleWindowDuration;

			if (animator != null) {
				animator.SetBool(AnimShieldRaised, true);
				animator.SetTrigger(AnimSlam);
			}

			return true;
		}

		public bool TryStartSlamShockwave() {
			// Slam interrupts
			if (IsDead == true || IsStunned == true || IsAttacking == true) {
				return false;
			}
			// No target
			if (HasTarget == false) {
				return false;
			}
			// Slam shockwave is out of range
			if (InSlamRange == false) {
				return false;
			}
			// Slam shockwave is still on cooldown
			if (Time.time < nextSlamTime) {
				return false;
			}
				
			StopMove();

			IsAttacking = true;
			nextSlamTime = Time.time + slamCooldown;
			attackEndTime = Time.time + slamLockTime;

			// PROTOTYPE: Open grapple window immediately (or trigger it via anim event)
			GrappleWindowOpen = true;
			grappleWindowEndTime = Time.time + grappleWindowDuration;

			SetShieldRaised(true);

			if (animator != null) {
				animator.SetTrigger(AnimSlam);
			}

			return true;
		}

		public bool FireMinigunTick() {
			if (CanFireMinigun == false) {
				return false;
			}
			if (InFireRange == false) {
				return false;
			}
			if (HasLineOfSight == false) {
				return false;
			}
			//if (HasMuzzleLOS == false) {
			//	return false;
			//}

			BeginMinigunFiring();

			// Overheat check
			if (HasMinigunOverheated() == true) {
				EnterExposedState(minigunExposeDuration);
				return false;
			}

			// Advance & Hold shield whilst firing - NOTE: DO WE WANT THIS DESIGN?
			SetShieldRaised(true);

			IsFiringMinigun = true;
			FaceTarget(target.position);

			// Control fire rate
			// Increment time for next minigun shot
			float shotInterval = 1.0f / Mathf.Max(1.0f, minigunFireRate);
			// Still firing (waiting for the next shot)
			if (Time.time < nextMinigunShotTime) {
				return true;
			}

			nextMinigunShotTime = Time.time + shotInterval;
			DoHitscanShot();

			if (animator != null) {
				animator.SetBool(AnimFire, true);
			}

			return true;
		}

		private void BeginMinigunFiring() {
			if (IsFiringMinigun == false) {
				minigunFireStartTime = Time.time;
			}
		}

		private bool HasMinigunOverheated() {
			if (IsFiringMinigun == false) {
				return false;
			}

			return Time.time >= minigunFireStartTime + minigunOverheatDuration;
		}

		public void EnterExposedState(float duration) {
			if (IsDead == true) {
				return;
			}

			IsExposed = true;
			exposedEndTime = Time.time + duration;

			IsAttacking = false;
			StopMinigunFiring();
			StopMove();
			SetShieldRaised(false);

			if (animator != null) {
				animator.SetBool(AnimExposed, true);
			}
		}

		private void ExitExposedState() {
			IsExposed = false;
			postExposeEndTime = Time.time + postExposeRecoveryTime;

			if (animator != null) {
				animator.SetBool(AnimExposed, false);
			}
		}

		public void StopMinigunFiring() {
			IsFiringMinigun = false;
			hasLastMinigunAimPoint = false;
			minigunFireStartTime = -Mathf.Infinity;

			// Reset emission on stop / overheat
			if (minigunMaterial != null) {
				minigunMaterial.SetColor(emissionColorProperty, baseEmissionColor);
			}

			if (animator != null) {
				animator.SetBool(AnimFire, false);
			}
		}

		//// TODO: PUT LOGIC INSIDE A HitScan.cs script later (can be used by anyone that does hitscan (player, shield enemy etc)
		private void DoHitscanShot() {
			if (minigunMuzzle == null || target == null) {
				return;
			}

			Vector3 bulletOrigin = minigunMuzzle.position;

			// Resolve imperfect aim point instead of aiming directly at the player
			// This makes the minigun dodgeable and prevents guaranteed hits
			Vector3 imperfectAimPoint = GetImperfectMinigunAimPoint();

			Vector3 direction = imperfectAimPoint - bulletOrigin;
			if (direction.sqrMagnitude < 0.0001f) {
				direction = transform.forward;
			}
			direction.Normalize();

			// Calculate small random euler angle offset (this represents inaccuracy)
				// Create new quaternion direction for each bullet - allows deviation from center aim point
				direction = Quaternion.Euler(Random.Range(-minigunInaccuracyDegrees, minigunInaccuracyDegrees),
											 Random.Range(-minigunInaccuracyDegrees, minigunInaccuracyDegrees), 0.0f) * direction;

			Vector3 endPoint = bulletOrigin + direction * minigunMaxRange;

			//	// Cast ray towards bullet tracer direction - check for contact
			if (Physics.Raycast(bulletOrigin, direction, out RaycastHit hit, minigunMaxRange, minigunHitMask, QueryTriggerInteraction.Ignore)) {
				// Adjust minigun bullet tracer to match hit point
				endPoint = hit.point;

				// Damage falloff based on distance to hit point
				minigunAttackData.Damage = CalculateDamageFalloff(bulletOrigin);

				// Notify HitScanHitbox of a successful hit
				hitScanHitbox.ApplyHitScanHit(hit, minigunAttackData);
			}

			SpawnHitScanTracer(bulletOrigin, endPoint);

			// OPTIONAL: Debug hitscan ray
			Debug.DrawRay(bulletOrigin, direction * minigunMaxRange, Color.red, 0.05f);
		}

		// Calculates the final aim point used for the minigun shot
		// Combines delayed tracking, random offset, and smoothing
		private Vector3 GetImperfectMinigunAimPoint() {
			// Default fallback (if history is empty)
			Vector3 delayedAimPoint = target.position + Vector3.up;

			// Find the sample closest to (current time - delay)
			float targetTime = Time.time - minigunAimDelay;

			foreach (AimSample sample in minigunAimHistory) {
				delayedAimPoint = sample.Position;

				// Stop once we reach the desired delayed timestamp
				if (sample.Time >= targetTime) {
					break;
				}
			}

			// Apply random offset in a circular area
			// This creates a clustered spray instead of perfect tracking
			Vector2 randomCircle = Random.insideUnitCircle * minigunAimOffsetRadius;
			delayedAimPoint += new Vector3(randomCircle.x, randomCircle.y * 0.35f, randomCircle.y);

			// Smooth aim movement between shots to avoid snapping
			if (hasLastMinigunAimPoint == true) {
				delayedAimPoint = Vector3.MoveTowards(lastMinigunAimPoint, delayedAimPoint, minigunMaxAimSnapPerShot);
			}

			// Store for next frame smoothing
			lastMinigunAimPoint = delayedAimPoint;
			hasLastMinigunAimPoint = true;

			return delayedAimPoint;
		}

		// Distance-based damage falloff
		private float CalculateDamageFalloff(Vector3 origin) {
			float distanceToHit = Vector3.Distance(origin, target.position);
			float finalDamage = minigunAttackData.Damage;

			// Damage Falloff
			// 0–20m = 100% damage
			if (distanceToHit <= minigunFullDamageRange) {
				finalDamage = minigunAttackData.Damage;
				Debug.Log("Minigun Damage: FULL (0–20m)");
			}
			// 20–35m = 75% damage
			else if (distanceToHit <= minigunMediumDamageRange) {
				finalDamage = minigunAttackData.Damage * 0.75f;
				Debug.Log("Minigun Damage: MEDIUM (20–35m)");
			}
			// 35–45m = 50% damage
			else if (distanceToHit <= minigunMaxRange) {
				finalDamage = minigunAttackData.Damage * 0.50f;
				Debug.Log("Minigun Damage: LOW (35–45m)");
			}
			// 45m+ = 50% damage
			else {
				finalDamage = minigunAttackData.Damage * 0.50f;
				Debug.Log("APPLY LOW MINIGUN DAMAGE ABOVE MAX RANGE");
			}

			return finalDamage;
		}

		// Visual tracer for minigun bullets
		private void SpawnHitScanTracer(Vector3 start, Vector3 end) {
			LineRenderer minigunTracer = Instantiate(minigunBulletTracer);

			minigunTracer.positionCount = 2;

			// Set constant width for Minigun tracer
			minigunTracer.startWidth = 0.025f;
			minigunTracer.endWidth = 0.025f;

			minigunTracer.SetPosition(0, start);
			minigunTracer.SetPosition(1, end);

			Destroy(minigunTracer.gameObject, minigunBulletLifetime);
		}

		public void SetShieldRaised(bool isRaised) {
			ShieldRaised = isRaised;

			if (animator != null) {
				animator.SetBool(AnimShieldRaised, isRaised);
			}

			// Only enable bullet blocking when shield is raised
			if (shieldBlockCollider != null) {
				shieldBlockCollider.enabled = isRaised;
			}	
		}

		// - Damge / Stun -

		// TODO: Remove since we have Damage system now
		public void TakeDamage(int amount) {
			if (IsDead == true) {
				return;
			}

			// Decrement health by 'x' amount
			currentHealth -= amount;

			// Death on 0 health
			if (currentHealth <= 0) {
				Die();
				return;
			}

			Stun(hitStunDuration);

			if (animator != null) {
				animator.SetTrigger(AnimHit);
			}
		}

		// Prevent/Interrupt enemy from attacking once they take damage (for a 'short' time)
		private void Stun(float duration) {
			if (IsDead) return;

			IsStunned = true;
			IsAttacking = false;
			stunEndTime = Time.time + duration;

			StopMove();
		}
		
		// Handles Slam Shockwave ring expansion
		// Shockwave ring gradually grows and deals AOE damage to anything in its current radius
		private IEnumerator ShockwaveRoutine() {
			shockwaveActive = true;

			// Snap to ground for the center of the shockwave ring
			// Cast a ray downwards to find the ground
			Vector3 center = transform.position;
			if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5.0f)) {
				center = hit.point;
			}

			// Spawn Shockwave Debug ring to visualise the AOE on the ground
			if (spawnShockwaveDebugRing && shockwaveRingPrefab != null) {
				ShockwaveDebugRing shockwaveRing = Instantiate(shockwaveRingPrefab, center + Vector3.up * 0.02f, Quaternion.identity);
				shockwaveRing.Init(shockwaveRadius, shockwaveRingExpandDuration, shockwaveRingHoldDuration, shockwaveRingYThickness);
			}

			float elapsed = 0f;
			bool hasHitPlayer = false;

			while (elapsed < shockwaveRingExpandDuration) {
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / shockwaveRingExpandDuration);
				float currentRadius = Mathf.Lerp(0f, shockwaveRadius, t);

				// // Get colliders that overlap the Shockwave Slam AOE - only hit objects near the current radius (not the entire shockwave disc)
				Collider[] hits = Physics.OverlapSphere(center, currentRadius, shockwaveHitMask, QueryTriggerInteraction.Ignore);

				foreach (var h in hits) {
					// Only apply damage once
					if (hasHitPlayer == true) {
						break;
					}

					// Check distance band (using ring thickness)
					float distance = Vector3.Distance(center, h.ClosestPoint(center));
					if (Mathf.Abs(distance - currentRadius) <= shockwaveRingYThickness * 0.5f) {
						// Check if colliders in AOE contains a hurtbox (can it take damage)
						IDamageable damageableInterface = h.GetComponentInParent<IDamageable>();
						if (damageableInterface == null) {
							Debug.LogError("IDamageable: not found in parent of: " + h.gameObject.name);
							yield return null;
						}

						// Damage handling for Shockwave Slam AOE 
						damageableInterface.TakeDamage(shockwaveAttackData);

						hasHitPlayer = true;
					}
				}
					yield return null;
			}
			shockwaveActive = false;
		}

		// - Animation Events -

		// Call this at end of attack animation
		public void AnimEvent_AttackFinished() {
			IsAttacking = false;
			// PROTOTYPE: keep shield raised during combat
			// To lower it -> set 'ShieldRaised' FALSE somewhere
		}

		// Call this at the impact frame for the slam shockwave animation
		public void AnimEvent_DoShockwave() {
			if (IsDead == true) {
				return;
			}
			//if (shockwaveActive == true) {
			//	return;
			//}
			//StartCoroutine(ShockwaveRoutine());

			Vector3 center = transform.position;
			// Cast a ray downwards to find the ground
			if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5.0f, shockwaveHitMask)) {
				center = hit.point;
			}
			// Spawn Shockwave Debug ring to visualise the AOE on the ground
			if (spawnShockwaveDebugRing && shockwaveRingPrefab != null) {
				ShockwaveDebugRing shockwaveRing = Instantiate(shockwaveRingPrefab, center + Vector3.up * 0.02f, Quaternion.identity);
				shockwaveRing.Init(shockwaveRadius, shockwaveRingExpandDuration, shockwaveRingHoldDuration, shockwaveRingYThickness);
			}

			//// Snap to ground for the center of the shockwave ring
			//// Cast a ray downwards to find the ground
			//Vector3 center = transform.position;
			//if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5.0f)) {
			//	center = hit.point;
			//}

			//// Spawn Shockwave Debug ring to visualise the AOE on the ground
			//if (spawnShockwaveDebugRing && shockwaveRingPrefab != null) {
			//	ShockwaveDebugRing shockwaveRing = Instantiate(shockwaveRingPrefab, center + Vector3.up * 0.02f, Quaternion.identity);
			//	shockwaveRing.Init(shockwaveRadius, shockwaveRingExpandDuration, shockwaveRingHoldDuration, shockwaveRingYThickness);
			//}

			// Get colliders that overlap the Shockwave Slam AOE
			Collider[] hits = Physics.OverlapSphere(center, shockwaveRadius, shockwaveDamageMask, QueryTriggerInteraction.Ignore);

			foreach (var h in hits) {
				// Check if colliders in AOE contains a hurtbox (can it take damage)
				IDamageable damageableInterface = h.GetComponentInParent<IDamageable>();
				if (damageableInterface == null) {
					Debug.LogError("IDamageable: not found in parent of: " + h.gameObject.name);
					return;
				}

				//Debug.DrawLine(transform.position + Vector3.up * 0.2f, h.transform.position, Color.white, 0.2f);
				// Damage handling for Shockwave Slam AOE 
				damageableInterface.TakeDamage(shockwaveAttackData);

				// Knockback
				//Rigidbody rb = hit.attachedRigidbody;
				//if (rb != null) {
				//	Vector3 away = hit.transform.position - center;
				//	away.y = 0.2f;
				//	if (away.sqrMagnitude > 0.0001f) {
				//		rb.AddForce(away.normalized * shockwaveAttackData.Knockback.Force, ForceMode.VelocityChange);
				//	}
				//}
				//}
			}

			EnterExposedState(slamExposeDuration);
		}

		// Called in ShieldBlockReceiver.cs to trigger the block react animation when the player shoots the shield
		public void TriggerBlockReact() {
			if (animator == null) {
				return;
			}

			if (Time.time < nextBlockReactTime) {
				return;
			}

			// Small delay so shield block react doesn't spam
			nextBlockReactTime = Time.time + 0.15f; 
			animator.SetTrigger(AnimBlockReact);
		}

		// Hitbox toggles (call inside anim event)
		public void AnimEvent_EnableHitbox() {
			// TODO: ADD LOGIC TO AnimEvent_EnableHitbox()
		}
		public void AnimEvent_DisableHitbox() {
			// TODO: ADD LOGIC TO AnimEvent_DisableHitbox()
		}

		// - Movement Helpers -
		// TODO: Place inside MovementHelpers.cs script later

		// Turn to face the targets direction (used when chasing to maintain LOS)
		private void FaceTarget(Vector3 worldPos) {
			Vector3 direction = worldPos - transform.position;
			direction.y = 0.0f;

			// Safety - ensures that a direction exists before rotating towards it
			if (direction.sqrMagnitude < 0.0001f) {
				return;
			}

			Quaternion targetRotation = Quaternion.LookRotation(direction);

			if (toggleSmoothRotation == true) {
				// Smooth rotation towards target direction (using Slerp)
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothing);
			}
			else {
				// Responsive rotation towards target direction (using RotateTowards)
				// [BETTER FOR SHIELD since heavy, deliberate]
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}
		// - Event & Callback handlers -

		private void OnEnable() {
			if (healthComponent != null) {
				previousHealthValue = healthComponent.CurrentHealth;
				healthComponent.OnHealthChanged += HandleHealthChanged;
			}
		}
		private void OnDisable() {
			if (healthComponent != null) {
				healthComponent.OnHealthChanged -= HandleHealthChanged;
			}
		}

		private void HandleHealthChanged(float current, float max) {
			// Damage only if health has gone down
			if (current < previousHealthValue) {
				// Reset regen delay timer - only regen when out of combat
				lastDamageTime = Time.time;
			}
			previousHealthValue = current;
		}
	}
}

// - DEPRECATED -

// REASON: No longer needed as TryStartPunch() & TryStartSlam() are exposed to BT -> BT decides which attack not C#
// Shared action node 'PrimaryAttack' can be used
// But for shield we want 'Punch' Vs 'Slam' selection in the BT for shield enemy combat actions
//public bool TryStartPrimaryAttack() {
//	// PROTOTYPE fallback: if slam is ready and not too close to target -> slam, otherwise punch
//	if (CanSlam == true && InSlamRange == true && InPunchRange == false) {
//		return TryStartSlam();
//	}

//	// Punch if not in slam attack range
//	return TryStartPunch();
//}