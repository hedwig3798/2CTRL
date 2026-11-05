using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 창 크기 / 전체 화면 관련 PlayerSettings 를 설정한다 (멱등)
    /// - 창은 마우스 드래그로 자유롭게 크기 조절 (resizableWindow). 비율은 런타임 Letterbox 가 16:9 로 유지한다.
    /// - 첫 실행 기본값은 GameSettings 기본 화면 모드(ScreenModes.DEFAULT_INDEX)와 맞춘다.
    ///   (실행 직후 SettingsApplier 가 저장값으로 다시 적용)
    /// </summary>
    public static class DisplaySettingsBuilder
    {
        private const int DEFAULT_WINDOW_WIDTH = 1280;
        private const int DEFAULT_WINDOW_HEIGHT = 720;

        [MenuItem("Tools/Builders/DisplaySettings")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[DisplaySettingsBuilder]");

            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultScreenWidth = DEFAULT_WINDOW_WIDTH;
            PlayerSettings.defaultScreenHeight = DEFAULT_WINDOW_HEIGHT;
            PlayerSettings.fullScreenMode = true == ScreenModes.IsFullScreen(ScreenModes.DEFAULT_INDEX)
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            AssetDatabase.SaveAssets();

            sb.AppendLine("resizableWindow = " + PlayerSettings.resizableWindow);
            sb.AppendLine("fullScreenMode = " + PlayerSettings.fullScreenMode);
            sb.AppendLine("defaultScreen = " + PlayerSettings.defaultScreenWidth + "x" + PlayerSettings.defaultScreenHeight);
            return sb.ToString();
        }
    }
}
