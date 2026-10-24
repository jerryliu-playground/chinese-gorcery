using Godot;

namespace ChineseGrocery.Game;

/// <summary>Registers the input actions in code so the bindings are readable in one place.</summary>
public static class InputSetup
{
    public const string MoveUp = "move_up";
    public const string MoveDown = "move_down";
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string Interact = "interact";
    public const string Rotate = "rotate";
    public const string Cancel = "cancel";

    public static void Register()
    {
        Bind(MoveUp, Key.W, Key.Up);
        Bind(MoveDown, Key.S, Key.Down);
        Bind(MoveLeft, Key.A, Key.Left);
        Bind(MoveRight, Key.D, Key.Right);
        Bind(Interact, Key.E, Key.Space);
        Bind(Rotate, Key.R);
        Bind(Cancel, Key.Escape);
    }

    private static void Bind(string action, params Key[] keys)
    {
        if (InputMap.HasAction(action)) return;

        InputMap.AddAction(action);
        foreach (var key in keys)
        {
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        }
    }
}
