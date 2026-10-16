namespace ONI_Together.Networking.States
{
	public enum CursorState
	{
		NONE,
		SELECT,        // Default inspect/select tool
		BUILD,         // Place buildings
		DIG,           // Dig terrain
		CANCEL,        // Cancel tasks
		DECONSTRUCT,   // Deconstruct buildings
		PRIORITIZE,    // Set priority levels >= 5
		DEPRIORITIZE,  // Set priority levels < 5
		SWEEP,         // Mark items to be swept
		MOP,           // Mop up liquids
		HARVEST,       // Harvest crops
		DISINFECT,     // Disinfect areas
		ATTACK,        // Attack critters or objects
		CAPTURE,       // Capture critters
		WRANGLE,       // Wrangle critters for transport
		EMPTY_PIPE,    // Empty contents of pipes
		DISCONNECT,    // Disconnect wires, pipes etc
		CLEAR_FLOOR,   // Mark debris for removal
		MOVE_TO,        // Direct duplicants to move
		// Sandbox tools
		SANDBOX_BRUSH,
		SANDBOX_SPRINKLE,
		SANDBOX_FLOOD,
		SANDBOX_SAMPLE,
		SANDBOX_HEAT,
		SANDBOX_STRESS,
		SANDBOX_SPAWN,
		SANDBOX_DESTROY,
		SANDBOX_REVEAL,
		SANDBOX_CLEAR_FLOOR,
		SANDBOX_CRITTER,
		SANDBOX_STORY_TRAIT
	}
}
