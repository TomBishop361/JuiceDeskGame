using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Obsolete("SeamlessScenePortal is now obsolete. Use the AirlockSceneTransitionPortal instead.")]
// Seamless door/portal controller
// This one component owns the entire seamless transition:
// - preloads the next level additively,
// - aligns that level's entry point to this door's exit anchor,
// - opens the door once the next scene is ready,
// - commits the player into the streamed scene,
// - then unloads the old level scene
[DisallowMultipleComponent]
public sealed class SeamlessScenePortal : MonoBehaviour {
	[Serializable]
	public sealed class LoadingProgressEvent : UnityEvent<float> { }

	[Serializable]
	public sealed class SceneNameEvent : UnityEvent<string> { }

	[Header("Scene Target")]
	[Tooltip("Name of the next level scene to load additively. This must exactly match a scene in Build Settings.")]
	[SerializeField] private string nextSceneName;
	[Tooltip("ID of the SeamlessSceneEntryPoint inside the next scene that should line up with this door.")]
	[SerializeField] private string targetEntryPointId = "Entry_A";
	[Tooltip("World-space connection point on this door/corridor. The streamed scene entry point is moved and rotated to match this transform.")]
	[SerializeField] private Transform exitAnchor;

	[Header("Door References")]
	[Tooltip("Optional animator on the existing door model. The portal sends Open and Close trigger names to it.")]
	[SerializeField] private Animator doorAnimator;
	[Tooltip("Optional physical blocker that remains enabled while the portal is locked or while the next scene is not ready.")]
	[SerializeField] private Collider blockingCollider;

	[Header("Door Behaviour")]
	[Tooltip("If true, the portal starts locked and ignores preload/commit until UnlockPortal is called.")]
	[SerializeField] private bool startsLocked = false;
	[Tooltip("If true, the door opens automatically once loading finishes while the player is still in the preload zone.")]
	[SerializeField] private bool autoOpenWhenReady = true;
	[Tooltip("Animator trigger used to open the existing door animation.")]
	[SerializeField] private string openTriggerName = "Open";
	[Tooltip("Animator trigger used to close the existing door animation.")]
	[SerializeField] private string closeTriggerName = "Close";

	[Header("Commit / Unload")]
	[Tooltip("If true, the old level scene unloads after the player commits into the new scene.")]
	[SerializeField] private bool unloadPreviousSceneAfterCommit = true;
	[Tooltip("Time (in seconds) to wait after commit before unloading the old scene.")]
	[SerializeField] private float unloadPreviousSceneDelay = 0.5f;
	[Tooltip("Scene name that should never be unloaded by this portal. Example: Player scene is additive so don't unload it.")]
	[SerializeField] private string protectedPlayerSceneName = "Player";
	[Tooltip("If true and the player reaches the commit trigger early, the portal commits automatically as soon as loading finishes.")]
	[SerializeField] private bool commitWhenReadyIfPlayerIsWaiting = true;

	[Header("Unity Events - Core")]
	[Tooltip("Invoked every frame while loading. Value is 0-1 and can be used for loading bars, text, or emissive shader fill etc.")]
	[SerializeField] private LoadingProgressEvent onLoadingProgressChanged;
	[Tooltip("Invoked when additive scene loading starts. Parameter: next scene name.")]
	[SerializeField] private SceneNameEvent onPreloadStarted;
	[Tooltip("Invoked when additive scene loading and alignment finishes. Parameter: next scene name.")]
	[SerializeField] private SceneNameEvent onPreloadCompleted;
	[Tooltip("Invoked when the streamed scene becomes the active gameplay scene. Parameter: next scene name.")]
	[SerializeField] private SceneNameEvent onCommitCompleted;

	[Header("Unity Events - Portal Status")]
	[Tooltip("Invoked when the portal is locked or the player tries to use it while locked. Example: Red door lights or denied audio.")]
	[SerializeField] private UnityEvent onLockedStatus;
	[Tooltip("Invoked when the portal starts loading. Example: Amber lights or 'pressurising' text.")]
	[SerializeField] private UnityEvent onLoadingStatus;
	[Tooltip("Invoked when the streamed scene is loaded, aligned, and ready. Example: Blue/green lights.")]
	[SerializeField] private UnityEvent onReadyStatus;
	[Tooltip("Invoked when the portal opens the existing door animator.")]
	[SerializeField] private UnityEvent onOpenStatus;
	[Tooltip("Invoked when the player commits into the next scene.")]
	[SerializeField] private UnityEvent onCommittedStatus;
	[Tooltip("Invoked if preload/alignment fails.")]
	[SerializeField] private UnityEvent onFailedStatus;
	[Tooltip("Invoked when the door closes.")]
	[SerializeField] private UnityEvent onDoorClosed;

	[Header("Debug")]
	[Tooltip("If true, the seamless scene loading process is logged to the Unity Console.")]
	[SerializeField] private bool debugLogging = true;
	[Tooltip("Colour used for the exit anchor gizmo.")]
	[SerializeField] private Color exitAnchorGizmoColor = new Color(0.1f, 0.85f, 1.0f, 1.0f);

	//public string NextSceneName => nextSceneName;
	//public string TargetEntryPointId => targetEntryPointId;
	public PortalStatus CurrentStatus => currentStatus;
	//public bool IsLocked => locked;
	//public bool IsLoading => loadRoutine != null;
	public bool IsSceneReady => sceneReady;
	//public bool HasCommitted => committed;
	//public float LoadingProgress => loadingProgress;

	private PortalStatus currentStatus = PortalStatus.Idle;
	private Scene loadedScene;
	private float loadingProgress;
	private bool locked;
	private bool preloadRequested;
	private bool sceneReady;
	private bool committed;
	private bool doorOpen;
	private bool playerInPreloadZone;
	private bool playerInCommitZone;

	private void Reset() {
		doorAnimator = GetComponentInChildren<Animator>();

		if (exitAnchor == null) {
			GameObject anchor = new GameObject("SeamlessExitAnchor");
			anchor.transform.SetParent(transform);

			// The generated anchor is only a failsafe
			// The seamless exit anchor should be moved to the exact doorway/corridor connection point
			anchor.transform.localPosition = Vector3.forward * 2.0f;
			anchor.transform.localRotation = Quaternion.identity;

			exitAnchor = anchor.transform;
		}
	}

	private void Awake() {
		locked = startsLocked;

		SetStatus(locked ? PortalStatus.Locked : PortalStatus.Idle);
		UpdateBlockingCollider();
	}

	private void Update() {
		if (preloadRequested && committed == false) {
			// This keeps material/UI progress listeners updated even if Unity's async progress
			// does not change every frame near the end of loading
			onLoadingProgressChanged?.Invoke(loadingProgress);
		}

		// The blocker is updated continuously so external systems can lock/unlock the portal
		// without needing to know about the collider directly
		UpdateBlockingCollider();
	}

	// Called by SeamlessSceneTriggerVolume when the player enters a preload or commit trigger
	public void HandleTriggerEntered(SeamlessSceneTriggerVolume.TriggerAction action, Collider playerCollider) {
		if (action == SeamlessSceneTriggerVolume.TriggerAction.Preload) {
			playerInPreloadZone = true;

			// Preloading starts before the player reaches the doorway so the next level
			// can become visible through the door rather than just appearing after a fade
			RequestPreload();

			// This will only open immediately if the scene is already ready, or if Open Only When Next Scene Ready is disabled
			TryOpenDoor();
		}
		else if (action == SeamlessSceneTriggerVolume.TriggerAction.Commit) {
			playerInCommitZone = true;

			// Commit can be requested early
			// If loading is not complete yet, CommitTransition marks that the player is waiting and automatically finishes when ready
			CommitTransition();
		}
	}

	// Called by SeamlessSceneTriggerVolume when the player exits a preload or commit trigger
	public void HandleTriggerExited(SeamlessSceneTriggerVolume.TriggerAction action, Collider playerCollider) {
		if (action == SeamlessSceneTriggerVolume.TriggerAction.Preload) {
			playerInPreloadZone = false;

			// Do not close the door after commit, otherwise the old door will animate while
			// the old scene is about to unload and causes errors
			if (committed == false) {
				TryCloseDoor();
			}
		}
		else if (action == SeamlessSceneTriggerVolume.TriggerAction.Commit) {
			playerInCommitZone = false;
		}
	}

	// Unlocks the portal
	// To be called by level-complete events, enemy tracker events, or alarms etc
	public void UnlockPortal() {
		locked = false;

		SetStatus(sceneReady ? PortalStatus.Ready : PortalStatus.Idle);
		UpdateBlockingCollider();

		Log("Portal unlocked.");
	}

	// Locks the portal
	// To be called by alarm lockdowns / encounter start events
	public void LockPortal() {
		locked = true;

		TryCloseDoor();
		SetStatus(PortalStatus.Locked);
		UpdateBlockingCollider();

		Log("Portal locked.");
	}

	// Starts additive loading for the next scene (can be called more than once)
	[ContextMenu("Request Preload")]
	public void RequestPreload() {
		if (locked) {
			// Report locked status again so denied user feedback can fire every time the player tries
			SetStatus(PortalStatus.Locked);
			return;
		}

		if (preloadRequested) {
			return;
		}

		if (ValidatePortal(false) == false) {
			SetStatus(PortalStatus.Failed);
			return;
		}

		preloadRequested = true;
		StartCoroutine(PreloadRoutine());
	}

	// Commits the player into the streamed scene if it is ready, otherwise waits for readiness
	[ContextMenu("Commit Transition")]
	public void CommitTransition() {
		if (committed) {
			return;
		}

		if (locked) {
			SetStatus(PortalStatus.Locked);
			return;
		}

		if (preloadRequested == false) {
			RequestPreload();
		}

		if (sceneReady == false) {
			if (commitWhenReadyIfPlayerIsWaiting) {
				// This remembers that the player reached the handoff point before loading finished
				// FinishPreloadFromLoadedScene will call CommitTransition again once alignment succeeds
				playerInCommitZone = true;
			}

			Log("Commit requested before scene was ready. Waiting for additive load to finish.");
			return;
		}

		committed = true;

		SetStatus(PortalStatus.Committing);

		// Record this before SetActiveScene
		// After SetActiveScene, SceneManager.GetActiveScene() will return the new scene instead
		Scene previousScene = SceneManager.GetActiveScene();

		if (loadedScene.IsValid() && loadedScene.isLoaded) {
			SceneManager.SetActiveScene(loadedScene);
		}

		TryOpenDoor();

		SetStatus(PortalStatus.Committed);
		onCommitCompleted?.Invoke(nextSceneName);

		if (unloadPreviousSceneAfterCommit && IsSceneSafeToUnload(previousScene)) {
			// The portal is attached to the exit door in the previous scene, so it may be destroyed when that scene unloads
			// Running the unload on a persistent runner prevents the coroutine from being stopped early
			SeamlessSceneRuntimeRunner.Run(UnloadPreviousSceneRoutine(previousScene, unloadPreviousSceneDelay, debugLogging));
		}
	}

	// Opens the door if it is allowed to open
	public void TryOpenDoor() {
		if (locked) {
			SetStatus(PortalStatus.Locked);
			return;
		}

		if (sceneReady == false) {
			return;
		}

		if (doorOpen) {
			return;
		}

		doorOpen = true;

		SetAnimatorTrigger(openTriggerName);
		SetStatus(PortalStatus.Open);
	}

	// Closes the door using the configured animator trigger
	public void TryCloseDoor() {
		if (doorOpen == false) {
			return;
		}

		doorOpen = false;

		SetAnimatorTrigger(closeTriggerName);
		onDoorClosed?.Invoke();

		if (locked) {
			SetStatus(PortalStatus.Locked);
		}
		else if (sceneReady) {
			SetStatus(PortalStatus.Ready);
		}
		else {
			SetStatus(PortalStatus.Idle);
		}
	}

	// Validates any setup mistakes on the seamless scene portal system
	[ContextMenu("Validate Seamless Portal")]
	public bool ValidatePortal() {
		return ValidatePortal(true);
	}


	private IEnumerator PreloadRoutine() {
		SetStatus(PortalStatus.Loading);

		loadingProgress = 0.0f;
		onLoadingProgressChanged?.Invoke(loadingProgress);
		onPreloadStarted?.Invoke(nextSceneName);

		Log($"Preloading scene '{nextSceneName}'.");

		Scene alreadyLoadedScene = SceneManager.GetSceneByName(nextSceneName);
		if (alreadyLoadedScene.IsValid() && alreadyLoadedScene.isLoaded) {
			// This handles testing or cases where another portal already loaded the target scene
			loadedScene = alreadyLoadedScene;
			FinishPreloadFromLoadedScene();
			yield break;
		}

		AsyncOperation loadOperation;
		try {
			loadOperation = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);
		} catch (Exception exception) {
			Debug.LogError($"{name}: Could not start additive load for scene '{nextSceneName}'. Is it added to Build Settings?\n{exception}", this);

			SetStatus(PortalStatus.Failed);

			yield break;
		}

		if (loadOperation == null) {
			Debug.LogError($"{name}: LoadSceneAsync returned null for '{nextSceneName}'. Check the scene name and Build Settings.", this);

			SetStatus(PortalStatus.Failed);

			yield break;
		}

		while (loadOperation.isDone == false) {
			// Scene loading seems to report 0.0-0.9 while loading, then complete at 1.0
			// Dividing by 0.9 allows a cleaner 0-1 value for UI/shaders
			loadingProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
			onLoadingProgressChanged?.Invoke(loadingProgress);

			yield return null;
		}

		loadedScene = SceneManager.GetSceneByName(nextSceneName);

		FinishPreloadFromLoadedScene();
	}

	private void FinishPreloadFromLoadedScene() {
		SeamlessSceneRoot loadedRoot = FindSceneRoot(loadedScene);

		if (loadedRoot == null) {
			Debug.LogWarning($"{name}: Scene '{nextSceneName}' loaded but has no SeamlessSceneRoot. The scene cannot be aligned to the door.", this);
			SetStatus(PortalStatus.Failed);
			return;
		}

		//// Main seamless-loading step
		//// The streamed level is moved so its chosen entry point exactly matches the current scenes door exit anchor
		////bool aligned = loadedRoot.AlignEntryPointToExit(targetEntryPointId, exitAnchor);
		//if (aligned == false) {
		//	SetStatus(PortalStatus.Failed);
		//	return;
		//}

		loadingProgress = 1.0f;
		sceneReady = true;

		onLoadingProgressChanged?.Invoke(loadingProgress);
		onPreloadCompleted?.Invoke(nextSceneName);

		SetStatus(PortalStatus.Ready);
		UpdateBlockingCollider();

		Log($"Scene '{nextSceneName}' loaded, aligned, and ready.");

		if (autoOpenWhenReady && playerInPreloadZone) {
			TryOpenDoor();
		}

		if (commitWhenReadyIfPlayerIsWaiting && playerInCommitZone) {
			CommitTransition();
		}
	}

	private SeamlessSceneRoot FindSceneRoot(Scene scene) {
		if (scene.IsValid() == false || scene.isLoaded == false) {
			return null;
		}

		GameObject[] rootObjects = scene.GetRootGameObjects();

		SeamlessSceneRoot foundRoot = null;
		int foundCount = 0;

		for (int i = 0; i < rootObjects.Length; i++) {
			GameObject rootObject = rootObjects[i];

			if (rootObject == null) {
				continue;
			}

			SeamlessSceneRoot root = rootObject.GetComponentInChildren<SeamlessSceneRoot>(true);
			if (root == null) {
				continue;
			}

			foundRoot = root;
			foundCount++;
		}

		if (foundCount > 1) {
			Debug.LogWarning($"{name}: Scene '{scene.name}' has {foundCount} SeamlessSceneRoot components. Use one streamed root per scene.", this);
		}

		return foundRoot;
	}

	private bool ValidatePortal(bool logSuccess) {
		bool valid = true;

		if (string.IsNullOrWhiteSpace(nextSceneName)) {
			Debug.LogWarning($"{name}: Next Scene Name is empty.", this);
			valid = false;
		}

		if (string.IsNullOrWhiteSpace(targetEntryPointId)) {
			Debug.LogWarning($"{name}: Target Entry Point ID is empty.", this);
			valid = false;
		}

		if (exitAnchor == null) {
			Debug.LogWarning($"{name}: Exit Anchor is missing. Create a child transform at the door connection point.", this);
			valid = false;
		}

		if (valid && logSuccess) {
			Debug.Log($"{name}: Seamless portal validation passed.", this);
		}

		return valid;
	}

	private bool IsSceneSafeToUnload(Scene scene) {
		if (scene.IsValid() == false || scene.isLoaded == false) {
			return false;
		}

		// Never unload the scene that the player just committed into
		if (loadedScene.IsValid() && scene == loadedScene) {
			return false;
		}

		// The player is additive/persistent
		// This prevents the portal from deleting the player scene
		if (string.IsNullOrWhiteSpace(protectedPlayerSceneName) == false && scene.name == protectedPlayerSceneName) {
			return false;
		}

		return true;
	}

	private static IEnumerator UnloadPreviousSceneRoutine(Scene previousScene, float delay, bool log) {
		if (delay > 0.0f) {
			yield return new WaitForSeconds(delay);
		}

		if (previousScene.IsValid() == false || previousScene.isLoaded == false) {
			yield break;
		}

		if (log) {
			Debug.Log($"SeamlessScenePortal: Unloading previous scene '{previousScene.name}'.");
		}

		AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousScene);

		if (unloadOperation != null) {
			while (unloadOperation.isDone == false) {
				yield return null;
			}
		}
	}

	private void SetStatus(PortalStatus newStatus) {
		currentStatus = newStatus;

		switch (newStatus) {
			case PortalStatus.Locked:
				onLockedStatus?.Invoke();
				break;

			case PortalStatus.Loading:
				onLoadingStatus?.Invoke();
				break;

			case PortalStatus.Ready:
				onReadyStatus?.Invoke();
				break;

			case PortalStatus.Open:
				onOpenStatus?.Invoke();
				break;

			case PortalStatus.Committed:
				onCommittedStatus?.Invoke();
				break;

			case PortalStatus.Failed:
				onFailedStatus?.Invoke();
				break;
		}
	}

	private void UpdateBlockingCollider() {
		if (blockingCollider == null) {
			return;
		}

		// The blocker prevents the player entering empty space if they reach the door
		// before the streamed scene has finished loading/alignment
		bool shouldBlock = locked || sceneReady == false;
		blockingCollider.enabled = shouldBlock;
	}

	private void SetAnimatorTrigger(string triggerName) {
		if (doorAnimator == null || string.IsNullOrWhiteSpace(triggerName)) {
			return;
		}

		// Resetting before SetTrigger prevents a redundant trigger from a previous open/close attempt
		// from causing any inconsistent door animation behaviour
		doorAnimator.ResetTrigger(triggerName);
		doorAnimator.SetTrigger(triggerName);
	}

	private void Log(string message) {
		if (debugLogging) {
			Debug.Log($"{name}: {message}", this);
		}
	}

	private void OnValidate() {
		unloadPreviousSceneDelay = Mathf.Max(0.0f, unloadPreviousSceneDelay);
	}

	private void OnDrawGizmosSelected() {
		if (exitAnchor == null) {
			return;
		}

		Color previousColor = Gizmos.color;

		Gizmos.color = exitAnchorGizmoColor;
		Gizmos.DrawWireSphere(exitAnchor.position, 0.35f);
		Gizmos.DrawLine(exitAnchor.position, exitAnchor.position + exitAnchor.forward * 2.0f);
		DrawArrowHead(exitAnchor.position + exitAnchor.forward * 2.0f, exitAnchor.forward);

		Gizmos.color = previousColor;

#if UNITY_EDITOR
		Handles.color = exitAnchorGizmoColor;
		Handles.Label(exitAnchor.position + Vector3.up * 0.6f, $"Exit -> {nextSceneName}:{targetEntryPointId}");
#endif
	}

	private void DrawArrowHead(Vector3 position, Vector3 direction) {
		Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

		Quaternion lookRotation = Quaternion.LookRotation(safeDirection, Vector3.up);
		Vector3 right = lookRotation * Quaternion.Euler(0.0f, 150.0f, 0.0f) * Vector3.forward;
		Vector3 left = lookRotation * Quaternion.Euler(0.0f, -150.0f, 0.0f) * Vector3.forward;

		Gizmos.DrawLine(position, position + right * 0.35f);
		Gizmos.DrawLine(position, position + left * 0.35f);
	}
}