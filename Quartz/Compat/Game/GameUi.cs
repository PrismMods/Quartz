namespace Quartz.Compat.Game;
public static class GameUi {
    public static void HideDifficultyContainer(scrUIController uiController) {
        if(uiController == null) return;
        try {
            if(uiController.difficultyContainer != null && uiController.difficultyContainer.gameObject.activeSelf)
                uiController.difficultyContainer.gameObject.SetActive(false);
            if(uiController.difficultyFadeContainer != null) {
                if(uiController.difficultyFadeContainer.blocksRaycasts)
                    uiController.difficultyFadeContainer.blocksRaycasts = false;
                if(uiController.difficultyFadeContainer.gameObject.activeSelf)
                    uiController.difficultyFadeContainer.gameObject.SetActive(false);
            }
            if(uiController.difficultyButtonLeft != null && uiController.difficultyButtonLeft.enabled)
                uiController.difficultyButtonLeft.enabled = false;
            if(uiController.difficultyButtonRight != null && uiController.difficultyButtonRight.enabled)
                uiController.difficultyButtonRight.enabled = false;
        } catch { }
    }
}
