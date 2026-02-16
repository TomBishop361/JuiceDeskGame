// e.g Implement this in each entity script
// Example: PlayerController: public Faction OwnerFaction => Faction.Player;
public interface IFactionOwner {
	Faction OwnerFaction { get; }
}