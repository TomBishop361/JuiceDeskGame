// Interface used by Hunt Mode to create silhouettes/outlines without depending on a specific outline shader
public interface IHuntRevealVisual {
	void SetRevealVisible(bool visible);
	void SetRevealOpacity(float opacity);
}