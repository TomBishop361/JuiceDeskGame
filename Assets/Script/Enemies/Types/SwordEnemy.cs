using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Game.AI;
using UnityEngine.InputSystem; // IEnemyAgent namespace

namespace Game.AI.Sword {
	[DisallowMultipleComponent] // can only add this component once to a gameobject
	public class SwordEnemy : MonoBehaviour, IEnemyAgent, IFactionOwner, IHealthSettings {
		// Implement IFactionOwner
		public Faction OwnerFaction => Faction.Enemy;

		// Implement IHealthSettings
		public float MaxHealth => maxHealth;
		public float LowHealthThreshold => 1; // TODO: CHANGE THIS FROM MAGIC NUMBER

		[Header("References")]
		[SerializeField] private Transform target;
		[SerializeField] private NavMeshAgent navMeshAgent;
		[SerializeField] private MeleeHitbox meleeHitbox; // Shared Melee Hitbox (on sword object)
		[SerializeField] private MeleeHitbox swingHitbox; // TEMP
		[SerializeField] private MeleeHitbox lungeHitbox; // TEMP
		[SerializeField] private Rigidbody rb; // NOTE: ONLY NEED IF USING PHYSICS FOR LUNGE ATTACK
		[SerializeField] private Animator animator;
		[SerializeField] private LOSSensor losSensor;

		[Header("Stats")]
		[SerializeField] private int maxHealth = 3;
		[SerializeField] private float defaultSpeed = 7.0f;
		[SerializeField] private float defaultAcceleration = 16.0f;

		[Header("Combat")]
		[SerializeField] private float damage = 2.0f; // TODO: different attack damages (swing: 1 + lunge: 2)
		[SerializeField] private AttackData swingAttackData = new AttackData();
		[SerializeField] private AttackData lungeAttackData = new AttackData();

		[Header("Ranges")]
		[SerializeField] private float swingRange = 1.8f;
		[SerializeField] private float lungeMinRange = 2.8f; // start lunge attack if player is at least this far
		[SerializeField] private float lungeMaxRange = 6.5f; // don't lunge attack if player exceeds this range

		[Header("Cooldowns")]
		[SerializeField] private float swingCooldown = 1.0f;
		[SerializeField] private float lungeCooldown = 2.2f; // good ranges: 1.8-2.5

		[Header("Movement")]
		[SerializeField] private bool toggleSmoothRotation = false; // toggle between reactive rotation & smooth rotation
		[SerializeField] private float rotationSpeed = 440.0f; // degrees per second (360 - 480 good range for responsive melee speed)
		[SerializeField] private float rotationSmoothing = 12.0f; // smoothing multiplier (12 - 15 good range)

		[Header("Stun")]
		[SerializeField] private float hitStunDuration = 0.6f;

		[Header("Attack Lock Times (prevents spam)")]
		[SerializeField] private float swingLockTime = 0.55f; // anim length (approx)
		[SerializeField] private float lungeLockTime = 0.75f; // anim length (approx)

		//[Header("Lunge Settings")]
		//[SerializeField] private float lungeSpeed = 8.0f; // good ranges: 7-10 [NOTE: 20 for extreme difficulty)
		//[SerializeField] private float lungeAcceleration = 16.0f;
		//[SerializeField] private float lungeDistance = 1.5f; // distance to lunge forward (uses stopping distance below to ensure no overshooting)
		//[SerializeField] private float lungeStopDistance = 0.6f; // makes sure to not overshoot into the player | good ranges: 0.6-0.9
		//[SerializeField] private float lungeDuration = 0.3f; // good ranges: 0.18-0.30 (match active lunge part of anim clip)
		//[SerializeField] private LayerMask lungeBlockers; // use for walls and obstacles 

		[Header("Lunge Physics (Method)")]
		[SerializeField] private float lungeForce = 30.0f; // NOTE: ONLY NEED IF USING PHYSICS FOR LUNGE ATTACK
		[SerializeField] private float lungeWindupTime = 0.20f; // telegraphs lunge attack - so player can react (match windup part of lunge anim clip)
		[SerializeField] private float lungeLeadTime = 0.15f; // good ranges: 0.10–0.18
		[SerializeField] private float lungeMaxLaunchAngle = 45.0f; // degrees (good range: 0.35–0.55) // facing direction angle
		[SerializeField] private float lungeDuration = 0.22f; // good ranges: 0.18-0.30 (match active part of lunge anim clip)
		[SerializeField] private float lungeRecoveryTime = 0.4f; // (match downtime part of lunge anim clip)

		[Header("LOS Memory")]
		[SerializeField] private float targetMemoryDuration = 1.0f;

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [START] -

		public bool IsDead { get; private set; }
		public bool IsStunned { get; private set; }
		//public bool HasTarget => target != null;
		public bool HasTarget { get; private set; }
		//public bool HasLOS { get; private set; }

		public float DistanceToTarget { get; private set; }
		public bool InAttackRange => HasTarget && (InSwingRange || InLungeRange); // shared 'InAttackRange' node for sword enemy can mean 'InSwingRange' OR 'InLungeRange'

		public bool CanAttack => CanSwing; // shared 'CanAttack' node for sword enemy can mean 'CanSwing' (default primary attack)
		public bool IsAttacking { get; private set; }
		//public bool IsTargetTooClose { get; private set; }

		// - Implement IEnemyAgent Properties (Blackboard flags for BT) [END] -

		// - Sword Enemy Specific Properties [START] -

		public bool InSwingRange => HasTarget && DistanceToTarget <= swingRange;
		public bool InLungeRange => HasTarget && DistanceToTarget >= lungeMinRange && DistanceToTarget <= lungeMaxRange;
		public bool ShouldLunge => InLungeRange && !InSwingRange;
		public bool CanSwing => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextSwingTime;
		public bool CanLunge => !IsDead && !IsStunned && !IsAttacking && Time.time >= nextLungeTime;
		public bool HasLineOfSight { get; private set; }

		// - Sword Enemy Specific Properties [END] -

		// Cooldown/State timers
		private float nextSwingTime = -Mathf.Infinity;
		private float nextLungeTime = -Mathf.Infinity;
		private float stunEndTime = -Mathf.Infinity;
		private float attackEndTime = -Mathf.Infinity; // enforce min attack time (safety for anim not firing)
		private float lungeEndTime = -Mathf.Infinity;

		// Animator IDs
		private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed"); // Float
		private static readonly int AnimSwing = Animator.StringToHash("Swing"); // Trigger
		private static readonly int AnimLunge = Animator.StringToHash("Lunge"); // Trigger
		private static readonly int AnimHit = Animator.StringToHash("Hit"); // Trigger
		private static readonly int AnimDie = Animator.StringToHash("Die"); // Trigger

		// - Cached Components -
		private Health healthComponent;

		// Runtime params
		// (NOTE: SerializeField atm for tracking in inspector)
		/*[SerializeField]*/ private bool isLunging = false;
		/*[SerializeField]*/ private float currentHealth;
		/*[SerializeField]*/ private float previousHealthValue = 0.0f;
		/*[SerializeField]*/ private float lastDamageTime = -Mathf.Infinity; // TODO: use for invunerability window
		/*[SerializeField]*/ private float lastSeenTime = -Mathf.Infinity;
		private Coroutine lungeRoutine = null;
		private bool lungeInProgress = false;
		private Vector3 lungeDirection;
		private bool AgentReady => navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;
		//[SerializeField] private AttackData attackData = new AttackData(); // configured at start of each attack via animation event (if using one melee hitbox for both swing and lunge)

		// TEMP
		private int normalLayer;
		private int lungeLayer;

		// Helpers for ensuring that NavMesh agent functions can carry out 
		// TODO: CREATE NAVMESHAGENT HELPER SCRIPT WITH THESE
		private void SafeSetDestination(Vector3 pos) {
			if (AgentReady == false) {
				return;
			}
			navMeshAgent.SetDestination(pos);
		}

		private void SafeStopAgent() {
			if (AgentReady == false) {
				return;
			}
			navMeshAgent.isStopped = true;
		}

		private void SafeResumeAgent() {
			if (AgentReady == false) {
				return;
			}
			navMeshAgent.isStopped = false;
		}

		// Reset to default values
		private void Reset() {
			navMeshAgent = GetComponent<NavMeshAgent>();
			// rb = GetComponent<Rigidbody>(); // NOTE: ONLY NEED IF USING PHYSICS FOR LUNGE ATTACK
			meleeHitbox = GetComponentInChildren<MeleeHitbox>(true);
			swingHitbox = GetComponentInChildren<MeleeHitbox>(true);
			lungeHitbox = GetComponentInChildren<MeleeHitbox>(true);
			animator = GetComponentInChildren<Animator>();
		}

		private void Awake() {
			CacheComponents();
			ResetRuntimeToBaseValues();
			InitialRuntimeSetup();

			if (losSensor == null) {
				losSensor = GetComponent<LOSSensor>();
			}

			normalLayer = LayerMask.NameToLayer("Enemy");
			lungeLayer = LayerMask.NameToLayer("EnemyNoPush");
		}

		// - Initialisation Functions (Called inside Awake()) [START] -

		private void CacheComponents() {
			if (navMeshAgent == null) {
				navMeshAgent = GetComponent<NavMeshAgent>();
			}
			if (rb == null) {
				rb = GetComponent<Rigidbody>(); // NOTE: ONLY NEED IF USING PHYSICS FOR LUNGE ATTACK
				rb.isKinematic = true; // NavMesh controls movement
			}
			if (meleeHitbox == null) {
				meleeHitbox = gameObject.GetComponentInChildren<MeleeHitbox>(true);
			}
			if (swingHitbox == null) {
				swingHitbox = gameObject.GetComponentInChildren<MeleeHitbox>(true);
			}
			if (lungeHitbox == null) {
				lungeHitbox = gameObject.GetComponentInChildren<MeleeHitbox>(true);
			}
			if (animator == null) {
				animator = GetComponentInChildren<Animator>();
			}
		}

		private void ResetRuntimeToBaseValues() {
			// Set Default values
			navMeshAgent.speed = defaultSpeed;
			navMeshAgent.acceleration = defaultAcceleration;
			navMeshAgent.angularSpeed = rotationSpeed;
		}

		private void InitialRuntimeSetup() {
			// Sync health
			currentHealth = maxHealth;

			// Configure melee swing & lunge hit box's with data
			swingHitbox.Initialise(swingAttackData);
			lungeHitbox.Initialise(lungeAttackData);
		}

		// - Initialisation Functions (Called inside Awake()) [END] -

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
				
			// Handle stun timer
			if (IsStunned == true && Time.time >= stunEndTime) {
				IsStunned = false;
			}

			// Handle Attack lock timer (prevents immediate re-trigger spam even if anim event misfires)
			// NOTE: THIS ANIM EVENT IS A SAFETY NET IN CASE ANIM DOES NOT FIRE OR ISN'T WIRED CORRECTLY
			if (IsAttacking && Time.time >= attackEndTime) {
				IsAttacking = false;
			}

			// Perform lunge attack - NavMesh Method
			//if (isLunging == true) {
			//	LungeTick();
			//}

			// Animator movement speed
			// NOTE: Can do Root motion lunge attack where animation drives movement for more precision (requires good animations but is typically the most polished)
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

		// Lunge Windup/Rotation Coroutine
		private IEnumerator LungeCoroutine() {
			Debug.Log("LUNGE ATTACK");

			SafeStopAgent();
			navMeshAgent.updateRotation = false;

			// Windup
			float windupTimer = 0.0f;
			while (windupTimer < lungeWindupTime) {
				// Interrupt Lunge (also done in BT but here acts as safety)
				if (IsDead == true || IsStunned == true || target == null) {
					EndLunge();
					yield break;
				}

				// Face target during windup -> micro adjustments = fast enemy reaction time
				//toggleSmoothRotation = false; // reactive rotation for lunge windup - have seperate functions for facing on windup + chase
				FaceTarget(target.position);

				// Increment windup timer
				windupTimer += Time.deltaTime;

				yield return null;
			}

			// Wait until animation event ends lunge attack
			while (IsAttacking == true) {
				yield return null;
			}

			EndLunge();
		}

		// Predict the player position using player velocity alongside a time lead (predicat ahead of time)
		private Vector3 PredictAimPoint() {
			// Aim at players chest (0.8 - 1.2 for the multiplier depending on player height)
			Vector3 baseAim = target.position + Vector3.up * 1.0f;

			if (target.TryGetComponent(out Rigidbody rb) == true) {
				return baseAim + rb.linearVelocity * lungeLeadTime;
			}

			// TODO: Expose velocity & use that instead
			return baseAim; 
		}

		// Ensure that enemy is facing their targets direction within a given angle (prevents side-ways lunging)
		private bool IsFacingDirection(Vector3 worldDirection) {
			if (target == null) {
				return false;
			}

			worldDirection.y = 0.0f;
			if (worldDirection.sqrMagnitude < 0.001f) {
				return true;
			}

			float facingAngle = Vector3.Angle(transform.forward, worldDirection);

			return facingAngle <= lungeMaxLaunchAngle;
		}

		// Lunge dash start called by animation event at the beginning of stab clip
		// Calculate dash direction at exact launch moment + ensure enemy is facing within a given angle
		public void OnLungeDashStart() {
			// Stop lunge
			if (target == null || rb == null) {
				return;
			}

			gameObject.layer = lungeLayer;

			// Compute the lunge dash direction (this is locked at launch to prevent aimbot-like tracking)
			Vector3 aim = PredictAimPoint();
			Vector3 direction = aim - transform.position;
			direction.y = 0.0f;

			if (direction.sqrMagnitude < 0.001f) {
				direction = transform.forward;
			}
			else {
				lungeDirection = direction.normalized;
			}

			// Check if sword enemy is facing enough within a given angle
			// If not, snap rotation to the dash direction before launch
			if (IsFacingDirection(lungeDirection) == false) {
				Quaternion desiredRotation = Quaternion.LookRotation(lungeDirection);
				transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
			}

			// Disable NavMesh agent (for physX lunge movement)
			if (navMeshAgent != null) {
				navMeshAgent.enabled = false;
			}

			rb.isKinematic = false;
			rb.linearVelocity = Vector3.zero;
			//rb.angularVelocity = Vector3.zero;

			SetLayerRecursively(gameObject, lungeLayer);

			Debug.Log("DashStart");

			// Dash in the same direction used for enemy facing 
			rb.AddForce(lungeDirection * lungeForce, ForceMode.VelocityChange);
		}

		void SetLayerRecursively(GameObject obj, int layer) {
			obj.layer = layer;
			foreach (Transform child in obj.transform)
				SetLayerRecursively(child.gameObject, layer);
		}

		// Lunge dash end called by animation event when dash motion is over in stab clip (same time as hitbox being disabled) 
		public void OnLungeDashEnd() {
			gameObject.layer = normalLayer;

			Debug.Log("DashEnd");

			rb.linearVelocity = Vector3.zero;
			//rb.angularVelocity = Vector3.zero;
			rb.isKinematic = true;

			SetLayerRecursively(gameObject, normalLayer);

			navMeshAgent.enabled = true;
			navMeshAgent.Warp(transform.position);
			SafeResumeAgent();
			//navMeshAgent.updatePosition = true;
			navMeshAgent.updateRotation = true; // if you want agent rotation again
		}

		// Cleanup Lunge attack
		// NOTE: CALL ON STUN INTERRUPTS LATER (will interrupt lunge attack motion)
		private void EndLunge() {
			// PhysX cleanup
			//if (rb != null) {
			//	rb.linearVelocity = Vector3.zero;
			//	rb.angularVelocity = Vector3.zero;
			//	rb.isKinematic = true;
			//}
			// NavMesh cleanup - re-sync NavMesh agent after applying Rigidbody physics
			//if (navMeshAgent != null) {
			//	if (navMeshAgent.enabled == false) {
			//		navMeshAgent.enabled = true;
			//	}
			//	SafeResumeAgent();
			//	navMeshAgent.updatePosition = true;
			//	navMeshAgent.updateRotation = true;
			//	navMeshAgent.Warp(transform.position);
			//}

			// NOTE: Safety for disabling melee hitbox in case it wasn't inside the animation (delete later)
			if (meleeHitbox != null) {
				meleeHitbox.Disable();
			}

			// Runtime param cleanup
			isLunging = false;
			IsAttacking = false;
			lungeInProgress = false;

			lungeRoutine = null;
		}

		//private void LungeTick() {
		//	if (navMeshAgent == null) {
		//		isLunging = false;
		//		return;
		//	}
		//	// IMPORTANT NOTE: CONDITIONS LIKE THIS SHOULD BE CHECKED IN ONE PLACE (SOME ARE BEING CHECKED IN BT AND CODE ATM I BELIVE)
		//	if (HasTarget == false) {
		//		isLunging = false;
		//		return;
		//	}

		//	// Finish lunge attack before 'lungeEndTime' (safety for anim not firing correctly)
		//	if (Time.time >= lungeEndTime) {
		//		isLunging = false;
		//		return;
		//	}

		//	// Don't overshoot into the player
		//	if (DistanceToTarget <= lungeStopDistance) {
		//		isLunging = false;
		//		return;
		//	}

		//	// Get forward direction
		//	Vector3 forward = transform.forward;

		//	// OPTIONAL: Stop lunging if there is a wall in front of the sword enemy
		//	// 0.8f = height offset to check for obstacles | 0.6f = max ray distance
		//	if (Physics.Raycast(transform.position + Vector3.up * 0.8f, forward, 0.6f, lungeBlockers)) {
		//		isLunging = false;
		//		return;
		//	}

		//	// Calculate target point (past the player for commitment - deliberate overshoot on miss)
		//	//Vector3 direction = (target.position - transform.position).normalized;
		//	//Vector3 lungeTarget = target.position + direction * lungePastPlayer;

		//	// #1 Lunge forward (sword enemy remains on navmesh)
		//	// + Best way to work with NavMesh since using '.Move' and keeping nav active | - idk at the moment
		//	//navMeshAgent.speed = lungeSpeed;
		//	//navMeshAgent.acceleration = lungeAcceleration;
		//	navMeshAgent.isStopped = true;
		//	navMeshAgent.Move(forward * (lungeSpeed * Time.deltaTime)); // set speed + accel to test (possibly)

		//	// #2 Increase NavMesh agent speed and set lunge destination (bursts forward - but uses normal pathing)
		//	// + Simple | - Pathfinding can curve the lunge if NavMesh agent is avoiding obstacles
		//	//navMeshAgent.speed = lungeSpeed * 1.5f;// 150% of default speed
		//	//navMeshAgent.acceleration = acceleration * 1.5f; // 150% of default acceleration
		//	//navMeshAgent.SetDestination(transform.position + forward * lungeDistance);
		//	// NOTE: USING RestoreSpeedAccel() with the above method (#2)

		//	// #3 Disable NavMesh agent + use Rigidbody to dash
		//	// + Uses physics | - Have to resync NavMesh agent afte (can be difficult to get right)
		//	//navMeshAgent.enabled = false;
		//	// Use Rigidbody here

		//	// Restore normal stats
		//	//navMeshAgent.speed = defaultSpeed;
		//	//navMeshAgent.acceleration = defaultAcceleration;
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

			// Disable nav mesh agent upon death
			if (navMeshAgent != null) {
				SafeStopAgent();
				navMeshAgent.enabled = false;
			}

			// Play death animation for sword enemy
			if (animator != null) {
				animator.SetTrigger(AnimDie);
			}

			// NOTE: This is temporary
			Destroy(gameObject);
		}

		public void RecoverTick() {
			if (IsDead == true) {
				return;
			}

			StopMove();
		}

		public void ChaseTargetTick() {
			// Chase interrupts
			if (IsDead == true || IsStunned == true || IsAttacking == true) {
				return;
			}
			// No target
			if (HasTarget == false || navMeshAgent == null) {
				return;
			}
			if (AgentReady == false) {
				return;
			}

			SafeResumeAgent();
			SafeSetDestination(target.position);

			// Continuously adjust direction when chasing target
			//toggleSmoothRotation = true; // smooth rotation for chase - lunges sideways due to slow turning from chase
			FaceTarget(target.position);
		}

		public void StopMove() {
			if (navMeshAgent != null) {
				SafeStopAgent();
			}	
		}

		// Shared action node 'PrimaryAttack' can be used
		// But for sword map primary attack to 'Swing' instead (default attack)
		public bool TryStartPrimaryAttack() {
			return TryStartSwing();
		}

		// - Implement IEnemyAgent Methods [END] -

		// - Sword Enemy Specifc BT Action Node Execution -

		public bool TryStartSwing() {
			if (CanSwing == false) {
				return false;
			}
			// OPTIONAL: Remove if you want swing attack to begin slightly out of range as player may move out of range in that small window
			if (InSwingRange == false) {
				return false;
			}

			StopMove();

			IsAttacking = true;
			nextSwingTime = Time.time + swingCooldown;
			attackEndTime = Time.time + swingLockTime;

			if (animator != null) {
				// Set Swing anim trigger (plays swing anim)
				animator.ResetTrigger(AnimLunge);
				animator.SetTrigger(AnimSwing);
			}
				
			return true;
		}

		public bool TryStartLunge() {
			if (CanLunge == false || InLungeRange == false) {
				return false;
			}
			if (lungeInProgress == true) {
				return false;
			}

			StopMove();

			IsAttacking = true;
			isLunging = true;

			lungeEndTime = Time.time + lungeDuration; // NOTE: NEEDED?
			nextLungeTime = Time.time + lungeCooldown;
			attackEndTime = Time.time + lungeLockTime; // NOTE: NEEDED? (Anim event no longer decides when to stop attack - PhysX method)

			// Begin lunge attack Coroutine (PhysX method)
			lungeInProgress = true;
			lungeRoutine = StartCoroutine(LungeCoroutine());

			if (animator != null) {
				// Set Lunge anim trigger (plays windup anim -> stab anim)
				animator.ResetTrigger(AnimSwing);
				animator.SetTrigger(AnimLunge);
			}
			
			// NOTE: PHYSX METHOD HANDLES WINDUP ANIM - WHICH USES THIS TRIGGER
			//if (animator != null) {
			//	animator.SetTrigger(AnimLunge);
			//}

			return true;
		}

		// - Damge / Stun -

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
			if (IsDead == true) {
				return;
			}

			IsStunned = true;
			IsAttacking = false;
			stunEndTime = Time.time + duration;

			StopMove();
		}

		// - Animation Events -

		// NOTE: DONT CALL IN ANIM EVENT (makes adding new anims later easier) 
		// Call this at start of attack animation via event - 
		//public void AnimEvent_AttackStart() {
		//	IsAttacking = true;
		//}

		// NOTE: DONT CALL IN ANIM EVENT (makes adding new anims later easier) - Do this for Swing for now
		// Call this at end of attack animation via event
		public void AnimEvent_AttackFinished() {
			IsAttacking = false;
		}

		// Hitbox toggles (call inside animation event)
		public void AnimEvent_EnableHitbox() {
			meleeHitbox.Enable();
			// TODO: ADD LOGIC TO AnimEvent_EnableHitbox()
		}
		public void AnimEvent_DisableHitbox() {
			meleeHitbox.Disable();
			// TODO: ADD LOGIC TO AnimEvent_DisableHitbox()
		}
		public void AnimEvent_EnableSwingHitbox() {
			// TEMP
			swingHitbox.Enable();
		}
		public void AnimEvent_DisableSwingHitbox() {
			// TEMP
			swingHitbox.Disable();
		}
		public void AnimEvent_EnableLungeHitbox() {
			// TEMP
			// NOTE: Can Drive movement from animation instead
			// Forward lunge for melee attack
			//Vector3 meleeLunge = transform.position += transform.forward * 0.6f;
			//navMeshAgent.Move(meleeLunge);
			lungeHitbox.Enable();
		}
		public void AnimEvent_DisableLungeHitbox() {
			// TEMP
			lungeHitbox.Disable();
		}

		// - Setup Melee Attack Data (Swing + Lunge) -

		// Swing attack animation event calls this at the beginning of the animation
		public void AnimEvent_SetupSwingData() {
			// Configure melee hitbox with swing attack data
			meleeHitbox.Initialise(swingAttackData);
		}
		// Lunge attack animation event calls this at the beginning of the animation
		public void AnimEvent_SetupLungeData() {
			// Configure melee hitbox with lunge attack data
			meleeHitbox.Initialise(lungeAttackData);
		}

		// - Movement Helpers -
		// TODO: Place inside MovementHelpers.cs script later
		// NOTE: Might need to replace NavMesh angular speed with rotation speed

		// Turn to face the targets direction (used when chasing to maintain LOS)
		private void FaceTarget(Vector3 worldPos) {
			Vector3 direction = worldPos - transform.position;
			direction.y = 0.0f;

			// Safety - ensures that a direction exists before rotating towards it
			if (direction.sqrMagnitude < 0.0001f) {
				return;
			}
			
			Quaternion targetRotation = Quaternion.LookRotation(direction/*.normalized*/); // NOTE:ADD normalised for other enemies here as well

			if (toggleSmoothRotation == true) {
				// Smooth rotation towards target direction (using Slerp)
				// NOTE: USE SMOOTH ROTATION FOR CHASING THEN ROTATETOWARDS FOR WINDUP
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothing);
			} else {
				// Responsive rotation towards target direction (using RotateTowards)
				// [BETTER FOR SWORD since precise, melee facing]
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}

		// - Event & Callback handlers -

		private void OnEnable() {
			if (healthComponent != null) {
				previousHealthValue = healthComponent.CurrentHealth;
				healthComponent.OnHealthChanged += OnHealthChanged;
			}
		}
		private void OnDisable() {
			if (healthComponent != null) {
				healthComponent.OnHealthChanged -= OnHealthChanged;
			}
		}

		private void OnHealthChanged(float current, float max) {
			// Damage only if health has gone down
			if (current < previousHealthValue) {
				// Reset regen delay timer - only regen when out of combat
				lastDamageTime = Time.time;
			}
			previousHealthValue = current;
		}
	}
}

// NOTE: LATEST LUNGE ATTACK Physics Method -> Currently using Animation events to call lunge windup and stab animation instead for consistency 
// But using Anim events means less freedom in tuning -> can use windup + duration + recovery variables to test feel for correct lunge phase timings

//// Lunge Windup + Dash + Recovery (uses Rigidbody physX)
//private IEnumerator LungeCoroutine() {
//	Debug.Log("LUNGE ATTACK");

//	// Windup
//	navMeshAgent.isStopped = true;
//	navMeshAgent.updatePosition = false;
//	navMeshAgent.updateRotation = false;
//	rb.isKinematic = true;

//	// Set Lunge anim trigger (plays windup anim -> stab anim)
//	animator.ResetTrigger(AnimSwing);
//	animator.SetTrigger(AnimLunge);

//	float windupTimer = 0.0f;
//	while (windupTimer < lungeWindupTime) {
//		// Interrupt Lunge (also done in BT, but here acts as safety)
//		if (IsDead == true || IsStunned == true  || target == null) { 
//			EndLunge(); 
//			yield break; 
//		}

//		// Face target during windup -> micro adjustments = fast enemy reaction time
//		//toggleSmoothRotation = false; // reactive rotation for lunge windup - have seperate functions for facing on windup + chase
//		FaceTarget(target.position);

//		// Increment windup timer
//		windupTimer += Time.deltaTime;

//		yield return null;
//	}

//	// Lock direction at lunge - commit to attack
//	Vector3 lungeDirection = target.position - transform.position;
//	lungeDirection.y = 0.0f;

//	// If lunge direction is not much change from current direction (lunge forward)
//	if (lungeDirection.sqrMagnitude < 0.0001f) {
//		// Face forward only
//		lungeDirection = transform.forward;
//	}

//	lungeDirection.Normalize();

//	// Lunge
//	// Disable NavMesh + enable physics
//	navMeshAgent.enabled = false;
//	rb.isKinematic = false;

//	RigidbodyConstraints previousConstraints = rb.constraints;
//	rb.constraints |= RigidbodyConstraints.FreezeRotation;

//	// Clear previous movement before lunge
//	rb.linearVelocity = Vector3.zero;
//	rb.angularVelocity = Vector3.zero;

//	// Apply lunge force
//	rb.AddForce(lungeDirection * lungeForce, ForceMode.VelocityChange);
//	// NavMesh method - TODO: TRY AND MAKE THIS HAPPEN INSTEAD
//	//navMeshAgent.Move(lungeDirection * lungeSpeed * Time.deltaTime);

//	yield return new WaitForSeconds(lungeDuration);

//	// Recovery
//	rb.linearVelocity = Vector3.zero;
//	rb.angularVelocity = Vector3.zero;
//	rb.constraints = previousConstraints;
//	rb.isKinematic = true;

//	// Re-enable NavMesh (NOTE: Can warp to a valid position - but may look too much like teleporting)
//	// NOTE: Using warp is better than sampling + setting position
//	//if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) {
//	//	transform.position = hit.position;
//	//}


//	navMeshAgent.enabled = true;
//	//navMeshAgent.Warp(transform.position);
//	//navMeshAgent.updatePosition = true;
//	//navMeshAgent.updateRotation = true;
//	//navMeshAgent.isStopped = false;

//	// Downtime after lunging
//	yield return new WaitForSeconds(lungeRecoveryTime);

//	EndLunge();
//}

// NOTE: CAN USE LATER FOR LUNGE ATTACK - Physics Method

//public void StartLungeAttack() {
//	if (isLunging == true) {
//		StartCoroutine(LungeCoroutine());
//	}
//}

//private IEnumerator LungeCoroutine() {
//	if (navMeshAgent == null) {
//		isLunging = false;
//		yield return null;
//	}
//	// IMPORTANT NOTE: CONDITIONS LIKE THIS SHOULD BE CHECKED IN ONE PLACE (SOME ARE BEING CHECKED IN BT AND CODE ATM I BELIVE)
//	if (HasTarget == false) {
//		isLunging = false;
//		yield return null;
//	}

//	// Finish lunge attack before 'lungeEndTime' (safety for anim not firing correctly)
//	if (Time.time >= lungeEndTime) {
//		isLunging = false;
//		yield return null;
//	}

//	// Don't overshoot into the player
//	if (DistanceToTarget <= lungeStopDistance) {
//		isLunging = false;
//		yield return null;
//	}

//	// Wind up phase
//	navMeshAgent.isStopped = true;

//	// Get forward direction
//	Vector3 lungeDirection = (target.position - transform.position).normalized;
//	lungeDirection.y = 0.0f;

//	// Face player during windup - use helper later
//	Quaternion targetRotation =  Quaternion.LookRotation(lungeDirection);
//	float windupTimer = 0.0f;

//	while (windupTimer < lungeWindupTime) {
//		transform.rotation =  Quaternion.Slerp( transform.rotation, targetRotation, windupTimer / lungeWindupTime);
//		windupTimer += Time.deltaTime;
//		// TODO: Trigger windup animation here
//		yield return null;
//	}

//	// Lunge phase
//	// Disable NavMesh + enable physics
//	navMeshAgent.enabled = false;
//	rb.isKinematic = false;

//	// Apply lunge force
//	rb.AddForce(lungeDirection * lungeForce, ForceMode.VelocityChange);

//	yield return new WaitForSeconds(lungeDuration);

//	// Recovery Phase
//	rb.linearVelocity = Vector3.zero;
//	rb.isKinematic = true;

//	// Re-enable NavMesh (NOTE: Can warp to a valid position - but may look too much like teleporting)
//	// NOTE: Main disadvantage of disabling NavMesh to use physics during lunge
//	if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) {
//		transform.position = hit.position;
//	}

//	navMeshAgent.enabled = true;
//	navMeshAgent.isStopped = false;

//	yield return new WaitForSeconds(lungeRecoveryTime);

//	isLunging = false;
//}