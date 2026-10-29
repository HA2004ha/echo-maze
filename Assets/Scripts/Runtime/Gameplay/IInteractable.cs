namespace EchoMaze.Gameplay
{
    /// <summary>
    /// Something the player can do a thing to with E.
    ///
    /// Only the door implements it today. The seam exists now because the input read belongs to
    /// <see cref="Player"/> (one place touches the keyboard, one place decides what a press means) and the
    /// prompt belongs to the HUD: both of those want to talk to "whatever is in range" rather than to a
    /// door specifically, and the lever/valve set piece the GDD puts later in the term hangs off here
    /// without touching either of them.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>The line the HUD draws while the player is standing in range of this.</summary>
        string Prompt { get; }

        /// <summary>
        /// Whether the press would do anything. A door that is already opening has nothing to say, and a
        /// prompt that lies about what E does is worse than no prompt.
        /// </summary>
        bool CanInteract(Player player);

        void Interact(Player player);
    }
}
