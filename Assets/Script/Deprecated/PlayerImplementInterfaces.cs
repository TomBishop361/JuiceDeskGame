using UnityEngine;

//DEPRECATED

// TODO: Add IFactionOwner + IHealthSettings interface implementations to the PlayerController.cs
// Reason I did this: prevent conflicts w/ same script commits
public class PlayerImplementInterfaces : MonoBehaviour, IFactionOwner, IHealthSettings {
	// Implement IFactionOwner
	public Faction OwnerFaction => Faction.Player;

	// Implement IHealthSettings
	public float MaxHealth => 10; // TODO: PlayerSettings.cs -> acts as a config for all player properties
	public float LowHealthThreshold => 3; // TODO: CHANGE THIS FROM MAGIC NUMBER
}