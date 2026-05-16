using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static UnityEditor.Recorder.OutputPath;


#if UNITY_EDITOR
using UnityEditor;
#endif

// Airlock scene transition
// This script should be placed on a controller object rather than the full airlock room root
// Everything under this controller object will survive the old scene unload until Destroy(gameobject)
// This does the following:
// - preloads the next scene additively up to Unity's activation point,
// - seals the player inside an old-scene airlock room,
// - activates the next scene during a fade/blackout,
// - teleports only the player to the target SeamlessSceneEntryPoint,
// - unloads the previous level before fading back in
[DisallowMultipleComponent]
public sealed class AirlockSceneTransitionPortal : MonoBehaviour {
	[Serializable]
	public sealed class LoadingProgressEvent : UnityEvent<float> { }

	[Serializable]
	public sealed class SceneNameEvent : UnityEvent<string> { }

	[Header("Scene Target")]
	[Tooltip("Name of the next level scene to load additively.")]
	[SerializeField] private string nextSceneName;

	[Header("Portal Locking")]
	[Tooltip("If true, this airlock starts locked until UnlockPortal is called.")]
	[SerializeField] private bool startsLocked = false;

	[Header("Scene Safety")]
	[Tooltip("Scene names that must never be unloaded by this portal. Example: Persistent Player scene.")]
	[SerializeField] private string[] protectedSceneNames = { "Player" };

	[Header("Door / Airlock")]
	[Tooltip("Door the player enters from the old level into the airlock room.")]
	[SerializeField] private AirlockDoorController airlockEntranceDoor;
	[Tooltip("Optional animator used for airlock effects such as warning lights, pressure effects, fog, etc.")]
	[SerializeField] private Animator airlockAnimator;
	[Tooltip("Animator trigger fired when the airlock starts sealing.")]
	[SerializeField] private string pressuriseTriggerName = "Pressurise";
	[Tooltip("Animator trigger fired after the player has been moved to the next scene.")]
	[SerializeField] private string depressuriseTriggerName = "Depressurise";

	[Header("Player Handoff")]
	[Tooltip("Tag used to find the persistent player object.")]
	[SerializeField] private string playerTag = "Player";
	[Tooltip("Optional direct player Rigidbody reference. If left empty, the player is found by tag instead.")]
	[SerializeField] private Rigidbody playerRigidbody;
	[Tooltip("Point inside the old-scene airlock room where the player is held while the transition effect plays.")]
	[SerializeField] private Transform airlockPlayerHoldPoint;
	[Tooltip("If true, the player is snapped to the hold point when the airlock sequence starts.")]
	[SerializeField] private bool movePlayerToHoldPoint = true;
	[Tooltip("If true, player input/movement is disabled while the airlock is sealed.")]
	[SerializeField] private bool lockPlayerDuringTransition = true;
	[Tooltip("If true, the player's rotation is set to match the next scene entry point.")]
	[SerializeField] private bool alignPlayerToEntryPointRotation = true;
	[Tooltip("How the player's velocity is handled after teleporting to the next scene.")]
	[SerializeField] private AirlockPlayerHandoff.VelocityHandling velocityHandling = AirlockPlayerHandoff.VelocityHandling.Dampen;
	[Tooltip("Velocity multiplier used when Velocity Handling is set to Dampen.")]
	[SerializeField][Range(0.0f, 1.0f)] private float velocityDamping = 0.2f;
	[Tooltip("If true, angular velocity is cleared after teleporting.")]
	[SerializeField] private bool clearAngularVelocity = true;
	[Tooltip("Vertical offset used when setting the checkpoint to the entry point.")]
	[SerializeField] private float checkpointVerticalOffset = 1.5f;

	[Header("Fade")]
	[Tooltip("CanvasGroup used for fade/blackout effect.")]
	[SerializeField] private CanvasGroup fadeGroup;
	[Tooltip("Seconds used to fade to black before activating the next scene.")]
	[SerializeField] private float fadeOutDuration = 0.25f;
	[Tooltip("Seconds used to fade back in after the old scene unloads.")]
	[SerializeField] private float fadeInDuration = 0.35f;
	[Tooltip("Minimum time to keep the screen black after teleporting the player.")]
	[SerializeField] private float minimumBlackoutTime = 0.15f;

	[Header("Timing")]
	[Tooltip("Delay after closing the airlock entrance door before the fade begins.")]
	[SerializeField] private float airlockEntranceCloseDelay = 0.35f;
	[Tooltip("Delay after the pressurise animation starts before fading out.")]
	[SerializeField] private float pressuriseDelay = 0.2f;
	[Tooltip("Delay after the handoff before fading back in.")]
	[SerializeField] private float postHandoffDelay = 0.1f;
	[Tooltip("Delay after fade-in before restoring player control.")]
	[SerializeField] private float restoreControlDelay = 0.05f;

	[Header("Audio")]
	[Tooltip("Optional audio source for transition sounds.")]
	[SerializeField] private AudioSource transitionAudioSource;
	[Tooltip("Clip played when the airlock starts sealing.")]
	[SerializeField] private AudioClip airlockStartClip;
	[Tooltip("Clip played during the hidden player handoff.")]
	[SerializeField] private AudioClip handoffClip;
	[Tooltip("Clip played when the transition finishes.")]
	[SerializeField] private AudioClip transitionCompleteClip;

	[Header("Unity Events")]
	[Tooltip("Invoked when preload starts.")]
	[SerializeField] private SceneNameEvent onPreloadStarted;
	[Tooltip("Invoked while preloading. Value is 0-1.")]
	[SerializeField] private LoadingProgressEvent onPreloadProgress;
	[Tooltip("Invoked when preload reaches Unity's activation-ready point.")]
	[SerializeField] private SceneNameEvent onPreloadComplete;
	[Tooltip("Invoked when the sealed airlock sequence begins.")]
	[SerializeField] private UnityEvent onAirlockStarted;
	[Tooltip("Invoked after the old airlock door has been told to close.")]
	[SerializeField] private UnityEvent onInnerDoorClosed;
	[Tooltip("Invoked after the player has been moved to the next scene entry point.")]
	[SerializeField] private UnityEvent onPlayerHandoff;
	[Tooltip("Invoked after the next scene is activated.")]
	[SerializeField] private SceneNameEvent onNextSceneActivated;
	[Tooltip("Invoked after the previous scene unloads.")]
	[SerializeField] private SceneNameEvent onPreviousSceneUnloaded;
	[Tooltip("Invoked when the transition is fully complete.")]
	[SerializeField] private UnityEvent onTransitionComplete;
	[Tooltip("Invoked if the transition fails.")]
	[SerializeField] private UnityEvent onTransitionFailed;

	[Header("Debug")]
	[Tooltip("If true, the airlock transition process is logged to the Unity Console.")]
	[SerializeField] private bool debugLogging = true;
	[Tooltip("Colour used for the airlock hold-point gizmo.")]
	[SerializeField] private Color holdPointGizmoColor = new Color(0.1f, 0.85f, 1.0f, 1.0f);

	public AirlockStatus CurrentStatus => currentStatus;
	public bool IsLocked => locked;

	// This stays true only while the scene is actively loading toward the activation-ready point
	public bool IsPreloading => preloadRequested && preloadReadyToActivate == false;

	public bool IsReadyToActivate => preloadReadyToActivate;
	public bool IsTransitionRunning => transitionRunning;
	public float LoadingProgress => loadingProgress;

	private AirlockStatus currentStatus = AirlockStatus.Idle;

	private AsyncOperation preloadOperation;
	private Coroutine preloadRoutine;

	private bool preloadRequested;
	private bool preloadReadyToActivate;
	private bool transitionRunning;
	private bool locked;

	private float loadingProgress;

	private Scene loadedScene;
	private SeamlessSceneRoot loadedRoot;
	private SeamlessSceneEntryPoint targetEntryPoint;
	private AirlockPlayerHandoff playerHandoff;

	private void Reset() {
		airlockEntranceDoor = GetComponentInChildren<AirlockDoorController>();
		airlockAnimator = GetComponentInChildren<Animator>();
		transitionAudioSource = GetComponentInChildren<AudioSource>();
	}

	private void Awake() {
		locked = startsLocked;
		SetStatus(locked ? AirlockStatus.Locked : AirlockStatus.Idle);

		// If the portal starts locked, also lock the entrance door visually/physically
		if (locked && airlockEntranceDoor != null) {
			airlockEntranceDoor.LockDoor();
		}
	}

	// Locks this transition portal
	// Called by alarms/enemy systems if the exit should be blocked
	public void LockPortal() {
		locked = true;
		airlockEntranceDoor?.LockDoor();
		SetStatus(AirlockStatus.Locked);
		Log("Airlock portal locked.");
	}
	
	//  Unlocks this transition portal so preload and handoff can begin
	 // Called when the level is allowed to transition
	public void UnlockPortal() {
		locked = false;
		airlockEntranceDoor?.UnlockDoor();
		SetStatus(preloadReadyToActivate ? AirlockStatus.ReadyToActivate : AirlockStatus.Idle);
		Log("Airlock portal unlocked.");
	}


	// Called by AirlockTransitionVolume when the player enters a airlock transition trigger
	public void HandleAirlockVolumeEntered(AirlockTransitionVolume.TriggerAction action, Collider playerCollider) {
		switch (action) {
			case AirlockTransitionVolume.TriggerAction.Preload:
				// Start loading early and open the entrance door into the old-scene airlock room
				RequestPreload();
				OpenAirlockEntranceDoor();
				break;

			case AirlockTransitionVolume.TriggerAction.AirlockEntered:
				// Make sure loading has started, then close the door behind the player
				RequestPreload();
				CloseAirlockEntranceDoorBehindPlayer();
				break;

			case AirlockTransitionVolume.TriggerAction.Commit:
				// Player has reached the sealed part of the airlock
				// Start the hidden handoff
				BeginAirlockTransition(playerCollider);
				break;
		}
	}


	// Starts additive preloading
	// The loaded scene is loaded to just before activation (90%), then held inactive until airlock blackout moment
	[ContextMenu("Request Preload")]
	public void RequestPreload() {
		if (locked) {
			Debug.LogWarning($"{name}: Preload requested while the airlock portal is locked.", this);
			SetStatus(AirlockStatus.Locked);
			return;
		}

		// Prevent multiple preload operations for the same portal
		if (preloadRequested) {
			return;
		}

		if (ValidatePortal(false) == false) {
			FailTransition("Preload validation failed.");
			return;
		}

		preloadRequested = true;
		preloadRoutine = StartCoroutine(PreloadRoutine());
	}

	// Starts the full sealed airlock transition
	[ContextMenu("Begin Airlock Transition")]
	public void BeginAirlockTransition() {
		BeginAirlockTransition(null);
	}

	// Starts the full sealed airlock transition
	public void BeginAirlockTransition(Collider playerCollider) {
		if (transitionRunning) {
			Debug.LogWarning($"{name}: Airlock transition triggered while already running.", this);
			return;
		}

		if (locked) {
			Debug.LogWarning($"{name}: Airlock transition requested while portal is locked.", this);
			SetStatus(AirlockStatus.Locked);
			return;
		}

		if (ValidatePortal(true) == false) {
			FailTransition("Transition validation failed.");
			return;
		}

		transitionRunning = true;

		// Store the old scene before this portal object is moved to DontDestroyOnLoad
		Scene previousScene = gameObject.scene;

		// This portal started in the old level scene
		// Keep this controller alive so it can finish fade-in/events even if its old scene unloads
		transform.SetParent(null, true);
		DontDestroyOnLoad(gameObject);

		// Run the transition on a persistent coroutine runner so it can survive scene unloads
		SeamlessSceneRuntimeRunner.Run(AirlockTransitionRoutine(previousScene, playerCollider));
	}

	// Validation for any setup mistakes
	[ContextMenu("Validate Airlock Portal")]
	public bool ValidatePortal() {
		return ValidatePortal(true);
	}

	private IEnumerator PreloadRoutine() {
		SetStatus(AirlockStatus.Preloading);

		loadingProgress = 0.0f;
		onPreloadProgress?.Invoke(loadingProgress);
		onPreloadStarted?.Invoke(nextSceneName);

		Log($"Preloading next scene '{nextSceneName}'.");

		// If something else already loaded this scene, treat it as ready
		Scene alreadyLoadedScene = SceneManager.GetSceneByName(nextSceneName);
		if (alreadyLoadedScene.IsValid() && alreadyLoadedScene.isLoaded) {
			loadedScene = alreadyLoadedScene;
			MarkPreloadComplete();
			yield break;
		}

		try {
			preloadOperation = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);
		} 
		catch (Exception exception) {
			Debug.LogError($"{name}: Could not start additive load for scene '{nextSceneName}'. Make sure it is added to Build Settings?\n{exception}", this);
			FailTransition("LoadSceneAsync threw an exception.");
			yield break;
		}

		if (preloadOperation == null) {
			FailTransition("LoadSceneAsync returned null.");
			yield break;
		}

		// Unity loads the scene in the background but does not activate/render it into the world yet
		preloadOperation.allowSceneActivation = false;

		while (preloadOperation.isDone == false) {
			// When allowSceneActivation is false, Unity stops async scene loading at 0.9
			// Dividing by 0.9 so UI/events receive a normalised 0-1 loading value
			loadingProgress = Mathf.Clamp01(preloadOperation.progress / 0.9f);
			onPreloadProgress?.Invoke(loadingProgress);

			if (preloadOperation.progress >= 0.9f) {
				// Scene is loaded as far as Unity allows before activation
				MarkPreloadComplete();
				yield break;
			}

			yield return null;
		}
	}

	private IEnumerator AirlockTransitionRoutine(Scene previousScene, Collider playerCollider) {
		SetStatus(AirlockStatus.AirlockSealing);

		onAirlockStarted?.Invoke();
		PlayOneShot(airlockStartClip);

		// If the player reached the commit trigger before preload started, start it now
		RequestPreload();

		if (ResolvePlayer(playerCollider) == false) {
			FailTransition("Player could not be found.");
			yield break;
		}

		// Stop input/movement while the airlock seals and the player is moved
		if (lockPlayerDuringTransition && playerHandoff != null) {
			playerHandoff.LockPlayer();
		}

		// Optional: snap the player to a centre point inside the airlock before fading out
		if (movePlayerToHoldPoint && airlockPlayerHoldPoint != null && playerHandoff != null) {
			playerHandoff.MoveToHoldPoint(airlockPlayerHoldPoint, true, true);
		}

		CloseAirlockEntranceDoorBehindPlayer();
		onInnerDoorClosed?.Invoke();

		if (airlockEntranceCloseDelay > 0.0f) {
			yield return new WaitForSeconds(airlockEntranceCloseDelay);
		}

		// Play pressure/fog animation before blackout
		SetAnimatorTrigger(airlockAnimator, pressuriseTriggerName);

		if (pressuriseDelay > 0.0f) {
			yield return new WaitForSeconds(pressuriseDelay);
		}

		// Hide the screen before activating the next scene
		yield return FadeTo(1.0f, fadeOutDuration);

		SetStatus(AirlockStatus.ActivatingNextScene);
		yield return ActivateLoadedSceneDuringBlackout();

		if (currentStatus == AirlockStatus.Failed) {
			yield break;
		}

		SetStatus(AirlockStatus.HandingOffPlayer);
		PlayOneShot(handoffClip);

		if (playerHandoff != null && targetEntryPoint != null) {
			// Only the player moves
			playerHandoff.TeleportToEntryPoint(
				targetEntryPoint.transform, 
				alignPlayerToEntryPointRotation, 
				velocityHandling, 
				velocityDamping, 
				clearAngularVelocity
			);

			onPlayerHandoff?.Invoke();
		}
		else {
			FailTransition("Player handoff failed because the player handoff or target entry point is missing.");
			yield break;
		}

		UpdateCheckpointAfterHandoff();

		// Optional animation hook after arriving in the next scene
		SetAnimatorTrigger(airlockAnimator, depressuriseTriggerName);

		if (minimumBlackoutTime > 0.0f) {
			yield return new WaitForSeconds(minimumBlackoutTime);
		}

		if (postHandoffDelay > 0.0f) {
			yield return new WaitForSeconds(postHandoffDelay);
		}

		// Unload the previous level while the screen is still black
		yield return UnloadPreviousSceneRoutine(previousScene);

		// Reveal the new scene level
		yield return FadeTo(0.0f, fadeInDuration);

		if (restoreControlDelay > 0.0f) {
			yield return new WaitForSeconds(restoreControlDelay);
		}

		if (lockPlayerDuringTransition && playerHandoff != null) {
			playerHandoff.UnlockPlayer();
		}

		PlayOneShot(transitionCompleteClip);

		SetStatus(AirlockStatus.Complete);
		onTransitionComplete?.Invoke();

		Log("Airlock transition complete.");

		// Destroy the temporary persistent controller after the transition is finished
		Destroy(gameObject);
	}

	private IEnumerator ActivateLoadedSceneDuringBlackout() {
		// The player may reach the commit trigger before loading has reached 90%is ready
		// Wait here while the screen is hidden
		while (preloadReadyToActivate == false && currentStatus != AirlockStatus.Failed) {
			yield return null;
		}

		if (currentStatus == AirlockStatus.Failed) {
			yield break;
		}

		// Allow Unity to activate the preloaded scene now that the screen is black
		if (preloadOperation != null && preloadOperation.isDone == false) {
			preloadOperation.allowSceneActivation = true;

			while (preloadOperation.isDone == false) {
				yield return null;
			}
		}

		loadedScene = SceneManager.GetSceneByName(nextSceneName);
		if (loadedScene.IsValid() == false || loadedScene.isLoaded == false) {
			FailTransition($"Loaded scene '{nextSceneName}' is not valid after activation.");
			yield break;
		}

		loadedRoot = FindSceneRoot(loadedScene);
		if (loadedRoot == null) {
			FailTransition($"Scene '{nextSceneName}' loaded but has no SeamlessSceneRoot.");
			yield break;
		}

		targetEntryPoint = loadedRoot.GetEntryPoint();
		if (targetEntryPoint == null) {
			FailTransition($"Scene '{nextSceneName}' loaded but has no SeamlessSceneEntryPoint.");
			yield break;
		}

		// New objects created after this point are then associated with the new level scene
		SceneManager.SetActiveScene(loadedScene);

		onNextSceneActivated?.Invoke(nextSceneName);
		Log($"Scene '{nextSceneName}' activated.");
	}

	private IEnumerator UnloadPreviousSceneRoutine(Scene previousScene) {
		if (IsSceneSafeToUnload(previousScene) == false) {
			Debug.LogWarning($"{name}: Old scene '{previousScene.name}' is protected or invalid and cannot be unloaded.", this);
			yield break;
		}

		SetStatus(AirlockStatus.UnloadingPreviousScene);
		Log($"Unloading previous scene '{previousScene.name}'.");

		AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousScene);
		if (unloadOperation != null) {
			while (unloadOperation.isDone == false) {
				yield return null;
			}
		}

		onPreviousSceneUnloaded?.Invoke(previousScene.name);
	}

	private bool ResolvePlayer(Collider playerCollider) {
		GameObject playerObject = null;

		// Prefer the manually assigned Rigidbody if provided
		if (playerRigidbody != null) {
			playerObject = playerRigidbody.gameObject;
		}
		else if (playerCollider != null) {
			// If the trigger sent the player collider, then use that first
			playerRigidbody = playerCollider.attachedRigidbody;
			playerObject = playerRigidbody != null ? playerRigidbody.gameObject : playerCollider.gameObject;
		}

		// Fallback: find the persistent player by tag
		if (playerObject == null && string.IsNullOrWhiteSpace(playerTag) == false) {
			playerObject = GameObject.FindGameObjectWithTag(playerTag);

			if (playerObject != null) {
				playerRigidbody = playerObject.GetComponent<Rigidbody>();
			}
		}

		if (playerObject == null) {
			Debug.LogWarning($"{name}: Player cannot be found. Assign Player Rigidbody or make sure the player uses tag '{playerTag}'.", this);
			return false;
		}

		// The handoff component handles input locking, teleporting, and velocity behaviour
		playerHandoff = playerObject.GetComponent<AirlockPlayerHandoff>();
		if (playerHandoff == null) {
			playerHandoff = playerObject.AddComponent<AirlockPlayerHandoff>();
			Log("Added AirlockPlayerHandoff at runtime. Add it to the Player prefab for cleaner setup.");
		}

		return true;
	}

	private void OpenAirlockEntranceDoor() {
		if (airlockEntranceDoor == null || locked) {
			return;
		}

		airlockEntranceDoor.UnlockDoor();
		airlockEntranceDoor.OpenDoor();
	}

	private void CloseAirlockEntranceDoorBehindPlayer() {
		if (airlockEntranceDoor != null) {
			airlockEntranceDoor.CloseDoor();
		}
	}

	private void UpdateCheckpointAfterHandoff() {
		if (targetEntryPoint == null || CheckpointManager.Instance == null) {
			return;
		}

		if (playerRigidbody != null) {
			CheckpointManager.Instance.SetPlayer(playerRigidbody);
		}

		CheckpointManager.Instance.SetCheckpoint(targetEntryPoint.transform.position + Vector3.up * checkpointVerticalOffset);
	}

	private SeamlessSceneRoot FindSceneRoot(Scene scene) {
		if (scene.IsValid() == false || scene.isLoaded == false) {
			return null;
		}

		GameObject[] rootObjects = scene.GetRootGameObjects();

		for (int i = 0; i < rootObjects.Length; i++) {
			SeamlessSceneRoot root = rootObjects[i].GetComponentInChildren<SeamlessSceneRoot>(true);
			if (root != null) {
				return root;
			}
		}

		return null;
	}

	private bool IsSceneSafeToUnload(Scene scene) {
		if (scene.IsValid() == false || scene.isLoaded == false) {
			return false;
		}

		// Never unload the scene that was just loaded
		if (loadedScene.IsValid() && scene == loadedScene) {
			return false;
		}

		// Never unload persistent/protected scenes (mainly for the Player scene)
		for (int i = 0; i < protectedSceneNames.Length; i++) {
			if (scene.name == protectedSceneNames[i]) {
				return false;
			}
		}

		return true;
	}

	private IEnumerator FadeTo(float targetAlpha, float duration) {
		if (fadeGroup == null) {
			yield break;
		}

		float startAlpha = fadeGroup.alpha;
		float elapsed = 0.0f;

		// While the fade is black, block UI clicks/input through the fade panel
		fadeGroup.blocksRaycasts = targetAlpha > 0.0f;
		fadeGroup.interactable = targetAlpha > 0.0f;
		
		if (duration <= 0.0f) {
			fadeGroup.alpha = targetAlpha;
			yield break;
		}

		while (elapsed < duration) {
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);

			fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

			yield return null;
		}

		fadeGroup.alpha = targetAlpha;

		if (Mathf.Approximately(targetAlpha, 0.0f)) {
			fadeGroup.blocksRaycasts = false;
			fadeGroup.interactable = false;
		}
	}

	private bool ValidatePortal(bool logSuccess) {
		bool valid = true;

		if (string.IsNullOrWhiteSpace(nextSceneName)) {
			Debug.LogWarning($"{name}: Next Scene Name is empty.", this);
			valid = false;
		}

		if (airlockPlayerHoldPoint == null) {
			Debug.LogWarning($"{name}: Airlock Player Hold Point is missing. The transition can still run, but the player will not be centred inside the airlock before the fade.", this);
		}

#if UNITY_EDITOR
		if (string.IsNullOrWhiteSpace(nextSceneName) == false && IsSceneInBuildSettings(nextSceneName) == false) {
			Debug.LogWarning($"{name}: Scene '{nextSceneName}' is not enabled in Build Settings.", this);
			valid = false;
		}
#endif

		if (valid && logSuccess) {
			Debug.Log($"{name}: Airlock portal validation passed.", this);
		}

		return valid;
	}

#if UNITY_EDITOR
	private bool IsSceneInBuildSettings(string sceneName) {
		EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

		for (int i = 0; i < scenes.Length; i++) {
			if (scenes[i] == null || scenes[i].enabled == false) {
				continue;
			}

			string buildSceneName = System.IO.Path.GetFileNameWithoutExtension(scenes[i].path);
			if (buildSceneName == sceneName) {
				return true;
			}
		}

		return false;
	}
#endif

	private void MarkPreloadComplete() {
		preloadReadyToActivate = true;
		loadingProgress = 1.0f;

		onPreloadProgress?.Invoke(loadingProgress);
		onPreloadComplete?.Invoke(nextSceneName);

		SetStatus(AirlockStatus.ReadyToActivate);
		OpenAirlockEntranceDoor();

		Log($"Scene '{nextSceneName}' is preloaded and waiting for blackout activation.");
	}

	private void FailTransition(string reason) {
		SetStatus(AirlockStatus.Failed);
		onTransitionFailed?.Invoke();

		Debug.LogError($"{name}: Airlock transition failed. {reason}", this);

		if (playerHandoff != null) {
			playerHandoff.UnlockPlayer();
		}

		transitionRunning = false;
	}

	private void SetStatus(AirlockStatus status) {
		currentStatus = status;
	}

	private void SetAnimatorTrigger(Animator targetAnimator, string triggerName) {
		if (targetAnimator == null || string.IsNullOrWhiteSpace(triggerName)) {
			return;
		}

		targetAnimator.ResetTrigger(triggerName);
		targetAnimator.SetTrigger(triggerName);
	}

	private void PlayOneShot(AudioClip clip) {
		if (transitionAudioSource == null || clip == null) {
			return;
		}

		transitionAudioSource.PlayOneShot(clip);
	}

	private void Log(string message) {
		if (debugLogging) {
			Debug.Log($"{name}: {message}", this);
		}
	}

	private void OnValidate() {
		fadeOutDuration = Mathf.Max(0.0f, fadeOutDuration);
		fadeInDuration = Mathf.Max(0.0f, fadeInDuration);
		minimumBlackoutTime = Mathf.Max(0.0f, minimumBlackoutTime);

		airlockEntranceCloseDelay = Mathf.Max(0.0f, airlockEntranceCloseDelay);
		pressuriseDelay = Mathf.Max(0.0f, pressuriseDelay);
		postHandoffDelay = Mathf.Max(0.0f, postHandoffDelay);
		restoreControlDelay = Mathf.Max(0.0f, restoreControlDelay);

		checkpointVerticalOffset = Mathf.Max(0.0f, checkpointVerticalOffset);
	}

	private void OnDrawGizmosSelected() {
		if (airlockPlayerHoldPoint == null) {
			return;
		}

		Color previousColor = Gizmos.color;

		Gizmos.color = holdPointGizmoColor;
		Gizmos.DrawWireSphere(airlockPlayerHoldPoint.position, 0.35f);
		Gizmos.DrawLine(airlockPlayerHoldPoint.position, airlockPlayerHoldPoint.position + airlockPlayerHoldPoint.forward * 1.5f);

		Gizmos.color = previousColor;

#if UNITY_EDITOR
		Handles.color = holdPointGizmoColor;
		Handles.Label(airlockPlayerHoldPoint.position + Vector3.up * 0.5f, "Airlock Hold Point");
#endif
	}
}