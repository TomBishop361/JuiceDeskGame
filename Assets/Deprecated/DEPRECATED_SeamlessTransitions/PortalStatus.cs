using System;

[Obsolete("PortalStatus enum is now obsolete. Use the AirlockStatus enum instead.")]
// Runtime state used by SeamlessScenePortal when tracking an additive scene
// Useful for sci-fi door lights, holograms, and UI text etc
public enum PortalStatus {
	// Default inactive state
	// The portal has not started loading, is not locked, and is waiting for the player or another system to request preload
	Idle = 0,

	// The portal is locked and cannot currently load, open, or commit
	// Useful for doors blocked by enemy encounters, alarms, lockdowns, objectives, or level-completion requirements
	Locked = 1,

	// The next scene is currently being loaded additively in the background
	// This is a good state for things such as amber lights or loading UI etc
	Loading = 2,

	// The next scene has finished loading and has been aligned to the exit anchor
	// The player can now see the streamed scene through the door and the door is safe to open
	Ready = 3,

	// The door is open
	// The next scene is visible and the player can physically move through the transition space
	Open = 4,

	// The player has crossed the commit trigger and the system is handing control to the streamed scene
	// During this state, the new scene may become the active scene and the previous scene may be queued for unload
	Committing = 5,

	// The transition has completed successfully
	// The streamed scene is now the active gameplay scene and the previous scene may have been unloaded or scheduled for unload
	Committed = 6,

	// The portal failed to complete its preload or alignment
	// Example causes: missing scene in Build Settings, a missing SeamlessSceneRoot, a missing entry point ID, or a missing exit anchor
	Failed = 7
}

