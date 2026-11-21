using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RacingGame.Core.Save;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private SaveService observedSave;
        private bool saveWarningAcknowledged, saveFailureObserved;
        private RectTransform saveRecoveryPanel;
        private GameObject savePreviousSelection;
        private string saveRecoveryLanguage;
        private string reportedRecovery;
        private int saveRecoveryWidth, saveRecoveryHeight;
        private bool saveRecoveryReadFailed;

        private void UpdateSaveRecovery()
        {
            var save = PlatformGlue.Save;
            if (save != observedSave)
            {
                HideSaveRecovery();
                observedSave = save;
                saveWarningAcknowledged = saveFailureObserved = false;
                reportedRecovery = null;
            }
            if (save != null && save.LocalVersionTooNew)
            {
                HideSaveRecovery();
                if (saveFailureObserved) { saveFailureObserved = false; Rebuild(); }
                return;
            }
            if (save == null || !save.LocalWriteFailed)
            {
                if (saveFailureObserved && Loc.Ready)
                {
                    // A previously unread profile can contribute both progress and settings.
                    // Refresh the visible wallet and apply its audio/quality preference too.
                    SfxPlayer.Instance.volume = save.Data.settings.sfx * .8f;
                    PlatformGlue.ApplyQuality();
                    var driver = race.Player != null ? race.Player.GetComponent<PlayerDriverInput>() : null;
                    if (driver != null) driver.autoThrottle = save.Data.settings.autoThrottle || TouchControlsPresenter.IsShown;
                    Toast(Loc.Get("save.local.restored"));
                    if (!string.IsNullOrEmpty(current)) Rebuild();
                }
                saveFailureObserved = saveWarningAcknowledged = false;
                HideSaveRecovery();
                if (save != null && Loc.Ready && (flow.State != FlowState.Racing || PauseController.IsPaused))
                {
                    string notice = null;
                    if (save.RestoredLocalBackup) notice = "save.local.from_backup";
                    else if (save.LastLocalStatus == SaveParseStatus.Corrupt)
                        notice = save.LastCloudStatus == SaveParseStatus.Ok ? "save.local.from_cloud" : "save.local.no_copy";
                    if (notice != null && notice != reportedRecovery)
                    {
                        Toast(Loc.Get(notice));
                        reportedRecovery = notice;
                    }
                }
                return;
            }
            bool canShow = flow.State != FlowState.Racing || PauseController.IsPaused;
            if (!saveFailureObserved)
            {
                saveFailureObserved = true;
                if (!canShow && Loc.Ready) Toast(Loc.Get("save.local.title"));
            }
            // Do not interrupt driving with a dialog. Explain the failure at the next safe
            // menu/pause/results screen, and keep automatic retries running in the meantime.
            if (!Loc.Ready || !canShow || saveWarningAcknowledged)
            { HideSaveRecovery(); return; }
            if (saveRecoveryPanel != null && saveRecoveryLanguage == Loc.Language
                && saveRecoveryWidth == Screen.width && saveRecoveryHeight == Screen.height
                && saveRecoveryReadFailed == save.LocalReadFailed)
            { saveRecoveryPanel.SetAsLastSibling(); return; }
            HideSaveRecovery();
            saveRecoveryLanguage = Loc.Language;
            saveRecoveryWidth = Screen.width; saveRecoveryHeight = Screen.height;
            saveRecoveryReadFailed = save.LocalReadFailed;
            savePreviousSelection = EventSystem.current?.currentSelectedGameObject;
            saveRecoveryPanel = UiFactory.Panel(canvas.transform, "SaveRecovery", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, .8f), false);
            saveRecoveryPanel.GetComponent<Image>().raycastTarget = true;
            float scale = UiFactory.ReadableScale(Screen.width, Screen.height);
            float width = Mathf.Min(820f, Screen.width / scale - 32f);
            var card = UiFactory.Card(saveRecoveryPanel, "SaveRecoveryCard", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-width / 2f, -210f), new Vector2(width / 2f, 210f));
            UiFactory.Text(card, "SaveRecoveryTitle", Loc.Get(save.LocalReadFailed ? "save.local.read_title" : "save.local.title"), 32f, TextAlignmentOptions.Center,
                Top, Vector2.one, new Vector2(24f, -72f), new Vector2(-24f, -20f), UiFactory.Accent);
            UiFactory.Text(card, "SaveRecoveryMessage", Loc.Get(save.LocalReadFailed ? "save.local.read_message" : "save.local.message"), 26f, TextAlignmentOptions.Center,
                Top, Vector2.one, new Vector2(28f, -220f), new Vector2(-28f, -80f));
            var retry = UiFactory.Button(card, "SaveRetry", Loc.Get("save.local.retry"), () =>
            {
                save.RetryLocalWrite();
                UpdateSaveRecovery();
            }, Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 110f), new Vector2(-24f, 186f), 28f);
            var proceed = UiFactory.Button(card, "SaveContinue", Loc.Get("save.local.continue"), () =>
            {
                saveWarningAcknowledged = true;
                HideSaveRecovery();
            }, Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 26f), new Vector2(-24f, 102f), 28f, UiFactory.Raised);
            retry.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = proceed, selectOnUp = proceed };
            proceed.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = retry, selectOnUp = retry };
            EventSystem.current?.SetSelectedGameObject(retry.gameObject);
        }

        private void HideSaveRecovery()
        {
            if (saveRecoveryPanel == null) return;
            saveRecoveryPanel.gameObject.SetActive(false);
            Destroy(saveRecoveryPanel.gameObject);
            saveRecoveryPanel = null;
            EventSystem.current?.SetSelectedGameObject(savePreviousSelection != null && savePreviousSelection.activeInHierarchy
                ? savePreviousSelection : null);
            savePreviousSelection = null;
        }
    }
}
