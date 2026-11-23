using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private Slider replaySlider;
        private TMP_Text replayTime, replayAction;
        private int lastReplayMilliseconds;
        private string replayActionKey;

        private void BuildReplayControls()
        {
            var bar = UiFactory.Card(panel, "ReplayControls", Vector2.zero, new Vector2(1f, 0f),
                new Vector2(12f, 12f), new Vector2(-12f, Compact ? 144f : 166f));
            UiFactory.Button(bar, "Back", Loc.Get("common.back"), Back, new Vector2(0f, .5f), new Vector2(0f, 1f),
                new Vector2(12f, 2f), new Vector2(164f, -8f), 25f, UiFactory.Raised);
            replayTime = Label(bar, "ReplayTime", "", 30f, new Vector2(0f, .5f), Vector2.one,
                new Vector2(172f, 0f), new Vector2(-208f, -8f), Color.white, TextAlignmentOptions.Center);
            var toggle = UiFactory.Button(bar, "ReplayToggle", "", flow.ToggleReplay, new Vector2(1f, .5f), Vector2.one,
                new Vector2(-200f, 2f), new Vector2(-12f, -8f), 26f);
            replayAction = toggle.GetComponentInChildren<TMP_Text>();
            var root = UiFactory.Panel(bar, "ReplaySeek", Vector2.zero, new Vector2(1f, .5f),
                new Vector2(12f, 8f), new Vector2(-12f, -3f), Color.clear);
            replaySlider = BuildSlider(root, flow.ReplayDuration, flow.ReplayTime, false);
            replaySlider.onValueChanged.AddListener(seconds =>
            {
                flow.SeekReplay(seconds);
                lastReplayMilliseconds = -1;
                UpdateReplayControls();
            });
            lastReplayMilliseconds = -1;
            replayActionKey = null;
            UpdateReplayControls();
            EventSystem.current?.SetSelectedGameObject(toggle.gameObject);
        }

        private void UpdateReplayControls()
        {
            if (!flow.ViewingReplay || replaySlider == null) return;
            replaySlider.SetValueWithoutNotify(flow.ReplayTime);
            int milliseconds = Mathf.RoundToInt(flow.ReplayTime * 1000f);
            if (milliseconds != lastReplayMilliseconds && (flow.ReplayPaused || lastReplayMilliseconds < 0
                || Mathf.Abs(milliseconds - lastReplayMilliseconds) >= 100))
            {
                replayTime.text = UiFactory.FormatMilliseconds(milliseconds) + " / " + UiFactory.FormatMilliseconds(Mathf.RoundToInt(flow.ReplayDuration * 1000f));
                lastReplayMilliseconds = milliseconds;
            }
            string action = !flow.ReplayPaused ? "replay.pause" : flow.ReplayTime >= flow.ReplayDuration ? "pause.restart" : "pause.resume";
            if (replayActionKey != action) { replayAction.text = Loc.Get(action); replayActionKey = action; }
        }
    }
}
