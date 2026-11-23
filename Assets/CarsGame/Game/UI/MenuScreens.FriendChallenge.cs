using RacingGame.Core.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private string challengeInput = "";

        private static TMP_InputField LineInput(RectTransform field, int limit, string initial)
        {
            field.GetComponent<Image>().raycastTarget = true;
            var viewport = UiFactory.Panel(field, "Viewport", Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = UiFactory.Text(viewport, "Code", "", 24f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.richText = false; text.textWrappingMode = TextWrappingModes.NoWrap;
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport; input.textComponent = text; input.targetGraphic = field.GetComponent<Image>();
            input.lineType = TMP_InputField.LineType.SingleLine; input.richText = false; input.characterLimit = limit;
            input.text = initial;
            return input;
        }

        private void BuildFriendChallenge(Transform content, RaceEventDefinition selected)
        {
            var row = Item(content, "FriendChallenge", 240f);
            var status = Label(row, "ChallengeStatus", Loc.Get("challenge.code"), 27f, Top, Vector2.one,
                new Vector2(18f, -64f), new Vector2(-18f, -8f));
            var field = UiFactory.Panel(row, "ChallengeCode", Vector2.zero, Vector2.one, new Vector2(16f, 91f), new Vector2(-16f, -68f), UiFactory.Raised);
            var input = LineInput(field, FriendChallenge.MaxCodeLength, challengeInput);
            input.onValueChanged.AddListener(value => challengeInput = value);

            var best = selected != null && selected.mode == EventMode.Race ? PlatformGlue.Save?.Data.progress.bests.Find(b => b != null && b.eventId == selected.eventId) : null;
            string encoded = FriendChallenge.Encode(best);
            UiFactory.Button(row, "CreateChallenge", Loc.Get("challenge.create"), string.IsNullOrEmpty(encoded) ? null : () =>
            {
                input.text = encoded; input.Select(); input.ActivateInputField(); input.selectionAnchorPosition = 0; input.selectionFocusPosition = encoded.Length;
                status.text = Loc.Format("challenge.target", "t", UiFactory.FormatMilliseconds(best.bestTimeMs));
            }, Vector2.zero, new Vector2(.5f, 0f), new Vector2(16f, 12f), new Vector2(-5f, 80f), 24f, UiFactory.Raised);
            UiFactory.Button(row, "StartChallenge", Loc.Get("challenge.start"), () =>
            {
                if (!flow.StartFriendChallenge(input.text, out string error)) status.text = Loc.Get(error);
            }, new Vector2(.5f, 0f), new Vector2(1f, 0f), new Vector2(5f, 12f), new Vector2(-16f, 80f), 24f);
        }
    }
}
