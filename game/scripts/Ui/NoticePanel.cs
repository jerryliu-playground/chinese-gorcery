using ChineseGrocery.Game.View;
using ChineseGrocery.Sim.Calendar;
using Godot;

namespace ChineseGrocery.Game.Ui;

/// <summary>
/// Popups for festival reminders and newly announced events, one at a time with an icon; offers and power cuts carry
/// their answer buttons.
/// </summary>
public partial class NoticePanel : PanelBase
{
    private readonly Queue<Sim.Core.SimEvent> _queue = new();
    private readonly Art _art = new();

    public bool HasNotices => _queue.Count > 0;

    public void Add(Sim.Core.SimEvent notice) => _queue.Enqueue(notice);

    public void Clear() => _queue.Clear();

    protected override void Rebuild()
    {
        ClearBody();
        if (!_queue.TryPeek(out var notice))
        {
            Close();
            return;
        }

        var (icon, title, text, e) = notice switch
        {
            FestivalReminder r => Reminder(r),
            StoreEventAnnounced a => ("" + Host.Sim.EventDefOf(a.Event).Icon, UiText.T(Host.Sim.EventDefOf(a.Event).NameKey), NoticeText.Describe(Host, a.Event), a.Event),
            _ => ("ui_warning", "", "", (StoreEvent?)null),
        };
        SetTitle(title);
        var picture = new TextureRect
        {
            Texture = _art.Get(icon), CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        var label = Text(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(250, 0);
        Body.AddChild(Row(picture, label));

        var buttons = new HBoxContainer();
        if (e is not null)
        {
            var current = Host.Sim.StoreEvents.FirstOrDefault(x => x.Id == e.Id) ?? e;
            foreach (var b in NoticeText.Actions(Host, current, Next)) buttons.AddChild(b);
        }

        buttons.AddChild(ActionButton("ui.notice.ok", Next));
        Footer.AddChild(buttons);
    }

    private (string, string, string, StoreEvent?) Reminder(FestivalReminder r)
    {
        var f = Host.Data.Festivals!.Festivals.Single(x => x.Id == r.FestivalId);
        return ("ui_calendar", UiText.F("ui.notice.reminder_title", ("name", UiText.T(f.NameKey))), UiText.F("festival." + f.Id + ".reminder", ("days", r.DaysUntil)), null);
    }

    private void Next()
    {
        if (_queue.Count > 0) _queue.Dequeue();
        if (_queue.Count == 0) Close();
        else Rebuild();
    }
}
