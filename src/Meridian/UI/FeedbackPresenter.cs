using System;
using System.Collections.Generic;
using Godot;
using Meridian.Core;

namespace Meridian.UI;

public enum UIFeedbackKind { Information, Success, Failure, Quest, Save }
public readonly record struct UIFeedbackEvent(string MessageKey, UIFeedbackKind Kind = UIFeedbackKind.Information, float Seconds = 3f);
public readonly record struct UIHoldProgressEvent(string ActionKey, float Progress, bool Visible);

/// <summary>Bounded, event-driven toast and hold-progress presenter shared by every gameplay system.</summary>
public partial class FeedbackPresenter : Control
{
    [Export(PropertyHint.Range, "1,10,1")] public int MaxQueuedMessages { get; set; } = 5;
    private readonly Queue<UIFeedbackEvent> _queue = new();
    private Label? _toast;
    private Label? _holdLabel;
    private ProgressBar? _holdProgress;
    private IDisposable? _feedbackSubscription;
    private IDisposable? _noticeSubscription;
    private IDisposable? _holdSubscription;
    private bool _presenting;
    private int _sequence;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _toast = GetNodeOrNull<Label>("Toast");
        _holdLabel = GetNodeOrNull<Label>("HoldPanel/Rows/Label");
        _holdProgress = GetNodeOrNull<ProgressBar>("HoldPanel/Rows/Progress");
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            _feedbackSubscription = eventBus.Subscribe<UIFeedbackEvent>(Enqueue);
            _noticeSubscription = eventBus.Subscribe<HudNoticeEvent>(notice => Enqueue(new UIFeedbackEvent(notice.Message)));
            _holdSubscription = eventBus.Subscribe<UIHoldProgressEvent>(OnHoldProgress);
        }
    }

    public override void _ExitTree()
    {
        _feedbackSubscription?.Dispose();
        _noticeSubscription?.Dispose();
        _holdSubscription?.Dispose();
    }

    private void Enqueue(UIFeedbackEvent message)
    {
        if (_queue.Count >= MaxQueuedMessages) _queue.Dequeue();
        _queue.Enqueue(message);
        if (!_presenting) PresentNext();
    }

    private void PresentNext()
    {
        if (_toast == null || _queue.Count == 0)
        {
            _presenting = false;
            return;
        }
        _presenting = true;
        UIFeedbackEvent message = _queue.Dequeue();
        _toast.Text = Tr(message.MessageKey);
        _toast.Visible = true;
        int sequence = ++_sequence;
        GetTree().CreateTimer(Mathf.Max(0.5f, message.Seconds), processAlways: true).Timeout += () =>
        {
            if (!IsInstanceValid(this) || sequence != _sequence) return;
            _toast.Visible = false;
            PresentNext();
        };
    }

    private void OnHoldProgress(UIHoldProgressEvent progress)
    {
        if (_holdProgress == null || _holdLabel == null) return;
        _holdProgress.GetParent<Control>().Visible = progress.Visible;
        _holdProgress.Value = Mathf.Clamp(progress.Progress, 0f, 1f) * 100f;
        _holdLabel.Text = Tr(progress.ActionKey);
    }
}
