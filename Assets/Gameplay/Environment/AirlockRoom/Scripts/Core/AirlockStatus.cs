// Represents the current state of the airlock scene transition
// Used by the portal to prevent repeated triggers, track loading progress,
// and make debugging the transition easier
public enum AirlockStatus {
	// The airlock is inactive and ready to be used
	Idle = 0,

	// The airlock is locked and cannot start preloading or transitioning
	Locked = 1,

	// The player is inside the airlock and the effect has started
	AirlockSealing = 2,

	// The hidden blackout/fade moment is active and the next scene is being loaded
	LoadingNextScene = 3,

	// The player is being moved from the old airlock room to the next scene entry point
	HandingOffPlayer = 4,

	// The previous level scene is being unloaded after the player has moved to the next scene
	UnloadingPreviousScene = 5,

	// The transition finished successfully and player control can be restored
	Complete = 6,

	// The transition failed because required setup, loading, player lookup, or entry-point lookup failed
	Failed = 7
}
