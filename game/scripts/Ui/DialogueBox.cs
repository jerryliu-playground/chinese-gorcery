using ChineseGrocery.Game.View;
using ChineseGrocery.Sim.Data;
using ChineseGrocery.Sim.Story;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Shows the Sim's current dialogue (<see cref="StoryState.Current"/>): a 64x64 portrait, the speaker's name and text
/// that types itself out; click (or Space / Enter / E) to finish a line or go on. Phone calls ring in the corner;
/// captions are white text on black. Choices and the shop-name prompt come after the last line. Time is held while
/// it is open (see <see cref="SimHost.PauseForDialogue"/>); Skip skips the whole conversation and what follows it.
/// </summary>
public partial class DialogueBox : Control
{
    private const int ScreenW = 640;
    private const int ScreenH = 360;
    private const int PanelH = 84;

    private readonly ColorRect _black = new() { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
    private readonly PanelContainer _panel = new();
    private readonly TextureRect _portrait = new()
    {
        CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
    };

    private readonly Label _name = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
    private readonly Label _phoneTag = new() { Text = "ui.dialogue.on_phone" };
    private readonly Label _text = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _caption = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _more = new() { Text = "ui.dialogue.continue" };
    private readonly Button _skip = new() { Text = "ui.dialogue.skip" };
    private readonly TextureRect _phone = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly VBoxContainer _choices = new();
    private readonly PanelContainer _prompt = new();
    private readonly LineEdit _shopName = new() { CustomMinimumSize = new Vector2(180, 0) };

    private SimHost _host = null!;
    private FeelConfig _feel = null!;
    private Art _art = null!;
    private DialogueDef? _dialogue;
    private int _line;
    private float _revealed;
    private float _age;

    /// <summary>Developer screenshot tours that are not about the story keep dialogues off screen (and time running).</summary>
    public bool Suppressed
    {
        get => !_host.PauseForDialogue;
        set => _host.PauseForDialogue = !value;
    }

    public void Init(SimHost host, FeelConfig feel, Art art)
    {
        _host = host;
        _feel = feel;
        _art = art;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _black.Size = new Vector2(ScreenW, ScreenH);
        AddChild(_black);

        _caption.Position = new Vector2(80, 140);
        _caption.Size = new Vector2(ScreenW - 160, 80);
        _caption.HorizontalAlignment = HorizontalAlignment.Center;
        _caption.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_caption);

        _phone.Texture = art.Get("ui_phone");
        _phone.Size = new Vector2(32, 32);
        _phone.PivotOffset = new Vector2(16, 16);
        _phone.Position = new Vector2(ScreenW - 48, ScreenH - PanelH - 6 - 38);
        AddChild(_phone);

        BuildPanel();
        BuildPrompt();

        _choices.Position = new Vector2(ScreenW - 250, ScreenH - PanelH - 12);
        _choices.GrowVertical = GrowDirection.Begin;
        _choices.CustomMinimumSize = new Vector2(240, 0);
        AddChild(_choices);

        _skip.Pressed += Skip;
        AddChild(_skip);
    }

    /// <summary>Covers the whole screen, so every click lands here (the HUD itself is not sized to the screen).</summary>
    public override void _Ready()
    {
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
    }

    private void BuildPanel()
    {
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.09f, 0.12f, 0.96f), BorderColor = new Color(0.62f, 0.5f, 0.32f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 6, ContentMarginTop = 6, ContentMarginRight = 6, ContentMarginBottom = 4,
        });
        _panel.Position = new Vector2(8, ScreenH - PanelH - 6);
        _panel.Size = new Vector2(ScreenW - 16, PanelH);
        _panel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_panel);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(row);
        var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.22f, 0.2f, 0.24f) });
        frame.AddChild(_portrait);
        row.AddChild(frame);

        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(column);
        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 8);
        _name.AddThemeColorOverride("font_color", new Color(0.98f, 0.82f, 0.45f));
        _phoneTag.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.6f));
        header.AddChild(_name);
        header.AddChild(_phoneTag);
        column.AddChild(header);
        _text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _text.CustomMinimumSize = new Vector2(0, 44);
        column.AddChild(_text);
        _more.HorizontalAlignment = HorizontalAlignment.Right;
        _more.Modulate = new Color(1, 1, 1, 0.6f);
        column.AddChild(_more);
    }

    private void BuildPrompt()
    {
        _prompt.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.15f, 0.98f), BorderColor = new Color(0.62f, 0.5f, 0.32f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 8, ContentMarginTop = 6, ContentMarginRight = 8, ContentMarginBottom = 8,
        });
        var box = new VBoxContainer();
        _prompt.AddChild(box);
        box.AddChild(new Label { Text = "ui.dialogue.name_shop_title" });
        box.AddChild(new Label { Text = "ui.dialogue.name_shop_hint", Modulate = new Color(1, 1, 1, 0.7f) });
        box.AddChild(_shopName);
        var ok = new Button { Text = "ui.dialogue.name_shop_ok" };
        ok.Pressed += ConfirmShopName;
        _shopName.TextSubmitted += _ => ConfirmShopName();
        box.AddChild(ok);
        _prompt.Visible = false;
        AddChild(_prompt);
    }

    private StoryData Story => _host.Data.Story!;

    /// <summary>Picks up a change of the Sim's current dialogue (done every frame; the screenshot tour calls it directly).</summary>
    public void Sync()
    {
        var current = Suppressed || _host.Sim is null || _host.Data.Story is null ? null : _host.Sim.Story.Current;
        if (current != _dialogue?.Id) Show(current);
    }

    public override void _Process(double delta)
    {
        Sync();
        if (!Visible) return;

        _age += (float)delta;
        var full = _text.GetTotalCharacterCount();
        _revealed = Math.Min(full, _revealed + (float)delta * _feel.DialogueCharsPerSecond);
        _text.VisibleCharacters = (int)_revealed;
        _caption.VisibleCharacters = (int)_revealed;
        _more.Visible = LineDone && !_choices.Visible && !_prompt.Visible;

        // The phone rings: a quick wobble at first, then a gentle one while the call lasts.
        var strength = _age < _feel.PhoneRingSeconds ? 0.35f : 0.06f;
        _phone.Rotation = Mathf.Sin(_age * 30) * strength;
    }

    private bool LineDone => (int)_revealed >= Math.Max(_text.GetTotalCharacterCount(), _caption.GetTotalCharacterCount());

    private void Show(string? id)
    {
        _dialogue = id is null ? null : Story.FindDialogue(id);
        Visible = _dialogue is not null;
        if (_dialogue is null) return;

        _line = 0;
        _age = 0;
        var caption = _dialogue.Style == DialogueStyle.Caption;
        _black.Visible = caption;
        _caption.Visible = caption;
        _panel.Visible = !caption;
        _phone.Visible = _dialogue.Style == DialogueStyle.Phone;
        _phoneTag.Visible = _dialogue.Style == DialogueStyle.Phone;
        _prompt.Visible = false;
        ClearChoices();
        ShowLine();

        // Skip sits in the box's top-right corner, or bottom-right on a black screen.
        _skip.Size = _skip.GetCombinedMinimumSize();
        _skip.Position = caption
            ? new Vector2(ScreenW - 10 - _skip.Size.X, ScreenH - 10 - _skip.Size.Y)
            : new Vector2(ScreenW - 14 - _skip.Size.X, ScreenH - PanelH - 6 + 4);
    }

    private void ShowLine()
    {
        var line = _dialogue!.Lines[_line];
        var text = Fill(UiText.T(line.Key));
        _revealed = 0;
        _text.Text = text;
        _caption.Text = text;
        _text.VisibleCharacters = 0;
        _caption.VisibleCharacters = 0;
        if (_dialogue.Style == DialogueStyle.Caption) return;

        var character = Story.Settings.Characters.FirstOrDefault(c => c.Id == line.Speaker);
        _name.Text = character is null ? "" : UiText.T(character.NameKey);
        _portrait.Texture = _art.Get(StoryValidator.PortraitSprite(Story, line.Speaker, line.Expression));
    }

    /// <summary>Fills {protagonist}, {dad}, {mom} (and any other character id) and {shop}.</summary>
    public string Fill(string text)
    {
        foreach (var c in Story.Settings.Characters) text = text.Replace("{" + c.Id + "}", UiText.T(c.NameKey));
        return text.Replace("{shop}", ShopName());
    }

    public string ShopName() => _host.Sim.Story.ShopName ?? UiText.T(Story.Settings.DefaultShopNameKey);

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Advance();
            AcceptEvent();
        }
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible || _prompt.Visible) return;
        if (e is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Space or Key.Enter or Key.KpEnter or Key.E)
        {
            Advance();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Click: show the whole line, else the next line, else the answers or prompt, else finish.</summary>
    public void Advance()
    {
        if (_dialogue is null || _choices.Visible || _prompt.Visible) return;
        if (!LineDone)
        {
            _revealed = _text.GetTotalCharacterCount() + _caption.GetTotalCharacterCount();
            return;
        }

        if (_line + 1 < _dialogue.Lines.Count)
        {
            _line++;
            ShowLine();
            return;
        }

        if (_dialogue.Choices is { Count: > 0 } choices)
        {
            ShowChoices(choices);
            return;
        }

        if (_dialogue.Prompt == DialoguePrompt.ShopName)
        {
            _shopName.MaxLength = Story.Settings.ShopNameMaxLength;
            _shopName.PlaceholderText = UiText.T(Story.Settings.DefaultShopNameKey);
            _shopName.Text = _host.Sim.Story.ShopName ?? "";
            _prompt.Visible = true;
            _prompt.Size = _prompt.GetCombinedMinimumSize();
            _prompt.Position = ((new Vector2(ScreenW, ScreenH - PanelH) - _prompt.Size) / 2).Floor();
            _shopName.GrabFocus();
            return;
        }

        Finish(null);
    }

    private void ShowChoices(IReadOnlyList<DialogueChoiceDef> choices)
    {
        ClearChoices();
        foreach (var choice in choices)
        {
            var b = new Button { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            b.Text = Fill(UiText.T(choice.Key));
            b.Pressed += () => Finish(choice.Id);
            _choices.AddChild(b);
        }

        _choices.Visible = true;
    }

    private void ClearChoices()
    {
        foreach (var child in _choices.GetChildren())
        {
            _choices.RemoveChild(child);
            child.QueueFree();
        }

        _choices.Visible = false;
    }

    private void ConfirmShopName()
    {
        var name = _shopName.Text.Trim();
        if (name.Length > 0) _host.SendNow(new NameShop(name));
        _prompt.Visible = false;
        Finish(null);
    }

    private void Finish(string? choiceId)
    {
        if (_dialogue is not { } d) return;
        _host.SendNow(new FinishDialogue(d.Id, choiceId));
    }

    private void Skip()
    {
        if (_dialogue is not { } d) return;
        _prompt.Visible = false;
        _host.SendNow(new SkipDialogue(d.Id));
    }

    /// <summary>Line of the current dialogue on screen (for the screenshot tour's log).</summary>
    public int LineIndex => _line;

    /// <summary>For the screenshot tour: jump to a line with its text fully shown.</summary>
    public void DebugShowLine(int line)
    {
        if (_dialogue is null) return;
        _line = Math.Clamp(line, 0, _dialogue.Lines.Count - 1);
        ShowLine();
        _revealed = 10_000;
    }

    /// <summary>For the screenshot tour: open the answers or the shop-name prompt of the current dialogue.</summary>
    public void DebugEnd()
    {
        if (_dialogue is null) return;
        DebugShowLine(_dialogue.Lines.Count - 1);
        Advance();
    }
}
